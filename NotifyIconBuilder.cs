using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ModernNotifyIcon.Theme;

namespace PowerDimmer
{
    // from: https://github.com/Sharp0802/ModernNotifyIcon
    public class NotifyIconBuilder
    {
        private ContextMenuStripBuilder StripBuilder { get; } = new();

        protected NotifyIconBuilder()
        {
        }

        public static NotifyIconBuilder Create()
        {
            return new NotifyIconBuilder();
        }

        public NotifyIconBuilder Configure(Action<ContextMenuStripBuilder> builder)
        {
            builder(StripBuilder);
            return this;
        }

        public NotifyIcon Build(Icon? icon)
        {
            return new NotifyIcon
            {
                Icon = icon,
                ContextMenuStrip = StripBuilder.Build()
            };
        }
    }

    public class ContextMenuStripBuilder
    {
        public List<ToolStripItem> Items { get; } = new();

        public ContextMenuStripBuilder AddItem(ToolStripItem item)
        {
            Items.Add(item);
            return this;
        }

        public ContextMenuStripBuilder AddText(string text) => AddItem(new ToolStripMenuItem(text));

        public ContextMenuStripBuilder AddSeparator() => AddItem(new ToolStripSeparator { Margin = new Padding(0, 2, 0, 2) });

        public ContextMenuStripBuilder AddToggle(Action<ToggleGenerateOption> option)
        {
            var toggle = new ToolStripMenuItem();
            var optionRef = new ToggleGenerateOption(toggle);
            option.Invoke(optionRef);
            toggle.Text = optionRef.Text;
            toggle.Checked = optionRef.Checked;
            toggle.Click += (_, _) => optionRef.InvokeHandlers(toggle.Checked = !toggle.Checked);
            return AddItem(toggle);
        }

        public ContextMenuStripBuilder AddButton(Action<ButtonGenerateOption> option)
        {
            var button = new ToolStripMenuItem();
            var optionRef = new ButtonGenerateOption(button);
            option.Invoke(optionRef);
            button.Text = optionRef.Text;
            button.Click += (_, _) => optionRef.InvokeHandlers();
            return AddItem(button);
        }

        public ContextMenuStripBuilder AddSubmenu(string text, Action<ContextMenuStripBuilder> option)
        {
            var optionRef = new ContextMenuStripBuilder();
            option.Invoke(optionRef);
            var button = new ToolStripMenuItem(text)
            {
                DropDown = optionRef.Build()
            };
            return AddItem(button);
        }

        public ThemeReferencedContextMenuStrip Build()
        {
            const int padding = 5;

            var strip = new ThemeReferencedContextMenuStrip { Spacing = padding };
            var array = Items.ToArray();
            for (var i = 0; i < array.Length; ++i)
            {
                array[i].Padding = new Padding(0, padding, 0, padding);
                array[i].Margin += new Padding(0, i == 0 ? padding : 0, 0, i == array.Length - 1 ? padding : 0);
            }

            strip.Items.AddRange(array);
            return strip;
        }
    }
    public class GenerateOption<T> where T : GenerateOption<T>
    {
        public delegate void ConfigureItemHandler(ToolStripMenuItem item);

        public ToolStripMenuItem Item { get; private set; }

        public GenerateOption(ToolStripMenuItem item)
        {
            Item = item;
        }

        public T ConfigureItem(ConfigureItemHandler handler)
        {
            handler(Item);
            return (T)this;
        }
    }

    public sealed class ButtonGenerateOption : GenerateOption<ButtonGenerateOption>
    {
        public string? Text { get; private set; }

        public event Action? Toggled;
        public ButtonGenerateOption(ToolStripMenuItem item) : base(item)
        {
        }

        public ButtonGenerateOption SetText(string text)
        {
            Text = text;
            return this;
        }

        public ButtonGenerateOption AddHandler(Action handler)
        {
            Toggled += handler;
            return this;
        }

        internal void InvokeHandlers()
        {
            Toggled?.Invoke();
        }
    }

    public sealed class ToggleGenerateOption : GenerateOption<ToggleGenerateOption>
    {
        public delegate void ToggleEventHandler(bool toggled);

        public string? Text { get; private set; }

        public bool Checked { get; private set; } = false;


        public event ToggleEventHandler? Toggled;

        public ToggleGenerateOption(ToolStripMenuItem item) : base(item)
        {
        }

        public ToggleGenerateOption SetText(string text)
        {
            Text = text;
            return this;
        }

        public ToggleGenerateOption SetChecked(bool _checked)
        {
            Checked = _checked;
            return this;
        }

        public ToggleGenerateOption AddHandler(ToggleEventHandler handler)
        {
            Toggled += handler;
            return this;
        }

        internal void InvokeHandlers(bool check)
        {
            Toggled?.Invoke(check);
        }
    }
}