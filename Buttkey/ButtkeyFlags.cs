using Buttbee;
using Buttkey.Messages;
using DragonLib.CommandLine;

namespace Buttkey;

public record ButtkeyFlags : CommandLineFlags {
    [Flag("host", Positional = 0, Help = "Host to connect to")]
    public string Host { get; set; } = null!;

    [Flag("token", Positional = 1, Help = "API Token to use")]
    public string Token { get; set; } = null!;

    [Flag("buttplug-host", Help = "buttplug.io host to connect to")]
    public string ButtplugHost { get; set; } = "localhost";

    [Flag("buttplug-port", Help = "buttplug.io port to connect to")]
    public ushort ButtplugPort { get; set; } = 12345;

    [Flag("types", Help = "Types of notifications to listen for")]
    public List<MisskeyChannelType> Types { get; set; } = new() { MisskeyChannelType.ReceiveFollowRequest, MisskeyChannelType.Followed, MisskeyChannelType.Mention, MisskeyChannelType.Notification };

    [Flag("intensity", Help = "Intensity of the vibration")]
    public uint Intensity { get; set; } = 33;

    [Flag("duration", Help = "Duration of the vibration")]
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(300);

    public IButtbeeLogger Logger { get; set; } = null!;
}

