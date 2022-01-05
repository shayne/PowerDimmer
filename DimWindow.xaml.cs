using System;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms;
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

            var totalSize = Screen.AllScreens.Select(screen => screen.Bounds)
                                             .Aggregate(Rectangle.Union).Size;
            Width = totalSize.Width;
            Height = totalSize.Height;
            Console.WriteLine("Total Size: {0}", totalSize);

            brightness = _brightness;

            var opacityBinding = new System.Windows.Data.Binding(nameof(brightness.Value0));
            opacityBinding.Mode = BindingMode.OneWay;
            opacityBinding.Source = brightness;

            SetBinding(Window.OpacityProperty, opacityBinding);
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);

            var style = Win32.GetWindowLong(Handle, Win32.GWL_EXSTYLE);
            Win32.SetWindowLong(Handle, Win32.GWL_EXSTYLE, style | Win32.WS_EX_LAYERED | Win32.WS_EX_TRANSPARENT | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE);
        }
    }
}