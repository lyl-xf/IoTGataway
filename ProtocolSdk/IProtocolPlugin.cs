namespace IoTGateway.ProtocolSdk;

/// <summary>
/// Entry point for a protocol plugin DLL. Implement and ship one public class per plugin assembly.
/// </summary>
public interface IProtocolPlugin
{
    /// <summary>Stable id, e.g. <c>vendor.product</c>. Stored on <c>Device.PluginId</c>.</summary>
    string PluginId { get; }

    string DisplayName { get; }

    /// <summary>Create a new session. The gateway pools sessions by device connection fingerprint.</summary>
    IDeviceProtocolSession CreateSession(ProtocolConnectionContext context);
}
