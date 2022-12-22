namespace Buttkey.Messages;

public record MisskeyConnect : MisskeyBody {
    public string Channel { get; init; } = null!;
}
