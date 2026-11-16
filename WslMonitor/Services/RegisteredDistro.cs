namespace WslMonitor.Services;

/// <summary>A distro as WSL registers it for the current user.</summary>
public sealed record RegisteredDistro(
    Guid Id,
    string Name,
    int WslVersion,
    string BasePath,
    string DiskPath,
    string Flavor,
    string OsVersion,
    bool IsDefault,
    string BusyReason);
