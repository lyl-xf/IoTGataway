using System.Globalization;
using IoTGateway.ProtocolSdk;

namespace TutorialMeterPlugin;

/// <summary>
/// 教程配套插件：模拟一块「电表」，按物模型地址返回固定数值，写入阈值演示 Write。
/// PluginId: tutorial.meter
/// </summary>
public sealed class MeterProtocolPlugin : IProtocolPlugin
{
    public string PluginId => "tutorial.meter";

    public string DisplayName => "教程案例：模拟电表 (tutorial.meter)";

    public IDeviceProtocolSession CreateSession(ProtocolConnectionContext context) => new MeterSession(context);
}

internal sealed class MeterSession : IDeviceProtocolSession
{
    private readonly ProtocolConnectionContext _ctx;
    private bool _open;
    private double _setpoint = 100.0;

    public MeterSession(ProtocolConnectionContext ctx) => _ctx = ctx;

    public bool IsConnected => _open;

    public ProtocolResult Open()
    {
        // 真实协议里这里建立 TCP/串口；教程里直接认为已连接
        _open = true;
        return ProtocolResult.Ok();
    }

    public void Close()
    {
        _open = false;
    }

    public ProtocolResult<object?> Read(string address, ProtocolDataType dataType)
    {
        if (!_open)
            return ProtocolResult<object?>.Fail("未连接");

        var key = address.Trim().ToUpperInvariant();

        // 地址约定（与物模型里填的「寄存器地址」一致即可）
        switch (key)
        {
            case "U":
            case "VOLT":
                return BoxByDataType(220.5, dataType);
            case "I":
            case "AMP":
                return BoxByDataType(1.25, dataType);
            case "P":
                return BoxByDataType(275.625, dataType); // U*I 近似
            case "LIMIT":
                return BoxByDataType(_setpoint, dataType); // 反映上次 Write 到 SETPOINT 的值
            case "CFG":
                return ProtocolResult<object?>.Ok(string.IsNullOrWhiteSpace(_ctx.PluginConfigJson) ? "{}" : _ctx.PluginConfigJson);
            default:
                return ProtocolResult<object?>.Fail($"未知地址: {address}，教程支持 U/I/P/VOLT/AMP/CFG");
        }
    }

    public ProtocolResult Write(string address, ProtocolDataType dataType, string? value)
    {
        if (!_open)
            return ProtocolResult.Fail("未连接");

        var key = address.Trim().ToUpperInvariant();
        if (key is "SETPOINT" or "SP")
        {
            if (string.IsNullOrWhiteSpace(value))
                return ProtocolResult.Fail("缺少写入值");
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                return ProtocolResult.Fail("阈值必须是数字");
            _setpoint = d;
            return ProtocolResult.Ok();
        }

        return ProtocolResult.Fail("教程仅支持写入地址 SETPOINT 或 SP");
    }

    public void Dispose()
    {
        Close();
        GC.SuppressFinalize(this);
    }

    private static ProtocolResult<object?> BoxByDataType(double raw, ProtocolDataType dt)
    {
        try
        {
            object? v = dt switch
            {
                ProtocolDataType.Bool => raw != 0,
                ProtocolDataType.Int16 => (short)raw,
                ProtocolDataType.Int32 => (int)raw,
                ProtocolDataType.Float => (float)raw,
                ProtocolDataType.Double => raw,
                ProtocolDataType.String => raw.ToString(CultureInfo.InvariantCulture),
                ProtocolDataType.Short => (short)raw,
                ProtocolDataType.UShort => (ushort)Math.Clamp(raw, 0, ushort.MaxValue),
                ProtocolDataType.Long => (long)raw,
                ProtocolDataType.ULong => (ulong)Math.Max(0, raw),
                _ => raw
            };
            return ProtocolResult<object?>.Ok(v);
        }
        catch (Exception ex)
        {
            return ProtocolResult<object?>.Fail(ex.Message);
        }
    }
}
