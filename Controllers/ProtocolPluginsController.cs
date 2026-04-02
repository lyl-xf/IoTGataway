using IoTGateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTGateway.Controllers;

[Authorize]
[ApiController]
[Route("api/protocol-plugins")]
public class ProtocolPluginsController : ControllerBase
{
    private readonly IProtocolPluginHost _host;

    public ProtocolPluginsController(IProtocolPluginHost host) => _host = host;

    /// <summary>已加载的协议插件列表（用于前端下拉框）。</summary>
    [HttpGet]
    public IActionResult Get() =>
        Ok(_host.LoadedPlugins.Select(p => new { pluginId = p.PluginId, displayName = p.DisplayName }).ToList());
}
