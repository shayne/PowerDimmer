using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;

namespace PowerDimmer
{
    public partial class DimWindow : Window
    {
        public IntPtr Handle
        {
            get
            {
                return new WindowInteropHelper(this).Handle;
            }
        }

        private Brightness brightness;

        public DimWindow(Brightness _brightness)
        {
            InitializeComponent();

            brightness = _brightness;

            var opacityBinding = new System.Windows.Data.Binding(nameof(brightness.Value0));
            opacityBinding.Mode = BindingMode.OneWay;
            opacityBinding.Source = brightness;

            SetBinding(Window.OpacityProperty, opacityBinding);
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);

            WindowState = WindowState.Maximized;

            var style = Win32.GetWindowLong(Handle, Win32.GWL_EXSTYLE);
            Win32.SetWindowLong(Handle, Win32.GWL_EXSTYLE, style | Win32.WS_EX_LAYERED | Win32.WS_EX_TRANSPARENT | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE);
        }
    }
}