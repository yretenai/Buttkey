using Buttbee;
using Buttbee.Messages;
using Buttkey.Events;
using DragonLib.CommandLine;
using Serilog;

namespace Buttkey;

internal static class Program {
    private static async Task Main(string[] args) {
        var flags = CommandLineFlagsParser.ParseFlags<ButtkeyFlags>();
        if (flags == null) {
            return;
        }

        flags.Logger = new SerilogWrapper(new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger());

        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += delegate {
            Log.Debug("Canceling...");
            // ReSharper disable once AccessToDisposedClosure
            cts.Cancel();
        };

        await new ButtplugRouter(flags).Start(cts.Token);

        cts.Token.WaitHandle.WaitOne();
    }
}

internal class ButtplugRouter {
    public ButtplugRouter(ButtkeyFlags flags) {
        Flags = flags;
        Misskey = new MisskeyStreamClient(flags.Host, flags.Logger);
        Buttplug = new ButtbeeClient(flags.ButtplugHost, flags.ButtplugPort, flags.Logger);
    }

    public ButtkeyFlags Flags { get; }
    public MisskeyStreamClient Misskey { get; set; }
    public ButtbeeClient Buttplug { get; set; }
    public CancellationToken Token { get; set; }

    public async Task Start(CancellationToken token) {
        Token = token;
        await Buttplug.Connect(token);
        await Misskey.Connect(Flags.Token, token);
        await Misskey.Consume("main", Trigger);
        await Buttplug.RefreshDevices();
    }

    private void Trigger(object? sender, MisskeyChannelEvent e) {
        if (Flags.Types.Contains(e.Type)) {
            Task.Run(async () => {
                    foreach (var device in Buttplug.Devices.Values) {
                        await device.Scalar(ButtplugDeviceActuatorType.Vibrate, Flags.Intensity / 100d);
                    }

                    await Task.Delay(Flags.Duration, Token);
                    await Buttplug.StopDevices();
                },
                Token);
        }
    }
}
