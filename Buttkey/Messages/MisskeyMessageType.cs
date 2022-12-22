using System.Text.Json.Serialization;

namespace Buttkey.Messages;

public enum MisskeyMessageType {
    ReadNotification,
    [JsonPropertyName("s")] SubscribeNote,
    [JsonPropertyName("sr")] SubscribeReadNote,
    [JsonPropertyName("un")] UnsubscribeNote,
    Connect,
    Disconnect,
    Channel,
}
