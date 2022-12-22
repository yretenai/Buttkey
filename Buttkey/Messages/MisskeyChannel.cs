using System.Text.Json;

namespace Buttkey.Messages;

public record MisskeyChannel : MisskeyBody {
    public MisskeyChannelType Type { get; init; }
    public JsonElement Body { get; init; }
}
