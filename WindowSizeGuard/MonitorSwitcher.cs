using System;
using System.Runtime.InteropServices;
using ManagedWinapi.Windows;
using NLog;

// ReSharper disable MemberCanBePrivate.Local
#pragma warning disable CS0649

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

    public void switchToSingleOtherMonitor() {
        uint pathCount = 0;
        uint modeCount = 0;

        uint result = GetDisplayConfigBufferSizes(QDC_DATABASE_CURRENT, ref pathCount, ref modeCount);
        if (result != 0) {
            LOGGER.Error("Failed to get display configuration buffer sizes: {0}", result);
            return;
        }

        var paths = new PathInfo[pathCount];
        var modes = new ModeInfo[modeCount];

        result = QueryDisplayConfig(QDC_DATABASE_CURRENT, ref pathCount, paths, ref modeCount, modes, out TopologyId currentTopologyId);
        if (result != 0) {
            LOGGER.Error("Failed to query display config: {0}", result);
            return;
        }

        SetDisplayConfigFlags nextTopology = currentTopologyId switch {
            TopologyId.INTERNAL => SetDisplayConfigFlags.TOPOLOGY_EXTERNAL, //intentionally toggle between just internal and external, since I only use one of my two connected monitors at a time
            TopologyId.CLONE    => SetDisplayConfigFlags.TOPOLOGY_INTERNAL,
            TopologyId.EXTEND   => SetDisplayConfigFlags.TOPOLOGY_INTERNAL,
            TopologyId.EXTERNAL => SetDisplayConfigFlags.TOPOLOGY_INTERNAL,
            _                   => throw new ArgumentOutOfRangeException()
        };

        LOGGER.Info("Switching display configuration from {0} to {1}", currentTopologyId, nextTopology);

        result = SetDisplayConfig(0, IntPtr.Zero, 0, IntPtr.Zero, nextTopology | SetDisplayConfigFlags.APPLY);
        if (result != 0) {
            LOGGER.Error("Failed to set display config: {0}", result);
        }
    }

    #region Win32

    private const uint QDC_DATABASE_CURRENT = 0x04;

    [DllImport("user32.dll")]
    private static extern uint GetDisplayConfigBufferSizes(uint flags, ref uint pathCount, ref uint modeCount);

    [DllImport("user32.dll")]
    private static extern uint QueryDisplayConfig(uint flags, ref uint pathCount, [Out] PathInfo[] paths, ref uint modeCount, [Out] ModeInfo[] modes, out TopologyId currentTopologyId);

    [DllImport("user32.dll")]
    private static extern uint SetDisplayConfig(uint pathCount, IntPtr paths, uint modeCount, IntPtr modes, SetDisplayConfigFlags flags);

    private enum TopologyId: uint {

        INTERNAL = 0x1,
        CLONE    = 0x2,
        EXTEND   = 0x4,
        EXTERNAL = 0x8

    }

    [Flags]
    private enum SetDisplayConfigFlags: uint {

        TOPOLOGY_INTERNAL = 0x00000001,
        TOPOLOGY_CLONE    = 0x00000002,
        TOPOLOGY_EXTEND   = 0x00000004,
        TOPOLOGY_EXTERNAL = 0x00000008,
        APPLY             = 0x00000080

    }

    private struct PathInfo {

        public PathSourceInfo sourceInfo;
        public PathTargetInfo targetInfo;
        public uint           flags;

    }

    [StructLayout(LayoutKind.Explicit)]
    private struct ModeInfo {

        [FieldOffset(0)]  public readonly ModeInfoType     infoType;
        [FieldOffset(4)]  public readonly uint             id;
        [FieldOffset(8)]  public readonly Luid             adapterId;
        [FieldOffset(16)] public readonly TargetMode       targetMode;
        [FieldOffset(16)] public readonly SourceMode       sourceMode;
        [FieldOffset(16)] public readonly DesktopImageInfo desktopImageInfo;

    }

    private enum ModeInfoType {

        DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE        = 1,
        DISPLAYCONFIG_MODE_INFO_TYPE_TARGET        = 2,
        DISPLAYCONFIG_MODE_INFO_TYPE_DESKTOP_IMAGE = 3,
        DISPLAYCONFIG_MODE_INFO_TYPE_FORCE_UINT32  = -1,

    }

    [Flags]
    private enum PathTargetInfoFlags {

        NONE                                            = 0x0,
        DISPLAYCONFIG_TARGET_IN_USE                     = 0x00000001,
        DISPLAYCONFIG_TARGET_FORCIBLE                   = 0x00000002,
        DISPLAYCONFIG_TARGET_FORCED_AVAILABILITY_BOOT   = 0x00000004,
        DISPLAYCONFIG_TARGET_FORCED_AVAILABILITY_PATH   = 0x00000008,
        DISPLAYCONFIG_TARGET_FORCED_AVAILABILITY_SYSTEM = 0x00000010,

    }

    private struct PathTargetInfo {

        public Luid                  adapterId;
        public uint                  id;
        public TargetModeInfo        targetModeInfo;
        public VideoOutputTechnology outputTechnology;
        public Rotation              rotation;
        public Scaling               scaling;
        public Rational              refreshRate;
        public ScanlineOrdering      scanLineOrdering;

        [MarshalAs(UnmanagedType.Bool)]
        public bool targetAvailable;

        public PathTargetInfoFlags statusFlags;

    }

    [Flags]
    private enum PathSourceInfoFlags {

        NONE = 0,

        /// <summary>
        /// This source is in use by at least one active path.
        /// </summary>
        DISPLAYCONFIG_SOURCE_IN_USE = 0x00000001,

    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PathSourceInfo {

        [FieldOffset(0)]  public readonly Luid                adapterId;
        [FieldOffset(8)]  public readonly uint                id;
        [FieldOffset(12)] public readonly uint                modeInfoIdx;
        [FieldOffset(12)] public readonly ushort              cloneGroupId;
        [FieldOffset(14)] public readonly ushort              sourceModeInfoIdx;
        [FieldOffset(16)] public readonly PathSourceInfoFlags statusFlags;

    }

    private struct Luid {

        public uint lowPart;
        public int  highPart;

    }

    private struct SourceMode {

        public uint        width;
        public uint        height;
        public PixelFormat pixelFormat;
        public POINT       position;

    }

    private struct TargetMode {

        public DisplayconfigVideoSignalInfo targetVideoSignalInfo;

    }

    private struct DesktopImageInfo {

        public POINT pathSourceSize;
        public RECT  desktopImageRegion;
        public RECT  desktopImageClip;

    }

    private struct TargetModeInfo {

        private uint bitvector;

        public uint desktopModeInfoIdx {
            get => bitvector & 0xFFFF;
            set => bitvector = value | bitvector;
        }

        public uint targetModeInfoIdx {
            get => (bitvector & 0xFFFF0000) / 0x10000;
            set => bitvector = (value * 0x10000) | bitvector;
        }

    }

    private enum VideoOutputTechnology {

        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_OTHER                = -1,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_HD15                 = 0,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_SVIDEO               = 1,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_COMPOSITE_VIDEO      = 2,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_COMPONENT_VIDEO      = 3,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DVI                  = 4,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_HDMI                 = 5,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_LVDS                 = 6,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_D_JPN                = 8,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_SDI                  = 9,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EXTERNAL = 10,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EMBEDDED = 11,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_UDI_EXTERNAL         = 12,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_UDI_EMBEDDED         = 13,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_SDTVDONGLE           = 14,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_MIRACAST             = 15,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL             = -2147483648,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_FORCE_UINT32         = -1,

    }

    [StructLayout(LayoutKind.Explicit)]
    private struct DisplayconfigVideoSignalInfo {

        [FieldOffset(0)]  public readonly ulong                pixelRate;
        [FieldOffset(8)]  public readonly Rational             hSyncFreq;
        [FieldOffset(16)] public readonly Rational             vSyncFreq;
        [FieldOffset(24)] public readonly _2DRegion            activeSize;
        [FieldOffset(32)] public readonly _2DRegion            totalSize;
        [FieldOffset(40)] public readonly AdditionalSignalInfo AdditionalSignalInfo;
        [FieldOffset(40)] public readonly uint                 videoStandard;
        [FieldOffset(44)] public readonly ScanlineOrdering     scanLineOrdering;

    }

    private enum Rotation {

        DISPLAYCONFIG_ROTATION_IDENTITY     = 1,
        DISPLAYCONFIG_ROTATION_ROTATE90     = 2,
        DISPLAYCONFIG_ROTATION_ROTATE180    = 3,
        DISPLAYCONFIG_ROTATION_ROTATE270    = 4,
        DISPLAYCONFIG_ROTATION_FORCE_UINT32 = -1,

    }

    private enum Scaling {

        DISPLAYCONFIG_SCALING_IDENTITY               = 1,
        DISPLAYCONFIG_SCALING_CENTERED               = 2,
        DISPLAYCONFIG_SCALING_STRETCHED              = 3,
        DISPLAYCONFIG_SCALING_ASPECTRATIOCENTEREDMAX = 4,
        DISPLAYCONFIG_SCALING_CUSTOM                 = 5,
        DISPLAYCONFIG_SCALING_PREFERRED              = 128,
        DISPLAYCONFIG_SCALING_FORCE_UINT32           = -1,

    }

    private struct Rational {

        public uint numerator;
        public uint denominator;

    }

    private enum ScanlineOrdering {

        DISPLAYCONFIG_SCANLINE_ORDERING_UNSPECIFIED                = 0,
        DISPLAYCONFIG_SCANLINE_ORDERING_PROGRESSIVE                = 1,
        DISPLAYCONFIG_SCANLINE_ORDERING_INTERLACED                 = 2,
        DISPLAYCONFIG_SCANLINE_ORDERING_INTERLACED_UPPERFIELDFIRST = DISPLAYCONFIG_SCANLINE_ORDERING_INTERLACED,
        DISPLAYCONFIG_SCANLINE_ORDERING_INTERLACED_LOWERFIELDFIRST = 3,
        DISPLAYCONFIG_SCANLINE_ORDERING_FORCE_UINT32               = -1,

    }

    private enum PixelFormat {

        DISPLAYCONFIG_PIXELFORMAT_8_BPP        = 1,
        DISPLAYCONFIG_PIXELFORMAT_16_BPP       = 2,
        DISPLAYCONFIG_PIXELFORMAT_24_BPP       = 3,
        DISPLAYCONFIG_PIXELFORMAT_32_BPP       = 4,
        DISPLAYCONFIG_PIXELFORMAT_NONGDI       = 5,
        DISPLAYCONFIG_PIXELFORMAT_FORCE_UINT32 = -1,

    }

    private struct _2DRegion {

        public uint cx;
        public uint cy;

    }

    private struct AdditionalSignalInfo {

        private const int VSYNC_FREQ_DIVIDER_BIT_MASK = 0x3f;

        public  ushort videoStandard;
        private ushort split;

        public int vSyncFreqDivider {
            get => split & VSYNC_FREQ_DIVIDER_BIT_MASK;

            set {
                if (value <= VSYNC_FREQ_DIVIDER_BIT_MASK) {
                    split = (ushort) ((split & ~VSYNC_FREQ_DIVIDER_BIT_MASK) | value);
                } else {
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
                }
            }
        }

    }

    #endregion

}