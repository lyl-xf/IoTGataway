using IoTClient.Clients.Modbus;
using IoTClient.Clients.PLC;
using IoTClient.Common.Enums;
using IoTClient.Enums;
using IoTGateway.Models;
using System.IO.Ports;

namespace IoTGateway.Services
{
    public static class IoTClientFactory
    {
        public static dynamic CreateClient(Device device)
        {
            return device.ProtocolType switch
            {
                ProtocolType.ModbusTcp => new ModbusTcpClient(device.Ip, device.Port),
                ProtocolType.ModbusRtu => new ModbusRtuClient(device.PortName, device.BaudRate, device.DataBits, (StopBits)device.StopBits, (Parity)device.Parity),
                ProtocolType.ModbusAscii => new ModbusAsciiClient(device.PortName, device.BaudRate, device.DataBits, (StopBits)device.StopBits, (Parity)device.Parity),
                ProtocolType.SiemensClient => new SiemensClient((SiemensVersion)Enum.Parse(typeof(SiemensVersion), device.PlcVersion), new System.Net.IPEndPoint(System.Net.IPAddress.Parse(device.Ip), device.Port)),
                ProtocolType.MitsubishiClient => new MitsubishiClient((MitsubishiVersion)Enum.Parse(typeof(MitsubishiVersion), device.PlcVersion), device.Ip, device.Port),
                ProtocolType.OmronFinsClient => new OmronFinsClient(device.Ip, device.Port),
                ProtocolType.Custom => throw new InvalidOperationException("自定义协议由插件处理，不应调用 IoTClientFactory。"),
                _ => throw new NotSupportedException($"Protocol {device.ProtocolType} is not supported.")
            };
        }
    }
}