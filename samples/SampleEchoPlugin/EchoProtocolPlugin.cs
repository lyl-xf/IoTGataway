using IoTGateway.ProtocolSdk;

namespace SampleEchoPlugin;

/// <summary>演示插件：读操作返回「地址 + 可选配置」字符串，写操作始终成功。无需真实设备。</summary>
public sealed class EchoProtocolPlugin : IProtocolPlugin
{
    public string PluginId => "sample.echo";

    public string DisplayName => "示例：回显 (sample.echo)";

    public IDeviceProtocolSession CreateSession(ProtocolConnectionContext context) => new EchoSession(context);
}

internal sealed class EchoSession : IDeviceProtocolSession
{
    private readonly ProtocolConnectionContext _ctx;
    private bool _open;

    public EchoSession(ProtocolConnectionContext ctx) => _ctx = ctx;

    public bool IsConnected => _open;

    public ProtocolResult Open()
    {
        _open = true;
        return ProtocolResult.Ok();
    }

    public void Close() => _open = false;

    public ProtocolResult<object?> Read(string address, ProtocolDataType dataType)
    {
        if (!_open)
            return ProtocolResult<object?>.Fail("未连接");

        var cfg = string.IsNullOrWhiteSpace(_ctx.PluginConfigJson) ? "" : $"|cfg={_ctx.PluginConfigJson}";
        return ProtocolResult<object?>.Ok($"{address}{cfg}");
    }

    public ProtocolResult Write(string address, ProtocolDataType dataType, string? value)
    {
        if (!_open)
            return ProtocolResult.Fail("未连接");

        return ProtocolResult.Ok();
    }

    public void Dispose()
    {
        Close();
        GC.SuppressFinalize(this);
    }
}
