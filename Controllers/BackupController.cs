using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using IoTGateway.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IoTGateway.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class BackupController : ControllerBase
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly AppDbContext _context;
        public BackupController(AppDbContext context) => _context = context;

        [HttpGet("export")]
        public async Task<IActionResult> Export([FromQuery] bool includeMqtt = true, [FromQuery] bool download = false)
        {
            var devices = await _context.Devices.AsNoTracking().Include(d => d.Variables).OrderBy(d => d.Id).ToListAsync();
            var dto = new GatewayBackupDto
            {
                SchemaVersion = 2,
                ExportedAtUtc = DateTime.UtcNow,
                Devices = devices.Select(d => new DeviceBackupDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    Ip = d.Ip,
                    Port = d.Port,
                    ProtocolType = d.ProtocolType,
                    PortName = d.PortName,
                    BaudRate = d.BaudRate,
                    DataBits = d.DataBits,
                    StopBits = d.StopBits,
                    Parity = d.Parity,
                    PlcVersion = d.PlcVersion,
                    PluginId = d.PluginId,
                    PluginConfigJson = d.PluginConfigJson,
                    PollInterval = d.PollInterval,
                    IsActive = d.IsActive,
                    Variables = d.Variables.OrderBy(v => v.Id).Select(v => new DeviceVariableBackupDto
                    {
                        Id = v.Id,
                        DeviceId = v.DeviceId,
                        Address = v.Address,
                        DataType = v.DataType,
                        Alias = v.Alias,
                        Description = v.Description,
                        ReadWrite = v.ReadWrite
                    }).ToList()
                }).ToList()
            };

            if (includeMqtt)
            {
                var mqtt = await _context.MqttConfigs.AsNoTracking().FirstOrDefaultAsync();
                if (mqtt != null)
                {
                    dto.Mqtt = new MqttBackupDto
                    {
                        BrokerIp = mqtt.BrokerIp,
                        Port = mqtt.Port,
                        ClientId = mqtt.ClientId,
                        Username = mqtt.Username,
                        Password = mqtt.Password,
                        PubTopic = mqtt.PubTopic,
                        SubTopic = mqtt.SubTopic
                    };
                }
            }

            var json = JsonSerializer.Serialize(dto, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            if (download)
            {
                var name = $"iot-gateway-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}Z.json";
                return File(bytes, "application/json; charset=utf-8", name);
            }
            return Content(json, "application/json; charset=utf-8");
        }

        [HttpPost("import")]
        public async Task<IActionResult> Import([FromBody] JsonElement body, [FromQuery] string mode = "merge", [FromQuery] bool includeMqtt = true, [FromQuery] bool removeMissingVariables = false)
        {
            GatewayBackupDto? dto;
            try
            {
                dto = body.Deserialize<GatewayBackupDto>(new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                });
            }
            catch (JsonException ex)
            {
                return BadRequest(new
                {
                    message = "备份 JSON 解析失败",
                    detail = $"Path: {ex.Path ?? "(unknown)"}, Error: {ex.Message}"
                });
            }

            if (dto == null || dto.Devices == null) return BadRequest(new { message = "无效的 JSON 或缺少 devices" });
            if (dto.SchemaVersion < 1) return BadRequest(new { message = "不支持的 schemaVersion" });

            var merge = !string.Equals(mode, "replaceAll", StringComparison.OrdinalIgnoreCase);
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var duplicateDeviceIds = dto.Devices
                    .GroupBy(d => d.Id)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToArray();
                if (duplicateDeviceIds.Length > 0)
                {
                    return BadRequest(new { message = "导入数据包含重复设备 ID", detail = string.Join(", ", duplicateDeviceIds) });
                }

                foreach (var d in dto.Devices)
                {
                    var duplicateKeys = d.Variables
                        .GroupBy(v => string.IsNullOrWhiteSpace(v.Alias) ? $"\0{v.Address}" : v.Alias)
                        .Where(g => g.Count() > 1)
                        .Select(g => g.Key)
                        .ToArray();

                    if (duplicateKeys.Length > 0)
                    {
                        return BadRequest(new
                        {
                            message = $"设备 {d.Id} 的变量存在重复键（Alias 或 Address）",
                            detail = string.Join(", ", duplicateKeys)
                        });
                    }
                }

                if (merge) await ImportMergeAsync(dto, includeMqtt, removeMissingVariables);
                else await ImportReplaceAllAsync(dto, includeMqtt);

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                var dbEx = ex as DbUpdateException;
                var leaf = ex;
                while (leaf.InnerException != null) leaf = leaf.InnerException;
                var detail = dbEx?.InnerException?.Message ?? leaf.Message ?? ex.Message;
                return StatusCode(500, new { message = "导入失败", detail });
            }
            return Ok(new { message = merge ? "合并导入成功" : "全量替换导入成功" });
        }

        private async Task ImportMergeAsync(GatewayBackupDto body, bool includeMqtt, bool removeMissingVariables)
        {
            foreach (var dDto in body.Devices)
            {
                var device = await _context.Devices.FirstOrDefaultAsync(x => x.Id == dDto.Id);
                if (device == null)
                {
                    device = new Device { Id = dDto.Id };
                    _context.Devices.Add(device);
                }

                device.Name = dDto.Name;
                device.Ip = dDto.Ip;
                device.Port = dDto.Port;
                device.ProtocolType = dDto.ProtocolType;
                device.PortName = dDto.PortName;
                device.BaudRate = dDto.BaudRate;
                device.DataBits = dDto.DataBits;
                device.StopBits = dDto.StopBits;
                device.Parity = dDto.Parity;
                device.PlcVersion = dDto.PlcVersion;
                device.PluginId = dDto.PluginId ?? "";
                device.PluginConfigJson = dDto.PluginConfigJson ?? "";
                device.PollInterval = dDto.PollInterval;
                device.IsActive = dDto.IsActive;

                var keysInBackup = new HashSet<string>(StringComparer.Ordinal);
                foreach (var vDto in dDto.Variables)
                {
                    var stableKey = string.IsNullOrEmpty(vDto.Alias) ? $"\0{vDto.Address}" : vDto.Alias;
                    keysInBackup.Add(stableKey);

                    DeviceVariable? variable = null;
                    if (!string.IsNullOrEmpty(vDto.Alias))
                    {
                        variable = await _context.DeviceVariables.FirstOrDefaultAsync(v => v.DeviceId == dDto.Id && v.Alias == vDto.Alias);
                    }
                    else
                    {
                        variable = await _context.DeviceVariables.FirstOrDefaultAsync(v => v.DeviceId == dDto.Id && v.Address == vDto.Address);
                    }

                    if (variable == null)
                    {
                        variable = new DeviceVariable { DeviceId = dDto.Id };
                        _context.DeviceVariables.Add(variable);
                    }

                    variable.DeviceId = dDto.Id;
                    variable.Address = vDto.Address;
                    variable.DataType = vDto.DataType;
                    variable.Alias = vDto.Alias;
                    variable.Description = vDto.Description;
                    variable.ReadWrite = vDto.ReadWrite;
                }

                if (removeMissingVariables)
                {
                    var existing = await _context.DeviceVariables.Where(v => v.DeviceId == dDto.Id).ToListAsync();
                    foreach (var v in existing)
                    {
                        var key = string.IsNullOrEmpty(v.Alias) ? $"\0{v.Address}" : v.Alias;
                        if (!keysInBackup.Contains(key)) _context.DeviceVariables.Remove(v);
                    }
                }
            }

            if (includeMqtt && body.Mqtt != null) await ApplyMqttAsync(body.Mqtt);
        }

        private async Task ImportReplaceAllAsync(GatewayBackupDto body, bool includeMqtt)
        {
            var oldDevices = await _context.Devices.ToListAsync();
            _context.Devices.RemoveRange(oldDevices);
            await _context.SaveChangesAsync();

            foreach (var dDto in body.Devices)
            {
                _context.Devices.Add(new Device
                {
                    Id = dDto.Id,
                    Name = dDto.Name,
                    Ip = dDto.Ip,
                    Port = dDto.Port,
                    ProtocolType = dDto.ProtocolType,
                    PortName = dDto.PortName,
                    BaudRate = dDto.BaudRate,
                    DataBits = dDto.DataBits,
                    StopBits = dDto.StopBits,
                    Parity = dDto.Parity,
                    PlcVersion = dDto.PlcVersion,
                    PluginId = dDto.PluginId ?? "",
                    PluginConfigJson = dDto.PluginConfigJson ?? "",
                    PollInterval = dDto.PollInterval,
                    IsActive = dDto.IsActive
                });

                foreach (var vDto in dDto.Variables)
                {
                    _context.DeviceVariables.Add(new DeviceVariable
                    {
                        DeviceId = dDto.Id,
                        Address = vDto.Address,
                        DataType = vDto.DataType,
                        Alias = vDto.Alias,
                        Description = vDto.Description,
                        ReadWrite = vDto.ReadWrite
                    });
                }
            }

            if (includeMqtt && body.Mqtt != null) await ApplyMqttAsync(body.Mqtt);
        }

        private async Task ApplyMqttAsync(MqttBackupDto m)
        {
            var existing = await _context.MqttConfigs.FirstOrDefaultAsync();
            if (existing == null)
            {
                _context.MqttConfigs.Add(new MqttConfig
                {
                    BrokerIp = m.BrokerIp,
                    Port = m.Port,
                    ClientId = m.ClientId,
                    Username = m.Username,
                    Password = m.Password,
                    PubTopic = m.PubTopic,
                    SubTopic = m.SubTopic
                });
            }
            else
            {
                existing.BrokerIp = m.BrokerIp;
                existing.Port = m.Port;
                existing.ClientId = m.ClientId;
                existing.Username = m.Username;
                existing.Password = m.Password;
                existing.PubTopic = m.PubTopic;
                existing.SubTopic = m.SubTopic;
            }
        }
    }
}
