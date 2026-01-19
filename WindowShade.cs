using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        private static Win32.WinEventDelegate? eventMovedDelegate;
        private Win32.RECT rect;
        static GCHandle GCSafetyHandle;
        private IntPtr eventHook;
        public IntPtr TargetHandle { get { return _targetHandle; } }
        private bool isLocalPos = false;

        private double _left;
        private double _top;
        private double _width;
        private double _height;

        public WindowShade(IntPtr targetHandle)
        {
            _targetHandle = targetHandle;
            ShowInTaskbar = false;
            AllowsTransparency = true;
            Background = Brushes.Black;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;

            //Allocating the delegate to a GCHandle example from here. This prevents it from getting garbage collected and windows throwing a message error.
            //https://stackoverflow.com/questions/48767318/move-window-when-external-applications-window-moves
            var movedDelegate = new Win32.WinEventDelegate(WinEventMovedProc);
            eventMovedDelegate = movedDelegate;
            GCSafetyHandle = GCHandle.Alloc(movedDelegate);

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
                eventHook = Win32.SetWinEventHook((uint)Win32.SWEH_Events.EVENT_OBJECT_LOCATIONCHANGE, (uint)Win32.SWEH_Events.EVENT_OBJECT_LOCATIONCHANGE,
                                      _targetHandle, eventMovedDelegate, pid, targetThreadId, Win32.WINEVENT_OUTOFCONTEXT);
            }

        }

        public WindowShade(IntPtr targetHandle, double left, double top, double width, double height)
        {
            _targetHandle = targetHandle;
            ShowInTaskbar = false;
            AllowsTransparency = true;
            Background = Brushes.Black;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;

            _left = left;
            _top = top; 
            _width = width;
            _height = height;
            
            isLocalPos = true;

            var movedDelegate = new Win32.WinEventDelegate(WinEventMovedProc);
            eventMovedDelegate = movedDelegate;
            GCSafetyHandle = GCHandle.Alloc(movedDelegate);

            if (_targetHandle != IntPtr.Zero)
            {
                rect = Win32.GetWindowRectangle(targetHandle);
                Left = rect.Left + left;
                Top = rect.Top + top;
                Width = width ;
                Height = height;

                uint pid = Win32.GetProcessId(_targetHandle);
                uint targetThreadId = Win32.GetWindowThreadProcessId(_targetHandle, IntPtr.Zero);
                eventHook = Win32.SetWinEventHook((uint)Win32.SWEH_Events.EVENT_OBJECT_LOCATIONCHANGE, (uint)Win32.SWEH_Events.EVENT_OBJECT_LOCATIONCHANGE,
                                      _targetHandle, eventMovedDelegate, pid, targetThreadId, Win32.WINEVENT_OUTOFCONTEXT);
            }
        }

        public void WinEventMovedProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (hwnd != _targetHandle)
                return;
            rect = Win32.GetWindowRectangle(_targetHandle);
            SetPosAndHeight(rect);
        }

        private void SetPosAndHeight(Win32.RECT rect)
        {
            if(!isLocalPos)
                Win32.MoveWindow(Handle, rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top, false);
            else
            {
                int left = (int)(rect.Left + _left);
                int top = (int)(rect.Top + _top);
                int width = (int)(_width);
                int height = (int)(_height);
                Win32.MoveWindow(Handle, left, top, width, height, false);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            Handle = new WindowInteropHelper(this).EnsureHandle();

            var style = Win32.GetWindowLong(Handle, Win32.GWL_EXSTYLE);
            Win32.SetWindowLong(Handle, Win32.GWL_EXSTYLE, style | Win32.WS_EX_LAYERED | Win32.WS_EX_TRANSPARENT | Win32.WS_EX_NOACTIVATE);
            Win32.ShowWindow(Handle, Win32.SW_SHOWNORMAL);

            base.OnSourceInitialized(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if(!e.Cancel)
            {
                if(GCSafetyHandle.IsAllocated)
                {
                    GCSafetyHandle.Free();
                }
                Win32.UnhookWinEvent(eventHook);
            }
        }
    }
}
