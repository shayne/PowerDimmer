using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Config.Net;
using NHotkey;
using NHotkey.Wpf;

namespace PowerDimmer
{
    public partial class App : Application
    {
        private IntPtr curFgHwnd;
        private ISettings settings;
        private List<DimWindow> dimWindows { get; } = new();
        private List<WindowShade> shadeWindows { get; } = new();
        private SortedSet<IntPtr> pinnedHandles { get; } = new();
        static Func<int, double> brightnessToOpacity = (b) => 1 - (b / 100.0);

        static GCHandle GCSafetyHandleForActive;
        static GCHandle GCSafetyHandleForClose;

        public App()
        {
            settings = new ConfigurationBuilder<ISettings>().UseJsonFile("settings.json").Build();
            settings.DimmingEnabled = settings.ActiveOnLaunch;
            settings.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(settings.Brightness))
                {
                    dimWindows.ForEach(w => w.Opacity = brightnessToOpacity(settings.Brightness));
                }
                else if (e.PropertyName == nameof(settings.DimmingEnabled))
                {
                    if (settings.DimmingEnabled)
                    {
                        dimOn(curFgHwnd);
                    }
                    else
                    {
                        dimOff();
                    }
                }
            };
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {
            curFgHwnd = Win32.GetForegroundWindow();

            var iconController = new NotifyIconController(settings);
            iconController.ExitClicked += () => Shutdown();
            Exit += (e, s) =>
            {
                iconController.NotifyIcon.Visible = false;
                iconController.NotifyIcon.Icon.Dispose();
                iconController.NotifyIcon.Dispose();
            };

            TryRegisterHotkey(iconController, "PowerDimmerHotkey", settings.HotkeyPowerDimmerToggle, (s, e) =>
            {
                settings.DimmingEnabled = !settings.DimmingEnabled;
            });

            TryRegisterHotkey(iconController, "DimToggleHotkey", settings.HotkeyPinToggle, (s, e) =>
            {
                if (!settings.DimmingEnabled)
                {
                    return;
                }

                var hwnd = Win32.GetForegroundWindow();
                if (pinnedHandles.Contains(hwnd))
                {
                    pinnedHandles.Remove(hwnd);
                }
                else
                {
                    pinnedHandles.Add(hwnd);
                }
            });

            TryRegisterHotkey(iconController, "ShadeToggleHotkey", settings.HotkeyShadeToggle, (s, e) =>
            {
                if (!settings.WindowShadeEnabled)
                {
                    return;
                }

                //unshade window if exists
                var hwnd = Win32.GetForegroundWindow();
                var shadedWindow = shadeWindows.SingleOrDefault(w => w.TargetHandle == hwnd);
                if (shadedWindow != null)
                {
                    shadedWindow.Close();
                    shadeWindows.Remove(shadedWindow);
                }
                else
                {
                    var opacity = brightnessToOpacity(settings.Brightness);
                    var shade = new WindowShade(hwnd)
                    {
                        Opacity = opacity
                    };
                    shade.Show();
                    shadeWindows.Add(shade);
                }
            });

            TryRegisterHotkey(iconController, "CustomShadeHotkey", settings.HotkeyCustomShade, (s, e) =>
            {
                if (!settings.WindowShadeEnabled)
                {
                    return;
                }

                var hwnd = Win32.GetForegroundWindow();
                var shadedWindow = shadeWindows.SingleOrDefault(w => w.TargetHandle == hwnd);
                if (shadedWindow != null)
                {
                    shadedWindow.Close();
                    shadeWindows.Remove(shadedWindow);
                }
                var opacity = brightnessToOpacity(settings.Brightness);
                var customShadeCreatedDelegate = new Win32.CustomShadeCreatedEventDelegate(CreatedCustomShadeEventProc);
                var customShade = new CustomShadeTool(hwnd, customShadeCreatedDelegate);
                customShade.Show();
            });

            if (settings.ActiveOnLaunch)
            {
                dimOn(curFgHwnd);
            }

            var eventDelegate = new Win32.WinEventDelegate(WinEventProc);
            GCSafetyHandleForActive = GCHandle.Alloc(eventDelegate);
            Win32.SetWinEventHook(Win32.EVENT_SYSTEM_FOREGROUND, Win32.EVENT_SYSTEM_FOREGROUND,
                                  IntPtr.Zero, eventDelegate, 0, 0, Win32.WINEVENT_OUTOFCONTEXT);

            var eventClosedDelegate = new Win32.WinEventDelegate(WinCloseEventProc);
            GCSafetyHandleForClose = GCHandle.Alloc(eventClosedDelegate);
            Win32.SetWinEventHook(Win32.SWEH_Events.EVENT_OBJECT_DESTROY, Win32.SWEH_Events.EVENT_OBJECT_DESTROY,
                                  IntPtr.Zero, eventClosedDelegate, 0, 0, Win32.WINEVENT_OUTOFCONTEXT);
        }

