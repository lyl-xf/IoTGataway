using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IoTGateway.Models;

namespace IoTGateway.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DeviceController : ControllerBase
    {
        private readonly AppDbContext _context;
        public DeviceController(AppDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> Get() => Ok(await _context.Devices.ToListAsync());

        [HttpPost]
        public async Task<IActionResult> Post(Device device)
        {
            if (await _context.Devices.AnyAsync(d => d.Id == device.Id))
                return BadRequest(new { message = $"设备 ID {device.Id} 已存在" });

            _context.Devices.Add(device);
            await _context.SaveChangesAsync();
            return Ok(device);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(long id, Device device)
        {
            if (id != device.Id) return BadRequest();
            _context.Entry(device).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var device = await _context.Devices.FindAsync(id);
            if (device == null) return NotFound();
            _context.Devices.Remove(device);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class VariableController : ControllerBase
    {
        private readonly AppDbContext _context;
        public VariableController(AppDbContext context) => _context = context;

        [HttpGet("device/{deviceId}")]
        public async Task<IActionResult> GetByDevice(long deviceId) => 
            Ok(await _context.DeviceVariables.Where(v => v.DeviceId == deviceId).ToListAsync());

        [HttpPost]
        public async Task<IActionResult> Post(DeviceVariable variable)
        {
            _context.DeviceVariables.Add(variable);
            await _context.SaveChangesAsync();
            return Ok(variable);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(long id, DeviceVariable variable)
        {
            if (id != variable.Id) return BadRequest();
            _context.Entry(variable).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var variable = await _context.DeviceVariables.FindAsync(id);
            if (variable == null) return NotFound();
            _context.DeviceVariables.Remove(variable);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class MqttController : ControllerBase
    {
        private readonly AppDbContext _context;
        public MqttController(AppDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> Get() => Ok(await _context.MqttConfigs.FirstOrDefaultAsync());

        [HttpPost]
        public async Task<IActionResult> Save(MqttConfig config)
        {
            var existing = await _context.MqttConfigs.FirstOrDefaultAsync();
            if (existing == null) _context.MqttConfigs.Add(config);
            else
            {
                existing.BrokerIp = config.BrokerIp;
                existing.Port = config.Port;
                existing.ClientId = config.ClientId;
                existing.PubTopic = config.PubTopic;
                existing.SubTopic = config.SubTopic;
            }
            await _context.SaveChangesAsync();
            return Ok(config);
        }
    }
}
