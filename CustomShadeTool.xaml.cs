using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PowerDimmer
{
    /// <summary>
    /// Interaction logic for CustomShadeTool.xaml
    /// </summary>
    public partial class CustomShadeTool : Window
    {
        public IntPtr Handle;
        IntPtr _targetHandle;
        private Win32.RECT rect;
        private CustomShadeToolVM vm = new CustomShadeToolVM();
        public Win32.CustomShadeCreatedEventDelegate _createdCallback;

        public CustomShadeTool(IntPtr targetHandle, Win32.CustomShadeCreatedEventDelegate createdCallback)
        {
            _createdCallback = createdCallback;
            //ShadeOpacity = shadeOpacity;
            InitializeComponent();
            this.DataContext = vm;

            _targetHandle = targetHandle;
            ShowInTaskbar = false;
            AllowsTransparency = true;
            Background = Brushes.Red;
            Opacity = 0.1;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;

            if (_targetHandle != IntPtr.Zero)
            {
                rect = Win32.GetWindowRectangle(targetHandle);

                Left = rect.Left;
                Top = rect.Top;
                Width = rect.Right - rect.Left;
                Height = rect.Bottom - rect.Top;
            }
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            Handle = new WindowInteropHelper(this).EnsureHandle();

            var style = Win32.GetWindowLong(Handle, Win32.GWL_EXSTYLE);
            Win32.SetWindowLong(Handle, Win32.GWL_EXSTYLE, style | Win32.WS_EX_LAYERED);
            Win32.ShowWindow(Handle, Win32.SW_SHOWNORMAL);

            base.OnSourceInitialized(e);
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if(e.ChangedButton == MouseButton.Left)
            {
                vm.isDragging = true;
                vm.dragStartPos = e.GetPosition(this);

                this.CaptureMouse();
                e.Handled = true;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (vm.isDragging)
            {
                Point mousePoint = e.GetPosition(this);
                vm.UpdateRect(mousePoint);
                e.Handled = true;
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            vm.isDragging = false;
            e.Handled = true;

            if(_createdCallback != null && vm.ShadeWidth > 0 && vm.ShadeHeight > 0)
            {
                _createdCallback.Invoke(this, _targetHandle, vm.LeftPos, vm.TopPos, vm.ShadeWidth, vm.ShadeHeight);
            }
        }


    }
}