        // Registers a hotkey defined in settings (e.g. "Ctrl+Win+Alt+D"), skipping and warning instead of crashing if it's invalid or already claimed by another app.
        private void TryRegisterHotkey(NotifyIconController iconController, string name, string hotkeyString, EventHandler<HotkeyEventArgs> handler)
        {
            if (!TryParseHotkey(hotkeyString, out var key, out var modifiers))
            {
                iconController.NotifyIcon.ShowBalloonTip(5000, "PowerDimmer", $"Invalid shortcut \"{hotkeyString}\" in settings.json for {name}. That action has no shortcut until it's fixed.", System.Windows.Forms.ToolTipIcon.Warning);
                return;
            }

            try
            {
                HotkeyManager.Current.AddOrReplace(name, key, modifiers, true, handler);
            }
            catch (HotkeyAlreadyRegisteredException)
            {
                iconController.NotifyIcon.ShowBalloonTip(5000, "PowerDimmer", $"Shortcut \"{hotkeyString}\" for {name} is already in use by another app. Change it in settings.json and restart PowerDimmer.", System.Windows.Forms.ToolTipIcon.Warning);
            }
        }

        private static bool TryParseHotkey(string hotkeyString, out Key key, out ModifierKeys modifiers)
        {
            key = Key.None;
            modifiers = ModifierKeys.None;

            if (string.IsNullOrWhiteSpace(hotkeyString))
            {
                return false;
            }

            var parts = hotkeyString.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0 || !Enum.TryParse(parts[^1], true, out key))
            {
                return false;
            }

            foreach (var part in parts[..^1])
            {
                switch (part.ToLowerInvariant())
                {
                    case "ctrl":
                    case "control":
                        modifiers |= ModifierKeys.Control;
                        break;
                    case "alt":
                        modifiers |= ModifierKeys.Alt;
                        break;
                    case "shift":
                        modifiers |= ModifierKeys.Shift;
                        break;
                    case "win":
                    case "windows":
                        modifiers |= ModifierKeys.Windows;
                        break;
                    default:
                        return false;
                }
            }

