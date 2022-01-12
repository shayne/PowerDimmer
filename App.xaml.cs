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
        private IntPtr lastFgHwnd;
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
                        enableAllDimming(lastFgHwnd);
                    }
                    else
                    {
                        disableAllDimming();
                    }
                }
            };
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {
            // Record starting foreground window to return
            // to after launching. Hotkey and dimwin steal focus
            var startingFgHwnd = Win32.GetForegroundWindow();

            var iconController = new NotifyIconController(settings);
            iconController.ExitClicked += () => Shutdown();
            Exit += (e, s) =>
            {
                // iconController.NotifyIcon.Visible = false;
                iconController.NotifyIcon.Icon.Dispose();
                iconController.NotifyIcon.Dispose();
            };

            HotkeyManager.Current.AddOrReplace("PowerDimmerHotkey", Key.D, ModifierKeys.Windows | ModifierKeys.Control | ModifierKeys.Alt, true, (s, e) =>
            {
                settings.DimmingEnabled = !settings.DimmingEnabled;
            });

            HotkeyManager.Current.AddOrReplace("DimToggleHotkey", Key.D, ModifierKeys.Windows | ModifierKeys.Shift, true, (s, e) =>
            {
                if (pinnedHandles.Contains(lastFgHwnd))
                {
                    pinnedHandles.Remove(lastFgHwnd);
                }
                else
                {
                    pinnedHandles.Add(lastFgHwnd);
                }
            });

            if (settings.ActiveOnLaunch)
            {
                enableAllDimming(startingFgHwnd);
            }
        }

        private void enableAllDimming(IntPtr fgHwnd)
        {
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                var win = new DimWindow(settings);
                win.Left = screen.Bounds.Left;
                win.Top = screen.Bounds.Top;
                win.Opacity = brightnessToOpacity(settings.Brightness);
                dimWindows.Add(win);
            }

            dimWindows.ForEach(w => w.Show());

            var eventDelegate = new Win32.WinEventDelegate(WinEventProc);
            Win32.SetWinEventHook(Win32.EVENT_SYSTEM_FOREGROUND, Win32.EVENT_SYSTEM_FOREGROUND,
                                  IntPtr.Zero, eventDelegate, 0, 0, Win32.WINEVENT_OUTOFCONTEXT);


            Win32.SetForegroundWindow(fgHwnd);
        }

        private void disableAllDimming()
        {
            dimWindows.ForEach(w => w.Close());
            dimWindows.Clear();
        }

        // https://stackoverflow.com/questions/4372055/detect-active-window-changed-using-c-sharp-without-polling/10280800#10280800
        // https://docs.microsoft.com/en-us/windows/win32/winauto/event-constants
        public void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (Win32.IsStandardWindow(hwnd) && Win32.HasNoVisibleOwner(hwnd))
            {
                lastFgHwnd = hwnd;
                if (settings.DimmingEnabled)
                {
                    UpdateDimming(hwnd);
                }
            }
        }

        private void UpdateDimming(IntPtr fgHandle)
        {
            Console.WriteLine("UpdateDimming, handle: {0}", fgHandle);
            // Set the incoming/foreground handle as TOP...
            Win32.SetWindowPos(fgHandle, Win32.HWND_TOP, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
            IntPtr? firstPinned = null;
            foreach (var pinHandle in pinnedHandles)
            {
                if (pinHandle == fgHandle) continue; // if pinned but also foreground skip
                // Place each pinned window under the foreground
                Win32.SetWindowPos(pinHandle, fgHandle, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
                // store the first pinned handle we didn't skip over
                firstPinned ??= pinHandle;
            }
            foreach (var dimWin in dimWindows)
            {
                // Finally place the dimmer window behind the first pinned or foreground
                Win32.SetWindowPos(dimWin.Handle, firstPinned ?? fgHandle, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
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
