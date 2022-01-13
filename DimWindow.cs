using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PowerDimmer
{
    public partial class DimWindow : Window
    {
        public IntPtr Handle;

        public DimWindow(ISettings settings)
        {
            ShowInTaskbar = false;
            AllowsTransparency = true;
            Background = Brushes.Black;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            Handle = new WindowInteropHelper(this).EnsureHandle();

            var style = Win32.GetWindowLong(Handle, Win32.GWL_EXSTYLE);
            Win32.SetWindowLong(Handle, Win32.GWL_EXSTYLE, style | Win32.WS_EX_LAYERED | Win32.WS_EX_TRANSPARENT | Win32.WS_EX_NOACTIVATE);
            Win32.ShowWindow(Handle, Win32.SW_SHOWMAXIMIZED);

            base.OnSourceInitialized(e);
        }
    }
}