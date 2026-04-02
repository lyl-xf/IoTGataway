using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace IoTGateway.Models
{
    public enum ProtocolType 
    { 
        ModbusTcp, 
        ModbusRtu, 
        ModbusAscii, 
        SiemensClient, 
        MitsubishiClient, 
        OmronFinsClient,
        /// <summary>Implemented by DLL in /plugins; set <see cref="Device.PluginId"/>.</summary>
        Custom
    }
    public enum DataType { Bool, Int16, Int32, Float, Double, String, Coil, Discrete, Short, UShort, Long, ULong }
    public enum ReadWriteAccess { ReadOnly, WriteOnly, ReadWrite }

    public class User
    {
        [Key]
        public long Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class Device
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        public int Port { get; set; }
        public ProtocolType ProtocolType { get; set; }
        
        // Serial Port settings (for RTU/Ascii)
        public string PortName { get; set; } = string.Empty;
        public int BaudRate { get; set; } = 9600;
        public int DataBits { get; set; } = 8;
        public int StopBits { get; set; } = 1; // 0=None, 1=One, 2=Two, 3=OnePointFive
        public int Parity { get; set; } = 0; // 0=None, 1=Odd, 2=Even, 3=Mark, 4=Space

        // PLC specific settings
        public string PlcVersion { get; set; } = string.Empty; // e.g. "S7_1200", "Qna_3E"

        /// <summary>When protocol is Custom, must match the plugin assembly's PluginId.</summary>
        public string PluginId { get; set; } = string.Empty;

        /// <summary>Optional JSON consumed by the plugin (connection details, mapping, etc.).</summary>
        public string PluginConfigJson { get; set; } = string.Empty;

        public int PollInterval { get; set; }
        public bool IsActive { get; set; }
        
        [JsonIgnore]
        public ICollection<DeviceVariable> Variables { get; set; } = new List<DeviceVariable>();
    }

    public class DeviceVariable
    {
        [Key]
        public long Id { get; set; }
        public long DeviceId { get; set; }
        public string Address { get; set; } = string.Empty;
        public DataType DataType { get; set; }
        public string Alias { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ReadWriteAccess ReadWrite { get; set; }

        [JsonIgnore]
        public Device? Device { get; set; }
    }

    public class MqttConfig
    {
        [Key]
        public long Id { get; set; }
        public string BrokerIp { get; set; } = string.Empty;
        public int Port { get; set; }
        public string ClientId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string PubTopic { get; set; } = string.Empty;
        public string SubTopic { get; set; } = string.Empty;
    }

    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Device> Devices { get; set; }
        public DbSet<DeviceVariable> DeviceVariables { get; set; }
        public DbSet<MqttConfig> MqttConfigs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<Device>()
                .HasMany(d => d.Variables)
                .WithOne(v => v.Device)
                .HasForeignKey(v => v.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>().HasData(
                new User { Id = 1, Username = "admin", Password = "123" }
            );
        }
    }
}
