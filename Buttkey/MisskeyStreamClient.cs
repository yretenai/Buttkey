using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Buttbee;
using Buttkey.Events;
using Buttkey.Messages;

namespace Buttkey;

public class MisskeyStreamClient : IDisposable, IAsyncDisposable {
    private readonly object IdLock = new();

    public MisskeyStreamClient(string host, IButtbeeLogger? logger = null) {
        WebSocket = new ClientWebSocket();
        Host = new Uri($"wss://{host}");
        Logger = logger?.AddContext<MisskeyStreamClient>();
    }

    protected IButtbeeLogger? Logger { get; }
    public bool Debug { get; set; }

    public ClientWebSocket WebSocket { get; }
    public Uri Host { get; }
    private Thread? RxThread { get; set; }
    private CancellationTokenSource? CancellationTokenSource { get; set; }
    private ConcurrentDictionary<uint, EventHandler<MisskeyChannelEvent>> Callbacks { get; } = new();
    private uint IdCurrent { get; set; }

    public JsonSerializerOptions SerializerOptions { get; } = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
        },
    };

    public ValueTask DisposeAsync() {
        Close();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async Task Connect(string apiKey, CancellationToken token = default) {
        if (CancellationTokenSource is not null || RxThread is not null) {
            Logger?.Warn("Already connected");
            return;
        }

        if (WebSocket.State is not WebSocketState.None) {
            Logger?.Warn("WebSocket is not in a valid state");
            return;
        }

        Logger?.Info("Connecting to {Host}", Host);
        await WebSocket.ConnectAsync(new Uri(Host, $"?i={apiKey}"), token);

        CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);

        RxThread = new Thread(Rx) {
            Name = "MisskeyStream.Rx",
            IsBackground = true,
        };

        RxThread.Start();

        await Task.Delay(200, token);
    }

    public async Task Send<T>(MisskeyMessageType type, T message) where T : MisskeyBody {
        var msg = JsonSerializer.Serialize(new MisskeyMessage<T>(type, message), SerializerOptions);

        if (Debug) {
            Logger?.Verbose("Sending message {Name} with data {Data}", type, msg);
        }

        var buffer = Encoding.UTF8.GetBytes(msg);
        await WebSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationTokenSource!.Token);
    }

    public async Task<uint> Consume(string name, EventHandler<MisskeyChannelEvent> callback) {
        uint id;
        lock (IdLock) {
            id = ++IdCurrent;
        }

        Callbacks.TryAdd(id, callback);
        await Send(MisskeyMessageType.Connect, new MisskeyConnect { Id = id.ToString(), Channel = name });
        return id;
    }

    public async Task Unconsume(uint id) {
        Callbacks.TryRemove(id, out _);
        await Send(MisskeyMessageType.Disconnect, new MisskeyBody { Id = id.ToString() });
    }

    private void Rx() {
        Logger?.Info("Starting Rx thread");
        var buffer = ArrayPool<byte>.Shared.Rent(0x100000);
        var segment = new Memory<byte>(buffer);

        try {
            var fullBuffer = Array.Empty<byte>();
            while (CancellationTokenSource is { IsCancellationRequested: false }) {
                if (WebSocket.State is not WebSocketState.Open) {
                    break;
                }

                var result = WebSocket.ReceiveAsync(segment[..0x100000], CancellationTokenSource.Token).AsTask().Result;

                if (CancellationTokenSource.IsCancellationRequested) {
                    break;
                }

                if (WebSocket.State is not WebSocketState.Open) {
                    break;
                }

                if (result.MessageType is WebSocketMessageType.Close) {
                    Close();
                    break;
                }

                if (!result.EndOfMessage) {
                    var tmp = new byte[fullBuffer.Length + result.Count];
                    Array.Copy(fullBuffer, 0, tmp, 0, fullBuffer.Length);
                    Array.Copy(buffer, 0, tmp, fullBuffer.Length, result.Count);
                    fullBuffer = tmp;
                    continue;
                }

                string json;
                if (fullBuffer != Array.Empty<byte>()) {
                    Array.Copy(buffer, 0, fullBuffer, result.Count, result.Count);
                    json = Encoding.UTF8.GetString(fullBuffer);
                    fullBuffer = Array.Empty<byte>();
                } else {
                    json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                }

                if (Debug) {
                    Logger?.Verbose("Received message message {Data}", json);
                }

                try {
                    var document = JsonSerializer.Deserialize<MisskeyMessage<JsonElement>>(json, SerializerOptions);
                    if (document?.Body.ValueKind is not JsonValueKind.Object) {
                        continue;
                    }

                    if (document.Type is MisskeyMessageType.Channel) {
                        var channel = document.Body.Deserialize<MisskeyChannel>(SerializerOptions);
                        if (channel?.Body.ValueKind is not JsonValueKind.Object) {
                            continue;
                        }

                        if (Callbacks.TryGetValue(uint.Parse(channel.Id), out var callback)) {
                            callback(this, new MisskeyChannelEvent(channel.Type, channel.Body));
                        }
                    }
                } catch (JsonException e) {
                    Logger?.Critical(e, "Failed to parse json");
                }
            }
        } catch (Exception e) {
            if (e is not TaskCanceledException && e.InnerException is not TaskCanceledException && !(e is AggregateException agg && agg.InnerExceptions.OfType<TaskCanceledException>().Any())) {
                Logger?.Critical("Failed to receive message: {Message}", e.Message);
                Close();
                throw;
            }
        } finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        Logger?.Warn("Exiting Rx thread");
    }

    protected virtual void Dispose(bool disposing) {
        if (disposing) {
            // nothing.
        }

        Close();
    }

    ~MisskeyStreamClient() {
        Dispose(false);
    }

    public virtual void Close() {
        if (WebSocket.State is not WebSocketState.Open) {
            return;
        }

        Logger?.Info("Closing connection to {Host}", Host);

        CancellationTokenSource?.Cancel();
        CancellationTokenSource?.Dispose();
        CancellationTokenSource = null;

        if (RxThread is not null) {
            RxThread.Join();
            RxThread = null;
        }
    }
}