            return true;
        }

        private void dimOn(IntPtr fgHwnd)//creates a dim window on each screen
        {
            var opacity = brightnessToOpacity(settings.Brightness);
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                var win = new DimWindow(settings)
                {
                    Left = screen.Bounds.Left,
                    Top = screen.Bounds.Top,
                    Opacity = opacity
                };
                win.Show();
                dimWindows.Add(win);
            }

            UpdateDimming(fgHwnd);
        }

        private void dimOff()
        {
            dimWindows.ForEach(w => w.Close());
            dimWindows.Clear();
            // the following maintains the proper
            // foreground window upon disabling
            Win32.SetForegroundWindow(curFgHwnd);
        }

        // https://stackoverflow.com/questions/4372055/detect-active-window-changed-using-c-sharp-without-polling/10280800#10280800
        // https://docs.microsoft.com/en-us/windows/win32/winauto/event-constants
        public void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (settings.DimmingEnabled)
            {
                if (Win32.IsStandardWindow(hwnd) && Win32.HasNoVisibleOwner(hwnd))
                {
                    curFgHwnd = hwnd;
                    if (settings.DimmingEnabled)
                    {
                        UpdateDimming(hwnd);
                    }
                }
            }
            if (settings.WindowShadeEnabled)
            {
                var whwnd = Win32.GetForegroundWindow();
                WindowShade windowShade = shadeWindows.SingleOrDefault(s => s.TargetHandle == whwnd);
                if (windowShade != null)
                {
                    UpdateShade(whwnd, windowShade);
                }
            }
        }
        public void WinCloseEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (settings.WindowShadeEnabled)
            {
                WindowShade windowShade = shadeWindows.SingleOrDefault(s => s.TargetHandle == hwnd);
                if(windowShade != null)
                {
                    windowShade.Close();
                    shadeWindows.Remove(windowShade);
                }
            }

            if(settings.DimTaskbar)
            {
                IntPtr taskbarHwnd = Win32.FindWindow("Shell_TrayWnd", null);
                if (taskbarHwnd != IntPtr.Zero)
                {
                    Win32.SetWindowPos(taskbarHwnd, Win32.HWND_BOTTOM, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
                }
            }
        }

        private void UpdateDimming(IntPtr fgHwnd)
        {
            // Set the incoming/foreground handle as TOP...
            Win32.SetWindowPos(fgHwnd, Win32.HWND_TOP, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
            IntPtr? firstPinned = null;
            foreach (var pinHandle in pinnedHandles)
            {
                if (pinHandle == fgHwnd) continue; // if pinned but also foreground skip
                // Place each pinned window under the foreground
                Win32.SetWindowPos(pinHandle, fgHwnd, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
                // store the first pinned handle we didn't skip over
                firstPinned ??= pinHandle;
            }
            foreach (var dimWin in dimWindows)
            {
                // Finally place the dimmer window behind the first pinned or foreground
                Win32.SetWindowPos(dimWin.Handle, firstPinned ?? fgHwnd, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
            }

            if(settings.DimTaskbar)
            {
                IntPtr taskbarHwnd = Win32.FindWindow("Shell_TrayWnd", null);
                if (taskbarHwnd != IntPtr.Zero)
                {
                    Win32.SetWindowPos(taskbarHwnd, Win32.HWND_BOTTOM, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
                }
            }
        }

        private void UpdateShade(IntPtr shadedHwnd, WindowShade windowShade)
        {
            // Set the window shade handle as TOP...
            Win32.SetWindowPos(windowShade.Handle, Win32.HWND_TOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
            //delay making it not top because the window seems to end up on top immediatly afterwards
            Task.Run(() => 
            {
                System.Threading.Thread.Sleep(500);
                Win32.SetWindowPos(windowShade.Handle, Win32.HWND_NOTOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE); 
            });
        }

        public void CreatedCustomShadeEventProc(Window shadeTool, IntPtr hwnd, double left, double top, double width, double height)
        {
            var shadedWindow = shadeWindows.SingleOrDefault(w => w.TargetHandle == hwnd);
            if (shadedWindow != null)
            {
                shadedWindow.Close();
                shadeWindows.Remove(shadedWindow);
            }
            else
            {
                var opacity = brightnessToOpacity(settings.Brightness);
                var shade = new WindowShade(hwnd, left, top, width, height)
                {
                    Opacity = opacity
                };
                shade.Show();
                shadeWindows.Add(shade);
            }
            shadeTool.Close();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            if(GCSafetyHandleForActive.IsAllocated)
            {
                GCSafetyHandleForActive.Free();
            }
            if(GCSafetyHandleForClose.IsAllocated)
            {
                GCSafetyHandleForClose.Free();
            }
        }
    }

    public interface ISettings : INotifyPropertyChanged
    {
        [Option(Alias = "activeOnLaunch", DefaultValue = true)]
        bool ActiveOnLaunch { get; set; }

        [Option(Alias = "dimmingEnabled", DefaultValue = false)]
        bool DimmingEnabled { get; set; }

        [Option(Alias = "dimTaskbar", DefaultValue = true)]
        bool DimTaskbar { get; set; }

        [Option(Alias = "brightness", DefaultValue = 50)]
        int Brightness { get; set; }

        [Option(Alias = "windowShadeEnabled", DefaultValue = true)]
        bool WindowShadeEnabled { get; set; }

        [Option(Alias = "hotkeyPowerDimmerToggle", DefaultValue = "Ctrl+Win+Alt+D")]
        string HotkeyPowerDimmerToggle { get; set; }

        [Option(Alias = "hotkeyPinToggle", DefaultValue = "Win+Shift+D")]
        string HotkeyPinToggle { get; set; }

        [Option(Alias = "hotkeyShadeToggle", DefaultValue = "Win+Alt+S")]
        string HotkeyShadeToggle { get; set; }

        [Option(Alias = "hotkeyCustomShade", DefaultValue = "Win+Alt+A")]
        string HotkeyCustomShade { get; set; }
    }
}
