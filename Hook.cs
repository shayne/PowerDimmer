using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PowerDimmer
{
    public class Hook
    {
        public delegate void WinEventDelegate(
            IntPtr hWinEventHook,
            Win32.SWEH_Events eventType,
            IntPtr hwnd,
            Win32.SWEH_ObjectId idObject,
            long idChild,
            uint dwEventThread,
            uint dwmsEventTime
        );

        public static IntPtr WinEventHookRange(
            Win32.SWEH_Events eventFrom, Win32.SWEH_Events eventTo,
            IntPtr hwnd,
            WinEventDelegate eventDelegate,
            uint idProcess, uint idThread)
        {
            return Win32.SetWinEventHook(
                eventFrom, eventTo,
                hwnd, eventDelegate,
                idProcess, idThread,
                Win32.WinEventHookInternalFlags);
        }

        public static IntPtr WinEventHookOne(
            Win32.SWEH_Events eventId,
            IntPtr hwnd,
            WinEventDelegate eventDelegate,
            uint idProcess,
            uint idThread)
        {
            return Win32.SetWinEventHook(
                eventId, eventId,
                hwnd, eventDelegate,
                idProcess, idThread,
                Win32.WinEventHookInternalFlags);
        }

        public static bool WinEventUnhook(IntPtr hWinEventHook) => 
            Win32.UnhookWinEvent(hWinEventHook);

        public static uint GetWindowThread(IntPtr hWnd)
        {
            return Win32.GetWindowThreadProcessId(hWnd, IntPtr.Zero);
        }

        public static Win32.RECT GetWindowRectangle(IntPtr hWnd)
        {
            Win32.DwmGetWindowAttribute(hWnd, 
                Win32.DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, 
                out Win32.RECT rect, Marshal.SizeOf<Win32.RECT>());
            return rect;
        }
    }
}
