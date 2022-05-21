#nullable enable

using System;
using System.Runtime.InteropServices;
using NLog;

namespace WindowSizeGuard;

public interface MonitorSwitcher {

    /// <summary>
    /// Toggle the enabled monitors. This will alternate between the Internal and External monitor (as defined in the stock Win+P Projecting UI). This will enable only one monitor at a time, so the Clone and Extend modes are unused. This probably works poorly with three or more attached monitors, but I haven't tested that yet.
    /// <para>State diagram:</para>
    /// <para>(any other mode) → (only Monitor 1 enabled) ⇄ (only Monitor 2 enabled)</para>
    /// </summary>
    void switchToSingleOtherMonitor();

}

/*
 * Sources:
 * https://stackoverflow.com/a/69096335/979493
 * https://stackoverflow.com/q/16326932/979493
 * https://stackoverflow.com/q/57068497/979493
 * https://github.com/dotnet/pinvoke/tree/main/src/User32
 */
[Component]
public class MonitorSwitcherImpl: MonitorSwitcher {

    private static readonly Logger LOGGER = LogManager.GetLogger(nameof(MonitorSwitcherImpl));

    // struct sizes were determined by calling Marshal.SizeOf() on the structs provided by https://github.com/dotnet/pinvoke/blob/main/src/User32/User32+DISPLAYCONFIG_PATH_INFO.cs and related files
    private const int  PATH_INFO_SIZE_BYTES      = 72;
    private const int  MODE_INFO_SIZE_BYTES      = 64;
    private const uint QDC_DATABASE_CURRENT      = 0x04;
    private const uint ERROR_INSUFFICIENT_BUFFER = 0x7A;

    public void switchToSingleOtherMonitor() {
        uint       pathCount = 0;
        uint       modeCount = 0;
        uint       result;
        TopologyId currentTopologyId;

        do {
            result = GetDisplayConfigBufferSizes(QDC_DATABASE_CURRENT, ref pathCount, ref modeCount);
            if (result != 0) {
                LOGGER.Error("Failed to get display configuration buffer sizes: {0}", result);
                return;
            }

            byte[] paths = new byte[pathCount * PATH_INFO_SIZE_BYTES];
            byte[] modes = new byte[modeCount * MODE_INFO_SIZE_BYTES];

            result = QueryDisplayConfig(QDC_DATABASE_CURRENT, ref pathCount, paths, ref modeCount, modes, out currentTopologyId);
        } while (result == ERROR_INSUFFICIENT_BUFFER);

        if (result != 0) {
            LOGGER.Error("Failed to query display config: {0}", result);
            return;
        }

        //intentionally toggle between just internal and external, since I only use one of my two connected monitors at a time
        SetDisplayConfigFlags nextTopology = currentTopologyId == TopologyId.EXTERNAL ? SetDisplayConfigFlags.TOPOLOGY_INTERNAL : SetDisplayConfigFlags.TOPOLOGY_EXTERNAL;

        LOGGER.Info("Switching display configuration from {0} to {1}", currentTopologyId, nextTopology);
        result = SetDisplayConfig(0, IntPtr.Zero, 0, IntPtr.Zero, nextTopology | SetDisplayConfigFlags.APPLY);
        if (result != 0) {
            LOGGER.Error("Failed to set display config: {0}", result);
        }
    }

    #region Win32

    [DllImport("user32.dll")]
    private static extern uint GetDisplayConfigBufferSizes(uint flags, ref uint pathCount, ref uint modeCount);

    [DllImport("user32.dll")]
    private static extern uint QueryDisplayConfig(uint flags, ref uint pathCount, [Out] byte[] paths, ref uint modeCount, [Out] byte[] modes, out TopologyId currentTopologyId);

    [DllImport("user32.dll")]
    private static extern uint SetDisplayConfig(uint pathCount, IntPtr paths, uint modeCount, IntPtr modes, SetDisplayConfigFlags flags);

    private enum TopologyId: uint {

        INTERNAL = 0x01,
        CLONE    = 0x02,
        EXTEND   = 0x04,
        EXTERNAL = 0x08

    }

    [Flags]
    private enum SetDisplayConfigFlags: uint {

        TOPOLOGY_INTERNAL = 0x01,
        TOPOLOGY_CLONE    = 0x02,
        TOPOLOGY_EXTEND   = 0x04,
        TOPOLOGY_EXTERNAL = 0x08,
        APPLY             = 0x80

    }

    #endregion

}