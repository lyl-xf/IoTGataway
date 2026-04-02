using IoTGateway.Models;
using IoTGateway.ProtocolSdk;

namespace IoTGateway.Services;

public sealed class ProtocolSessionFactory(IProtocolPluginHost pluginHost)
{
    public IDeviceProtocolSession CreateSession(Device device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (device.ProtocolType == ProtocolType.Custom)
        {
            var id = device.PluginId?.Trim() ?? "";
            if (string.IsNullOrEmpty(id))
                throw new InvalidOperationException("自定义协议需要填写插件 ID (PluginId)。");

            if (!pluginHost.TryGetPlugin(id, out var plugin) || plugin == null)
                throw new InvalidOperationException(
                    $"未找到协议插件「{id}」。请将包含该插件的 DLL 放入应用程序目录下的 plugins 文件夹后重启网关。");

            var ctx = new ProtocolConnectionContext
            {
                PluginId = id,
                PluginConfigJson = device.PluginConfigJson ?? "",
                Ip = device.Ip ?? "",
                Port = device.Port,
                PortName = device.PortName ?? "",
                BaudRate = device.BaudRate,
                DataBits = device.DataBits,
                StopBits = device.StopBits,
                Parity = device.Parity,
                PlcVersion = device.PlcVersion ?? ""
            };

            return plugin.CreateSession(ctx);
        }

        return new BuiltInIoTClientSession(device);
    }
}
