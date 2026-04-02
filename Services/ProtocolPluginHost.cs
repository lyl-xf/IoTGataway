using System.Reflection;
using IoTGateway.ProtocolSdk;
using Microsoft.Extensions.Hosting;

namespace IoTGateway.Services;

public interface IProtocolPluginHost
{
    IReadOnlyList<ProtocolPluginDescriptor> LoadedPlugins { get; }

    bool TryGetPlugin(string pluginId, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IProtocolPlugin? plugin);
}

public sealed record ProtocolPluginDescriptor(string PluginId, string DisplayName, string SourcePath);

public sealed class ProtocolPluginHost : IProtocolPluginHost
{
    private readonly Dictionary<string, IProtocolPlugin> _plugins = new(StringComparer.Ordinal);
    private readonly List<ProtocolPluginDescriptor> _descriptors = new();
    private readonly ILogger<ProtocolPluginHost> _logger;

    public ProtocolPluginHost(IHostEnvironment env, ILogger<ProtocolPluginHost> logger)
    {
        _logger = logger;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        LoadFromDirectory(Path.Combine(env.ContentRootPath, "plugins"), seen);
        var baseDirPlugins = Path.Combine(AppContext.BaseDirectory, "plugins");
        if (!string.Equals(Path.GetFullPath(baseDirPlugins), Path.GetFullPath(Path.Combine(env.ContentRootPath, "plugins")), StringComparison.OrdinalIgnoreCase))
            LoadFromDirectory(baseDirPlugins, seen);
    }

    private void LoadFromDirectory(string directory, HashSet<string> seenFiles)
    {
        if (!Directory.Exists(directory))
            return;

        foreach (var dll in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(dll);
            if (name.Equals("IoTGateway.ProtocolSdk.dll", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!seenFiles.Add(Path.GetFullPath(dll)))
                continue;

            try
            {
                var asm = Assembly.LoadFrom(dll);
                Type[] types;
                try
                {
                    types = asm.GetExportedTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).Cast<Type>().ToArray();
                }

                foreach (var t in types)
                {
                    if (t is not { IsClass: true, IsAbstract: false } || t.GetConstructor(Type.EmptyTypes) == null)
                        continue;
                    if (!typeof(IProtocolPlugin).IsAssignableFrom(t))
                        continue;
                    if (Activator.CreateInstance(t) is not IProtocolPlugin plugin)
                        continue;

                    var id = plugin.PluginId?.Trim() ?? "";
                    if (string.IsNullOrEmpty(id))
                    {
                        _logger.LogWarning("Plugin type {Type} in {Dll} has empty PluginId, skipped", t.FullName, dll);
                        continue;
                    }

                    if (_plugins.ContainsKey(id))
                    {
                        _logger.LogWarning("Duplicate plugin id {Id} in {Dll}, skipped", id, dll);
                        continue;
                    }

                    _plugins[id] = plugin;
                    _descriptors.Add(new ProtocolPluginDescriptor(id, plugin.DisplayName, dll));
                    _logger.LogInformation("Loaded protocol plugin {Id} ({Name}) from {Dll}", id, plugin.DisplayName, dll);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load plugin assembly {Dll}", dll);
            }
        }
    }

    public IReadOnlyList<ProtocolPluginDescriptor> LoadedPlugins => _descriptors;

    public bool TryGetPlugin(string pluginId, out IProtocolPlugin? plugin) =>
        _plugins.TryGetValue(pluginId.Trim(), out plugin);
}
