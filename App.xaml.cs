using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Config.Net;
using NHotkey.Wpf;

namespace PowerDimmer
{
    public partial class App : Application
    {
        private IntPtr curFgHwnd;
        private ISettings settings;
        private List<DimWindow> dimWindows { get; } = new();
        private SortedSet<IntPtr> pinnedHandles { get; } = new();
        static Func<int, double> brightnessToOpacity = (b) => 1 - (b / 100.0);

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

            HotkeyManager.Current.AddOrReplace("PowerDimmerHotkey", Key.D, ModifierKeys.Windows | ModifierKeys.Control | ModifierKeys.Alt, true, (s, e) =>
            {
                settings.DimmingEnabled = !settings.DimmingEnabled;
            });

            HotkeyManager.Current.AddOrReplace("DimToggleHotkey", Key.D, ModifierKeys.Windows | ModifierKeys.Shift, true, (s, e) =>
            {
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

            if (settings.ActiveOnLaunch)
            {
                dimOn(curFgHwnd);
            }

            var eventDelegate = new Win32.WinEventDelegate(WinEventProc);
            Win32.SetWinEventHook(Win32.EVENT_SYSTEM_FOREGROUND, Win32.EVENT_SYSTEM_FOREGROUND,
                                  IntPtr.Zero, eventDelegate, 0, 0, Win32.WINEVENT_OUTOFCONTEXT);
        }

        private void dimOn(IntPtr fgHwnd)
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
            if (Win32.IsStandardWindow(hwnd) && Win32.HasNoVisibleOwner(hwnd))
            {
                curFgHwnd = hwnd;
                if (settings.DimmingEnabled)
                {
                    UpdateDimming(hwnd);
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
        }
    }

    public interface ISettings : INotifyPropertyChanged
    {
        [Option(Alias = "activeOnLaunch", DefaultValue = true)]
        bool ActiveOnLaunch { get; set; }

        [Option(Alias = "dimmingEnabled", DefaultValue = true)]
        bool DimmingEnabled { get; set; }

        [Option(Alias = "brightness", DefaultValue = 50)]
        int Brightness { get; set; }
    }
}
