using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Input;
using FormsNotifyIcon = System.Windows.Forms.NotifyIcon;
using Config.Net;
using NHotkey;
using NHotkey.Wpf;

namespace PowerDimmer
{
    public partial class App : Application
    {
        private IntPtr curFgHwnd;
        private ISettings settings;
        private FormsNotifyIcon? notifyIcon;
        private List<DimWindow> dimWindows { get; } = new();
        private List<WindowShade> shadeWindows { get; } = new();
        private SortedSet<IntPtr> pinnedHandles { get; } = new();
        static Func<int, double> brightnessToOpacity = (b) => 1 - (b / 100.0);

        static GCHandle GCSafetyHandleForActive;
        static GCHandle GCSafetyHandleForClose;

        public App()
        {
            settings = LoadSettings();
            NormalizeSettings(settings);
            settings.DimmingEnabled = settings.ActiveOnLaunch;
            SetupCrashLogging();
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

        private static string GetLogPath()
        {
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PowerDimmer");
            Directory.CreateDirectory(appDataDir);
            return Path.Combine(appDataDir, "crash.log");
        }

        private static void WriteCrashLog(Exception ex, string source)
        {
            try
            {
                var logPath = GetLogPath();
                var entry = $"[{DateTime.Now:O}] {source}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}";
                File.AppendAllText(logPath, entry);
            }
            catch
            {
                // Avoid secondary failures during crash handling.
            }
        }

        private void SetupCrashLogging()
        {
            DispatcherUnhandledException += (sender, args) =>
            {
                WriteCrashLog(args.Exception, "DispatcherUnhandledException");
                args.Handled = true;
                Shutdown();
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    WriteCrashLog(ex, "AppDomain.UnhandledException");
                }
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                WriteCrashLog(args.Exception, "TaskScheduler.UnobservedTaskException");
                args.SetObserved();
            };
        }

        private static string GetSettingsPath()
        {
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PowerDimmer");
            Directory.CreateDirectory(appDataDir);
            return Path.Combine(appDataDir, "settings.json");
        }

        private static ISettings LoadSettings()
        {
            var settingsPath = GetSettingsPath();
            var legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

            if (!File.Exists(settingsPath) && File.Exists(legacyPath))
            {
                File.Copy(legacyPath, settingsPath, overwrite: true);
            }

            try
            {
                return new ConfigurationBuilder<ISettings>().UseJsonFile(settingsPath).Build();
            }
            catch (Exception)
            {
                // Recover from invalid or malformed settings without crashing.
                File.WriteAllText(settingsPath, "{}");
                return new ConfigurationBuilder<ISettings>().UseJsonFile(settingsPath).Build();
            }
        }

        private static void NormalizeSettings(ISettings settingsToNormalize)
        {
            if (settingsToNormalize.Brightness < 0)
            {
                settingsToNormalize.Brightness = 0;
            }
            else if (settingsToNormalize.Brightness > 100)
            {
                settingsToNormalize.Brightness = 100;
            }
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                curFgHwnd = Win32.GetForegroundWindow();

                var iconController = new NotifyIconController(settings);
                notifyIcon = iconController.NotifyIcon;
                iconController.ExitClicked += () => Shutdown();
                Exit += (e, s) =>
                {
                    iconController.NotifyIcon.Visible = false;
                    iconController.NotifyIcon.Icon?.Dispose();
                    iconController.NotifyIcon.Dispose();
                };

                RegisterHotkeySafe("PowerDimmerHotkey", Key.D, ModifierKeys.Windows | ModifierKeys.Control | ModifierKeys.Alt, true, (s, e) =>
                {
                    settings.DimmingEnabled = !settings.DimmingEnabled;
                });

                RegisterHotkeySafe("DimToggleHotkey", Key.D, ModifierKeys.Windows | ModifierKeys.Shift, true, (s, e) =>
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

                RegisterHotkeySafe("ShadeToggleHotkey", Key.S, ModifierKeys.Windows | ModifierKeys.Alt, true, (s, e) =>
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

                RegisterHotkeySafe("CustomShadeHotkey", Key.A, ModifierKeys.Windows | ModifierKeys.Alt, true, (s, e) =>
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
            catch (Exception ex)
            {
                WriteCrashLog(ex, "App_Startup");
                Shutdown();
            }
        }

        private void RegisterHotkeySafe(string name, Key key, ModifierKeys modifiers, bool noRepeat, EventHandler<HotkeyEventArgs> handler)
        {
            try
            {
                HotkeyManager.Current.AddOrReplace(name, key, modifiers, noRepeat, handler);
            }
            catch (HotkeyAlreadyRegisteredException ex)
            {
                WriteCrashLog(ex, $"HotkeyRegistration:{name}");
                ShowHotkeyConflict(key, modifiers);
            }
            catch (Exception ex)
            {
                WriteCrashLog(ex, $"HotkeyRegistration:{name}");
            }
        }

        private void ShowHotkeyConflict(Key key, ModifierKeys modifiers)
        {
            if (notifyIcon == null)
            {
                return;
            }

            notifyIcon.BalloonTipTitle = "PowerDimmer hotkey conflict";
            notifyIcon.BalloonTipText =
                $"{FormatHotkey(modifiers, key)} is already registered by another app. " +
                "PowerDimmer will continue without that hotkey.";
            notifyIcon.ShowBalloonTip(5000);
        }

        private static string FormatHotkey(ModifierKeys modifiers, Key key)
        {
            var parts = new List<string>();
            if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
            parts.Add(key.ToString());
            return string.Join("+", parts);
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
                var windowShade = shadeWindows.SingleOrDefault(s => s.TargetHandle == whwnd);
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
                var windowShade = shadeWindows.SingleOrDefault(s => s.TargetHandle == hwnd);
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
    }
}
