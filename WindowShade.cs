using System;
using System.Collections.Generic;
using System.Linq;
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
        private IntPtr _targetHandle;
        private IntPtr hWinEventHook;
        protected Hook.WinEventDelegate WinEventDelegate;
        private Win32.RECT rect;
        public WindowShade(ISettings settings, IntPtr targetHandle)
        {
            _targetHandle = targetHandle;
            ShowInTaskbar = false;
            AllowsTransparency = true;
            Background = Brushes.Black;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;

            if (_targetHandle != IntPtr.Zero)
            {
                rect = Hook.GetWindowRectangle(targetHandle);
                //SetPosAndHeight(rect);
                Left = rect.Left;
                Top = rect.Top;
                Width = rect.Right - rect.Left;
                Height = rect.Bottom - rect.Top;

                //https://stackoverflow.com/questions/48767318/move-window-when-external-applications-window-moves
                WinEventDelegate = new Hook.WinEventDelegate(WinMovedEventProc);
                hWinEventHook = Hook.WinEventHookOne(Win32.SWEH_Events.EVENT_OBJECT_LOCATIONCHANGE, _targetHandle, WinMovedEventProc, 0, 0);
            }

        }

        private void WinMovedEventProc(
            IntPtr hWinEventHook, 
            Win32.SWEH_Events eventType, 
            IntPtr hWnd, 
            Win32.SWEH_ObjectId idObject, 
            long idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (hWnd != _targetHandle)
                return;
            rect = Hook.GetWindowRectangle(_targetHandle);
            SetPosAndHeight(rect);
        }

        private void SetPosAndHeight(Win32.RECT rect)
        {
            Win32.SetWindowPos(Handle,
                _targetHandle,//(IntPtr)(-1),
                rect.Left,
                rect.Top,
                rect.Right - rect.Left,
                rect.Bottom - rect.Top,
                0);
            Win32.SetWindowPos(_targetHandle,
                Handle,
                0,
                0,
                0,
                0,
                Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
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
