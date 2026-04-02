using System.Text.Json;
using IoTGateway.Models;
using IoTGateway.ProtocolSdk;
using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Client;

namespace IoTGateway.Services
{
    public class DataCollectionWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ProtocolSessionFactory _sessionFactory;
        private IMqttClient? _mqttClient;
        private readonly Dictionary<long, DateTime> _lastPollTimes = new();
        private DateTime _lastDbCheck = DateTime.MinValue;
        private MqttConfig? _cachedMqttConfig;
        private List<Device> _cachedDevices = new();

        // Shared client pool:
        // - deviceId -> configKey
        // - configKey -> client (shared by same protocol/ip/port/serial/plc settings)
        private readonly Dictionary<long, string> _deviceClientRefs = new();
        private readonly Dictionary<string, IDeviceProtocolSession> _sharedSessions = new();
        private readonly Dictionary<string, int> _sharedClientRefCounts = new();
        private readonly object _clientPoolLock = new();
        private readonly Dictionary<long, DateTime> _errorBackoff = new();

        public DataCollectionWorker(IServiceProvider serviceProvider, ProtocolSessionFactory sessionFactory)
        {
            _serviceProvider = serviceProvider;
            _sessionFactory = sessionFactory;
        }

        private string GetDeviceConfigHash(Device device)
        {
            return $"{device.ProtocolType}_{device.Ip}_{device.Port}_{device.PortName}_{device.BaudRate}_{device.DataBits}_{device.StopBits}_{device.Parity}_{device.PlcVersion}_{device.PluginId}_{device.PluginConfigJson}";
        }

        private IDeviceProtocolSession AcquireSessionForDevice(Device device)
        {
            var configKey = GetDeviceConfigHash(device);

            lock (_clientPoolLock)
            {
                if (_deviceClientRefs.TryGetValue(device.Id, out var oldKey) && oldKey != configKey)
                {
                    ReleaseClientByKeyNoLock(oldKey);
                    _deviceClientRefs.Remove(device.Id);
                }

                if (!_deviceClientRefs.TryGetValue(device.Id, out var currentKey))
                {
                    if (!_sharedSessions.ContainsKey(configKey))
                    {
                        _sharedSessions[configKey] = _sessionFactory.CreateSession(device);
                        _sharedClientRefCounts[configKey] = 0;
                    }

                    _sharedClientRefCounts[configKey]++;
                    _deviceClientRefs[device.Id] = configKey;
                    currentKey = configKey;
                }

                return _sharedSessions[currentKey];
            }
        }

        private void ReleaseClientByDeviceId(long deviceId)
        {
            lock (_clientPoolLock)
            {
                if (_deviceClientRefs.TryGetValue(deviceId, out var key))
                {
                    _deviceClientRefs.Remove(deviceId);
                    ReleaseClientByKeyNoLock(key);
                }
            }
        }

