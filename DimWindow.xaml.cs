using System;
using System.Windows;
using System.Windows.Interop;

namespace PowerDimmer
{
    public partial class DimWindow : Window
    {
        private Lazy<IntPtr> _handle;
        public IntPtr Handle => _handle.Value;

        public DimWindow(ISettings settings)
        {
            InitializeComponent();

            _handle = new(() => new WindowInteropHelper(this).Handle);
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);

            // Note: Moving this to the xaml or anywhere else
            // causes the window to stay on the primary display.
            WindowState = WindowState.Maximized;

            var style = Win32.GetWindowLong(Handle, Win32.GWL_EXSTYLE);
            Win32.SetWindowLong(Handle, Win32.GWL_EXSTYLE, style | Win32.WS_EX_LAYERED | Win32.WS_EX_TRANSPARENT | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE);
        }
    }
}