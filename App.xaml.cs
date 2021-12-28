using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Config.Net;
using NHotkey.Wpf;

namespace PowerDimmer
{
    public partial class App : Application
    {
        private DimWindow dimWin;
        private Win32.WinEventDelegate eventDelegate;
        private SortedSet<IntPtr> pinnedHandles = new SortedSet<IntPtr>();
        private Brightness brightness;
        private ISettings settings;

        public App()
        {
            settings = new ConfigurationBuilder<ISettings>().UseJsonFile("settings.json").Build();
            brightness = new Brightness(settings.brightness);
            eventDelegate = new Win32.WinEventDelegate(WinEventProc);
            dimWin = new DimWindow(brightness);
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {
            var iconController = new NotifyIconController(brightness);
            iconController.ExitClicked += () => Shutdown();
            Exit += (e, s) =>
            {
                // iconController.NotifyIcon.Visible = false;
                iconController.NotifyIcon.Icon.Dispose();
                iconController.NotifyIcon.Dispose();
            };
            iconController.MenuClosed += () =>
            {
                settings.brightness = brightness.Value;
            };

            // Record current foreground window to return
            // to after launching, hotkey and dimwin steal focus
            var curHwnd = Win32.GetForegroundWindow();

            HotkeyManager.Current.AddOrReplace("DimToggleHotkey", Key.D, ModifierKeys.Windows | ModifierKeys.Shift, (s, e) =>
            {
                var curHwnd = Win32.GetForegroundWindow();
                if (pinnedHandles.Contains(curHwnd))
                {
                    pinnedHandles.Remove(curHwnd);
                }
                else
                {
                    pinnedHandles.Add(curHwnd);
                }
                Console.WriteLine("count: {0}", pinnedHandles.Count);
            });

            dimWin.Show();

            eventDelegate = new Win32.WinEventDelegate(WinEventProc);
            IntPtr m_hhook = Win32.SetWinEventHook(Win32.EVENT_SYSTEM_FOREGROUND, Win32.EVENT_SYSTEM_FOREGROUND,
                                                    IntPtr.Zero, eventDelegate, 0, 0, Win32.WINEVENT_OUTOFCONTEXT);

            // Not sure if we need UpdateDimming here
            // UpdateDimming(curHwnd);
            Win32.SetForegroundWindow(curHwnd);
        }

        // https://stackoverflow.com/questions/4372055/detect-active-window-changed-using-c-sharp-without-polling/10280800#10280800
        // https://docs.microsoft.com/en-us/windows/win32/winauto/event-constants
        public void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            UpdateDimming(hwnd);
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
            // Finally place the dimmer window behind the first pinned or foreground
            Win32.SetWindowPos(dimWin.Handle, firstPinned ?? fgHandle, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        }
    }

    public interface ISettings
    {
        [DefaultValue(50)]
        int brightness { get; set; }
    }

    public class Brightness : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private int _value;

        public Brightness(int initalValue)
        {
            _value = initalValue;
        }

        public int Value
        {
            get { return _value; }
            set
            {
                _value = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Value0));
            }
        }

        public double Value0
        {
            get { return _value / 100.0; }
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
