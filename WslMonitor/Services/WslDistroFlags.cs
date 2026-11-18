namespace WslMonitor.Services;

/// <summary>
/// Reads the <c>Flags</c> value of a distro's registration under HKCU <c>...\Lxss</c>. Kept apart from
/// <see cref="WslRegistry"/> so the rule can be checked without a registry.
/// </summary>
public static class WslDistroFlags
{
    // LXSS_DISTRO_FLAGS_VM_MODE in WSL's wslservice.idl: set when the distro runs as WSL 2.
    private const int VmModeFlag = 0x8;

    /// <summary>
    /// The distro's WSL version, 1 or 2, from its registration's <c>Flags</c> value (pass the raw
    /// registry value). Not from <c>Version</c>: that is the format of a WSL 1 distro's file system,
    /// and reads 2 for current WSL 1 distros too. A registration without a numeric <c>Flags</c> is
    /// taken as WSL 2, WSL's default.
    /// </summary>
    public static int WslVersion(object? flags) =>
        flags is int f && (f & VmModeFlag) == 0 ? 1 : 2;
}
