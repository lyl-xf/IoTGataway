using IoTGateway.Models;
using IoTGateway.ProtocolSdk;
using IoTGateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IoTGateway.Controllers;

[Authorize]
[ApiController]
[Route("api/debug")]
public class DeviceDebugController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ProtocolSessionFactory _sessionFactory;

    public DeviceDebugController(AppDbContext context, ProtocolSessionFactory sessionFactory)
    {
        _context = context;
        _sessionFactory = sessionFactory;
    }

    public class DebugRequest
    {
        public long DeviceId { get; set; }
        public string Address { get; set; } = string.Empty;
        public DataType DataType { get; set; }
        public string? WriteValue { get; set; }
    }

    [HttpPost("read")]
    public async Task<IActionResult> Read([FromBody] DebugRequest req)
    {
        var device = await _context.Devices.FindAsync(req.DeviceId);
        if (device == null) return NotFound("设备不存在");

        using var session = _sessionFactory.CreateSession(device);
        var openResult = session.Open();
        if (!openResult.Success)
            return StatusCode(500, $"设备连接失败: {openResult.Error}");

        try
        {
            var read = session.Read(req.Address, (ProtocolDataType)(int)req.DataType);
            if (!read.Success)
                return BadRequest(new { Success = false, Error = read.Error });

            return Ok(new { Value = read.Value });
        }
        finally
        {
            session.Close();
        }
    }

    [HttpPost("write")]
    public async Task<IActionResult> Write([FromBody] DebugRequest req)
    {
        var device = await _context.Devices.FindAsync(req.DeviceId);
        if (device == null) return NotFound("设备不存在");

        using var session = _sessionFactory.CreateSession(device);
        var openResult = session.Open();
        if (!openResult.Success)
            return StatusCode(500, $"设备连接失败: {openResult.Error}");

        try
        {
            var wr = session.Write(req.Address, (ProtocolDataType)(int)req.DataType, req.WriteValue);
            if (!wr.Success)
                return BadRequest(new { Success = false, Error = wr.Error });

            return Ok(new { Success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Success = false, Error = ex.Message });
        }
        finally
        {
            session.Close();
        }
    }
}
