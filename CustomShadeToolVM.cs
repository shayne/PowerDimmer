using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PowerDimmer
{
    public class CustomShadeToolVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        #region props
        private double leftPos;
        public double LeftPos
        {
            get => leftPos;
            set
            {
                leftPos = value;
                OnPropertyChanged();
            }
        }

        private double topPos;
        public double TopPos
        {
            get => topPos;
            set
            {
                topPos = value;
                OnPropertyChanged();
            }
        }

        private double shadeWidth;
        public double ShadeWidth
        {
            get => shadeWidth;
            set
            {
                shadeWidth = value;
                OnPropertyChanged();
            }
        }

        private double shadeHeight;
        public double ShadeHeight
        {
            get => shadeHeight;
            set
            {
                shadeHeight = value;
                OnPropertyChanged();
            }
        }

        public double ShadeOpacity;

        #endregion
        public bool isDragging;
        public Point dragStartPos;

        public CustomShadeToolVM()
        {
        }
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public void UpdateRect(Point endPoint)
        {
            LeftPos = dragStartPos.X;
            TopPos = dragStartPos.Y;
            ShadeWidth = endPoint.X - LeftPos;
            ShadeHeight = endPoint.Y - TopPos;
        }
    }
}
