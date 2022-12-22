namespace Buttkey.Messages;

public record MisskeyBody {
    public string Id { get; init; } = null!;
}

public record MisskeyMessage<T>(MisskeyMessageType Type, T Body);
