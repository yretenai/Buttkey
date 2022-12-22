using System.Text.Json;
using Buttkey.Messages;

namespace Buttkey.Events;

public class MisskeyChannelEvent {
    public MisskeyChannelEvent(MisskeyChannelType type, JsonElement element) {
        Type = type;
        Element = element;
    }

    public JsonElement Element { get; }
    public MisskeyChannelType Type { get; set; }
}
