namespace IoTGateway.Models
{
    public class GatewayBackupDto
    {
        /// <summary>2 = device plugin fields; 1 = legacy export still importable.</summary>
        public int SchemaVersion { get; set; } = 2;
        public DateTime ExportedAtUtc { get; set; }
        public List<DeviceBackupDto> Devices { get; set; } = new();
        public MqttBackupDto? Mqtt { get; set; }
    }

    public class DeviceBackupDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        public int Port { get; set; }
        public ProtocolType ProtocolType { get; set; }
        public string PortName { get; set; } = string.Empty;
        public int BaudRate { get; set; }
        public int DataBits { get; set; }
        public int StopBits { get; set; }
        public int Parity { get; set; }
        public string PlcVersion { get; set; } = string.Empty;
        public string PluginId { get; set; } = string.Empty;
        public string PluginConfigJson { get; set; } = string.Empty;
        public int PollInterval { get; set; }
        public bool IsActive { get; set; }
        public List<DeviceVariableBackupDto> Variables { get; set; } = new();
    }

    public class DeviceVariableBackupDto
    {
        public long Id { get; set; }
        public long DeviceId { get; set; }
        public string Address { get; set; } = string.Empty;
        public DataType DataType { get; set; }
        public string Alias { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ReadWriteAccess ReadWrite { get; set; }
    }

    public class MqttBackupDto
    {
        public string BrokerIp { get; set; } = string.Empty;
        public int Port { get; set; }
        public string ClientId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string PubTopic { get; set; } = string.Empty;
        public string SubTopic { get; set; } = string.Empty;
    }
}
