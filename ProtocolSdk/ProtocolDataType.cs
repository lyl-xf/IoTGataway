namespace IoTGateway.ProtocolSdk;

/// <summary>
/// Must stay in sync with <c>IoTGateway.Models.DataType</c> numeric values (0–11).
/// </summary>
public enum ProtocolDataType
{
    Bool,
    Int16,
    Int32,
    Float,
    Double,
    String,
    Coil,
    Discrete,
    Short,
    UShort,
    Long,
    ULong
}
