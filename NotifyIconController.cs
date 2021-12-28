using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace PowerDimmer
{
    public partial class NotifyIconController
    {
        internal Action? ExitClicked;
        internal Action? MenuClosed;
        public NotifyIcon NotifyIcon;

        public NotifyIconController(Brightness brightness)
        {
            NotifyIcon = new NotifyIcon();

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.AddRange(
                new ToolStripItem[]
                {
                    new TrackBarMenuItem(brightness),
                    new ToolStripSeparator(),
                    new ToolStripMenuItem("E&xit", null,
                        (s, e) => ExitClicked?.Invoke())
                });

            NotifyIcon.ContextMenuStrip = contextMenu;
            NotifyIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            NotifyIcon.Text = "PowerDimmer";
            NotifyIcon.Visible = true;

            contextMenu.Closed += (o, a) => { MenuClosed?.Invoke(); };
        }
    }

    // https://stackoverflow.com/a/24825487
    public class TrackBarWithoutFocus : TrackBar
    {
        private const int WM_SETFOCUS = 0x0007;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_SETFOCUS)
            {
                return;
            }

            base.WndProc(ref m);
        }
    }

    // https://stackoverflow.com/questions/4339143/adding-a-trackbar-control-to-a-contextmenu
    public class TrackBarMenuItem : ToolStripControlHost
    {
        private TrackBar trackBar;

        public TrackBarMenuItem(Brightness brightness) : base(new ContainerControl())
        {
            BackColor = Color.White;

            var label = new Label()
            {
                Parent = Control,
                Text = "Brightness",
                TextAlign = ContentAlignment.MiddleCenter,
            };

            trackBar = new TrackBarWithoutFocus
            {
                Parent = Control,
                Top = 22,
                Minimum = 0,
                Maximum = 100,
                TickFrequency = 1,
                SmallChange = 5,
                LargeChange = 20,
                TickStyle = TickStyle.None,
                Value = 100 - brightness.Value,
            };

            var valueBox = new TextBox()
            {
                Parent = trackBar,
                Top = 28,
                Left = 1,
                Enabled = false,
                BackColor = Color.White,
                TextAlign = HorizontalAlignment.Center,
                BorderStyle = BorderStyle.None,
                Text = (100 - brightness.Value).ToString()
            };

            trackBar.ValueChanged += (o, s) =>
                {
                    // invert for "brightness" value
                    brightness.Value = 100 - trackBar.Value;
                    valueBox.Text = trackBar.Value.ToString();
                };
        }
    }
}