        private void ReleaseClientByKeyNoLock(string key)
        {
            if (!_sharedClientRefCounts.TryGetValue(key, out var refCount))
                return;

            refCount--;
            if (refCount > 0)
            {
                _sharedClientRefCounts[key] = refCount;
                return;
            }

            _sharedClientRefCounts.Remove(key);
            if (_sharedSessions.TryGetValue(key, out var session))
            {
                try { session.Close(); } catch { /* ignore */ }
                try { session.Dispose(); } catch { /* ignore */ }
                _sharedSessions.Remove(key);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            lock (_clientPoolLock)
            {
                foreach (var session in _sharedSessions.Values)
                {
                    try { session.Close(); } catch { /* ignore */ }
                    try { session.Dispose(); } catch { /* ignore */ }
                }
                _sharedSessions.Clear();
                _sharedClientRefCounts.Clear();
                _deviceClientRefs.Clear();
            }

            await base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.UtcNow;

                    // Refresh configuration from database every 15 seconds
                    if ((now - _lastDbCheck).TotalSeconds >= 15)
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        
                        _cachedMqttConfig = await db.MqttConfigs.AsNoTracking().FirstOrDefaultAsync(stoppingToken);
                        _cachedDevices = await db.Devices.Include(d => d.Variables).Where(d => d.IsActive).AsNoTracking().ToListAsync(stoppingToken);
                        _lastDbCheck = now;

                        // Cleanup inactive or deleted devices from cache
                        var activeDeviceIds = _cachedDevices.Select(d => d.Id).ToHashSet();
                        var toRemove = _deviceClientRefs.Keys.Where(id => !activeDeviceIds.Contains(id)).ToList();
                        foreach (var id in toRemove)
                        {
                            ReleaseClientByDeviceId(id);
                            _lastPollTimes.Remove(id);
                        }
                    }

                    if (_cachedMqttConfig != null && _cachedDevices.Any())
                    {
                        await EnsureMqttConnectedAsync(_cachedMqttConfig, stoppingToken);

                        foreach (var device in _cachedDevices)
                        {
                            var interval = device.PollInterval > 0 ? device.PollInterval : 1000; // Default to 1000ms if not set or invalid
                            
                            if (_errorBackoff.TryGetValue(device.Id, out var backoffTime) && now < backoffTime)
                            {
                                continue; // Skip polling, device is in error backoff
                            }

                            if (_lastPollTimes.TryGetValue(device.Id, out var lastPollTime))
                            {
                                if ((now - lastPollTime).TotalMilliseconds < interval)
                                {
                                    continue; // Skip polling, interval hasn't elapsed
                                }
                            }

                            _lastPollTimes[device.Id] = now;

                            var session = AcquireSessionForDevice(device);

                            var payload = new Dictionary<string, object>();
                            
                            lock (session)
                            {
                                try
                                {
                                    if (!session.IsConnected)
                                    {
                                        var openResult = session.Open();
                                        if (!openResult.Success)
                                            Console.WriteLine($"[Device {device.Id}] Failed to connect: {openResult.Error}");
                                    }
                                    
                                    if (session.IsConnected)
                                    {
                                        _errorBackoff.Remove(device.Id);
                                        foreach (var variable in device.Variables)
                                        {
                                            object? val = null;
                                            try 
                                            {
                                                var read = session.Read(variable.Address, (ProtocolDataType)(int)variable.DataType);
                                                if (read.Success)
                                                    val = read.Value;
                                                else
                                                    Console.WriteLine($"[Device {device.Id}] Failed to read {variable.Alias} ({variable.Address}): {read.Error}");
                                            } 
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"[Device {device.Id}] Exception reading {variable.Alias} ({variable.Address}): {ex.Message}");
                                            }
                                            
                                            if (val != null) payload[variable.Alias] = val;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[Device {device.Id}] Error: {ex.Message}");
                                    try { session.Close(); } catch { /* ignore */ }
                                    _errorBackoff[device.Id] = now.AddSeconds(15);
                                }
                            }

                            if (payload.Any() && _mqttClient != null && _mqttClient.IsConnected)
                            {
                                var json = JsonSerializer.Serialize(new
                                {
                                    deviceId = device.Id,
                                    timestamp = DateTime.UtcNow,
                                    data = payload
                                });
                                
                                var message = new MqttApplicationMessageBuilder()
                                    .WithTopic(_cachedMqttConfig.PubTopic)
                                    .WithPayload(json)
                                    .Build();
                                    
                                await _mqttClient.PublishAsync(message, stoppingToken);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Worker] Error: {ex.Message}");
                }

                await Task.Delay(100, stoppingToken); // Check frequently to support fine-grained poll intervals
            }
        }

        private async Task EnsureMqttConnectedAsync(MqttConfig config, CancellationToken ct)
        {
            if (_mqttClient == null) 
            {
                _mqttClient = new MqttFactory().CreateMqttClient();
                _mqttClient.ApplicationMessageReceivedAsync += HandleMqttMessageAsync;
            }

            if (!_mqttClient.IsConnected)
            {
                try
                {
                    var optionsBuilder = new MqttClientOptionsBuilder()
                        .WithTcpServer(config.BrokerIp, config.Port)
                        .WithClientId(config.ClientId);

                    if (!string.IsNullOrEmpty(config.Username))
                    {
                        optionsBuilder.WithCredentials(config.Username, config.Password);
                    }

                    var options = optionsBuilder.Build();
                    await _mqttClient.ConnectAsync(options, ct);

                    if (!string.IsNullOrEmpty(config.SubTopic))
                    {
                        await _mqttClient.SubscribeAsync(config.SubTopic, cancellationToken: ct);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MQTT] Connection failed: {ex.Message}");
                }
            }
        }

