using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PowerDimmer
{
    public partial class WindowShade : Window
    {
        public IntPtr Handle;
         IntPtr _targetHandle;
        static Win32.WinEventDelegate eventMovedDelegate = null;
        private Win32.RECT rect;
        static GCHandle GCSafetyHandle;
        public IntPtr TargetHandle { get { return _targetHandle; } }

        public WindowShade(ISettings settings, IntPtr targetHandle)
        {
            _targetHandle = targetHandle;
            ShowInTaskbar = false;
            AllowsTransparency = true;
            Background = Brushes.Black;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;

            eventMovedDelegate = new Win32.WinEventDelegate(WinEventMovedProc);

            if (_targetHandle != IntPtr.Zero)
            {
                rect = Win32.GetWindowRectangle(targetHandle);
                SetPosAndHeight(rect);
                Left = rect.Left;
                Top = rect.Top;
                Width = rect.Right - rect.Left;
                Height = rect.Bottom - rect.Top;

                uint pid = Win32.GetProcessId(_targetHandle);
                uint targetThreadId = Win32.GetWindowThreadProcessId(_targetHandle, IntPtr.Zero);
                //https://stackoverflow.com/questions/48767318/move-window-when-external-applications-window-moves
                //GCSafetyHandle = GCHandle.Alloc(WinEventDelegate);
                Win32.SetWinEventHook((uint)Win32.SWEH_Events.EVENT_OBJECT_LOCATIONCHANGE, (uint)Win32.SWEH_Events.EVENT_OBJECT_LOCATIONCHANGE,
                                      _targetHandle, eventMovedDelegate, pid, targetThreadId, Win32.WINEVENT_OUTOFCONTEXT);
            }

        }


        public void WinEventMovedProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            Debug.Print($"event triggered for {hwnd} event type {eventType}");
            if (hwnd != _targetHandle)
                return;
            rect = Win32.GetWindowRectangle(_targetHandle);
            SetPosAndHeight(rect);
        }

        private void SetPosAndHeight(Win32.RECT rect)
        {
            //use MoveWindow
            //Win32.SetWindowPos(Handle,
            //    _targetHandle,//(IntPtr)(-1),
            //    rect.Left,
            //    rect.Top,
            //    rect.Right - rect.Left,
            //    rect.Bottom - rect.Top,
            //    Win32.SWP_NOACTIVATE | Win32.SWP_NOZORDER);
            Win32.MoveWindow(Handle, rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top, false);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            Handle = new WindowInteropHelper(this).EnsureHandle();

            var style = Win32.GetWindowLong(Handle, Win32.GWL_EXSTYLE);
            Win32.SetWindowLong(Handle, Win32.GWL_EXSTYLE, style | Win32.WS_EX_LAYERED | Win32.WS_EX_TRANSPARENT | Win32.WS_EX_NOACTIVATE);
            Win32.ShowWindow(Handle, Win32.SW_SHOWNORMAL);

            base.OnSourceInitialized(e);
        }
    }
}
