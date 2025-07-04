#nullable enable

using Gma.System.MouseKeyHook;
using ManagedWinapi.Windows;
using NLog;
using System;
using System.Windows.Forms;

namespace WindowSizeGuard;

public interface HotkeyHandler {

    void onKeyDown(object sender, KeyEventArgs e);

}

[Component]
public class HotkeyHandlerImpl: HotkeyHandler, IDisposable {

    private static readonly Logger LOGGER = LogManager.GetLogger(typeof(HotkeyHandlerImpl).FullName!);

    private readonly WindowZoneManager    windowZoneManager;
    private readonly MonitorSwitcher      monitorSwitcher;
    private readonly IKeyboardMouseEvents globalHook;

    public HotkeyHandlerImpl(WindowZoneManager windowZoneManager, MonitorSwitcher monitorSwitcher) {
        this.windowZoneManager = windowZoneManager;
        this.monitorSwitcher   = monitorSwitcher;

        globalHook         =  Hook.GlobalEvents();
        globalHook.KeyDown += onKeyDown;

        LOGGER.Trace("Waiting for hotkeys.");
    }

    public void Dispose() {
        globalHook.KeyDown -= onKeyDown;
        globalHook.Dispose();
    }

    public void onKeyDown(object sender, KeyEventArgs _e) {
        KeyEventArgsExt e = (KeyEventArgsExt) _e;
        e.Handled = true; //will be set back to false at the end of this method if nothing handles this key

        SystemWindow foregroundWindow = SystemWindow.ForegroundWindow;
        bool         isWinKeyPressed  = e.isWinKeyPressed();
        bool         winPressed       = isWinKeyPressed && !e.Alt && !e.Shift;
        bool         winAltPressed    = isWinKeyPressed && e.Alt && !e.Shift;
        int?         rectangleInZone  = e.Control ? 0 : null;

        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault we're not trying to handle every possible key
        switch (e.KeyCode) {
            case Keys.PageDown when winPressed:
            case Keys.Right when winPressed && !e.Control: // Win+Ctrl+Right is Next Virtual Desktop, which should be preserved
                windowZoneManager.resizeWindowToZone(foregroundWindow, WindowZone.RIGHT, rectangleInZone);
                break;
            case Keys.Delete when winPressed:
            case Keys.Left when winPressed && !e.Control: // Win+Ctrl+Left is Previous Virtual Desktop, which should be preserved
                windowZoneManager.resizeWindowToZone(foregroundWindow, WindowZone.LEFT, rectangleInZone);
                break;
            case Keys.Home when winPressed:
            case Keys.Up when winPressed:
                windowZoneManager.resizeWindowToZone(foregroundWindow, WindowZone.TOP, rectangleInZone);
                break;
            case Keys.End when winPressed:
            case Keys.Down when winPressed:
                windowZoneManager.resizeWindowToZone(foregroundWindow, WindowZone.BOTTOM, rectangleInZone);
                break;
            case Keys.PageUp when winAltPressed:
                windowZoneManager.resizeWindowToZone(foregroundWindow, WindowZone.TOP_RIGHT, rectangleInZone);
                break;
            case Keys.PageDown when winAltPressed:
                windowZoneManager.resizeWindowToZone(foregroundWindow, WindowZone.BOTTOM_RIGHT, rectangleInZone);
                break;
            case Keys.End when winAltPressed:
                windowZoneManager.resizeWindowToZone(foregroundWindow, WindowZone.BOTTOM_LEFT, rectangleInZone);
                break;
            case Keys.Home when winAltPressed:
                windowZoneManager.resizeWindowToZone(foregroundWindow, WindowZone.TOP_LEFT, rectangleInZone);
                break;
            case Keys.Insert when winAltPressed:
                windowZoneManager.resizeWindowToZone(foregroundWindow, WindowZone.CENTER, rectangleInZone);
                break;
            case Keys.Insert when winPressed:
                windowZoneManager.maximizeForegroundWindow();
                break;
            case Keys.PageUp when winPressed:
                windowZoneManager.minimizeForegroundWindow();
                break;
            case Keys.T when winPressed:
                windowZoneManager.toggleForegroundWindowAlwaysOnTop();
                break;
            case Keys.Delete when winAltPressed:
                // do nothing, to prevent accidental deletion when alt is pressed
                break;
            case Keys.P when winPressed:
                monitorSwitcher.switchToSingleOtherMonitor();
                break;
            default:
                e.Handled = false;
                break;
        }
    }

}