namespace IoTGateway.ProtocolSdk;

/// <summary>
/// Connection parameters passed to a plugin when opening a device session.
/// Maps from gateway <c>Device</c> for <c>ProtocolType.Custom</c>.
/// </summary>
public sealed class ProtocolConnectionContext
{
    /// <summary>Matches <see cref="IProtocolPlugin.PluginId"/> used to create this session.</summary>
    public string PluginId { get; init; } = string.Empty;

    /// <summary>Optional per-device JSON for the plugin (connection options, credentials, etc.).</summary>
    public string PluginConfigJson { get; init; } = string.Empty;

    public string Ip { get; init; } = string.Empty;
    public int Port { get; init; }
    public string PortName { get; init; } = string.Empty;
    public int BaudRate { get; init; }
    public int DataBits { get; init; }
    public int StopBits { get; init; }
    public int Parity { get; init; }
    public string PlcVersion { get; init; } = string.Empty;
}