        private async Task HandleMqttMessageAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            try
            {
                var payload = e.ApplicationMessage.ConvertPayloadToString();
                Console.WriteLine($"[MQTT] Received command: {payload}");
                
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var command = JsonSerializer.Deserialize<MqttCommand>(payload, options);

                if (command != null && command.DeviceId > 0)
                {
                    var device = _cachedDevices.FirstOrDefault(d => d.Id == command.DeviceId);

                    if (device != null && device.IsActive)
                    {
                        var session = AcquireSessionForDevice(device);

                        MqttApplicationMessage? messageToPublish = null;

                        lock (session)
                        {
                            try
                            {
                                if (!session.IsConnected)
                                {
                                    var openResult = session.Open();
                                    if (!openResult.Success)
                                        Console.WriteLine($"[MQTT] Failed to connect to device {device.Id}: {openResult.Error}");
                                }

                                if (session.IsConnected)
                                {
                                    if (command.Action?.ToLower() == "write" && command.Writes != null)
                                    {
                                        foreach (var write in command.Writes)
                                        {
                                            var variable = device.Variables.FirstOrDefault(v => v.Alias == write.Key);
                                            if (variable != null && variable.ReadWrite != ReadWriteAccess.ReadOnly)
                                            {
                                                try
                                                {
                                                    var strVal = write.Value?.ToString();
                                                    if (strVal != null)
                                                    {
                                                        Console.WriteLine($"[MQTT] Writing to {variable.Alias} ({variable.Address}): {strVal}");
                                                        var wr = session.Write(variable.Address, (ProtocolDataType)(int)variable.DataType, strVal);
                                                        if (!wr.Success)
                                                            Console.WriteLine($"[MQTT] Write failed for {variable.Alias}: {wr.Error}");
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    Console.WriteLine($"[MQTT] Write error for {variable.Alias}: {ex.Message}");
                                                }
                                            }
                                            else
                                            {
                                                Console.WriteLine($"[MQTT] Variable {write.Key} not found or is read-only.");
                                            }
                                        }
                                    }

                                    if (command.Action?.ToLower() == "query" || command.Action?.ToLower() == "write")
                                    {
                                        var resultPayload = new Dictionary<string, object>();

                                        var variablesToQuery = (command.Reads != null && command.Reads.Any())
                                            ? device.Variables.Where(v => command.Reads.Contains(v.Alias))
                                            : device.Variables;

                                        foreach (var variable in variablesToQuery)
                                        {
                                            object? val = null;
                                            try
                                            {
                                                var read = session.Read(variable.Address, (ProtocolDataType)(int)variable.DataType);
                                                if (read.Success)
                                                    val = read.Value;
                                                else
                                                    Console.WriteLine($"[MQTT] Failed to read {variable.Alias} ({variable.Address}): {read.Error}");
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"[MQTT] Exception reading {variable.Alias} ({variable.Address}): {ex.Message}");
                                            }

                                            if (val != null) resultPayload[variable.Alias] = val;
                                        }

                                        if (resultPayload.Any() && _mqttClient != null && _mqttClient.IsConnected)
                                        {
                                            if (_cachedMqttConfig != null && !string.IsNullOrEmpty(_cachedMqttConfig.PubTopic))
                                            {
                                                var json = JsonSerializer.Serialize(new
                                                {
                                                    deviceId = device.Id,
                                                    timestamp = DateTime.UtcNow,
                                                    data = resultPayload,
                                                    replyTo = true
                                                });

                                                messageToPublish = new MqttApplicationMessageBuilder()
                                                    .WithTopic(_cachedMqttConfig.PubTopic)
                                                    .WithPayload(json)
                                                    .Build();
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[MQTT] Device {device.Id} error: {ex.Message}");
                                try { session.Close(); } catch { /* ignore */ }
                            }
                        }

                        if (messageToPublish != null)
                        {
                            await _mqttClient.PublishAsync(messageToPublish);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Log error
            }
        }

        private class MqttCommand
        {
            public string? MessageId { get; set; }
            public long DeviceId { get; set; }
            public string Action { get; set; } = string.Empty; // "query" or "write"
            public Dictionary<string, object>? Writes { get; set; }
            public List<string>? Reads { get; set; } // List of variable aliases to read
        }
    }
}
