namespace IoTGateway.ProtocolSdk;

/// <summary>
/// One logical connection to a field device (Modbus TCP client, serial port, plugin handle, etc.).
/// Implementations must be thread-safe for a single session if the gateway locks per session (current behavior).
/// </summary>
public interface IDeviceProtocolSession : IDisposable
{
    ProtocolResult Open();
    void Close();
    bool IsConnected { get; }

    ProtocolResult<object?> Read(string address, ProtocolDataType dataType);

    ProtocolResult Write(string address, ProtocolDataType dataType, string? value);
}
