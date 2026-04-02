namespace IoTGateway.ProtocolSdk;

public readonly struct ProtocolResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }

    public static ProtocolResult Ok() => new() { Success = true };

    public static ProtocolResult Fail(string? error) =>
        new() { Success = false, Error = error ?? "Unknown error" };
}

public readonly struct ProtocolResult<T>
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public T? Value { get; init; }

    public static ProtocolResult<T> Ok(T value) =>
        new() { Success = true, Value = value };

    public static ProtocolResult<T> Fail(string? error) =>
        new() { Success = false, Error = error ?? "Unknown error" };
}
