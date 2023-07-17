using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SpaceCG.Generic;

namespace MediaPlayerPro
{
    public class BackgroundConvert : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var eventType = (TraceEventType)value;
            return GetConsoleColor(eventType);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static SolidColorBrush GetConsoleColor(TraceEventType eventType)
        {
            return eventType == TraceEventType.Verbose ? Brushes.Green :
                eventType == TraceEventType.Information ? Brushes.WhiteSmoke :
                eventType == TraceEventType.Warning ? Brushes.Yellow :
                eventType == TraceEventType.Error ? Brushes.Red :
                eventType == TraceEventType.Critical ? Brushes.DarkRed :
                eventType == TraceEventType.Start ? Brushes.Cyan :
                eventType == TraceEventType.Stop ? Brushes.Cyan :
                eventType == TraceEventType.Suspend ? Brushes.Magenta :
                eventType == TraceEventType.Resume ? Brushes.Magenta :
                eventType == TraceEventType.Transfer ? Brushes.LightYellow :
                Brushes.Gray;
        }
    }

    public class LevelConvert : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var eventType = (TraceEventType)value;
            return TextFileStreamTraceListener.GetEventTypeChars(eventType);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    /// <summary>
    /// LoggerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LoggerWindow : Window
    {
        static readonly LoggerTrace Logger = new LoggerTrace();

        private ObservableCollection<TraceEventArgs> TraceEventList = new ObservableCollection<TraceEventArgs>();

        public LoggerWindow()
        {
            InitializeComponent();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            switch(e.Key)
            {
                case Key.Escape:
                    DataGrid_Logger.SelectedIndex = -1;
                    DataGrid_Logger.ScrollIntoView(TraceEventList[TraceEventList.Count - 1]);
                    break;

                case Key.Up:
                case Key.PageUp:
                    if(DataGrid_Logger.SelectedIndex >= 0)
                    {
                        DataGrid_Logger.SelectedIndex--;
                    }
                    break;

                case Key.Down:
                case Key.PageDown:
                    if (DataGrid_Logger.SelectedIndex >= 0)
                    {
                        DataGrid_Logger.SelectedIndex++;
                    }
                    break;

            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e); 
            //LoggerTrace.FileTraceEvent -= LoggerTrace_ConsoleTraceEvent;
            LoggerTrace.ConsoleTraceEvent -= LoggerTrace_ConsoleTraceEvent;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            DataGrid_Logger.ItemsSource = TraceEventList;
            //LoggerTrace.FileTraceEvent -= LoggerTrace_ConsoleTraceEvent;
            LoggerTrace.ConsoleTraceEvent += LoggerTrace_ConsoleTraceEvent;
        }

        private void LoggerTrace_ConsoleTraceEvent(object sender, TraceEventArgs e)
        {
            this.Dispatcher.InvokeAsync(() =>
            {
                TraceEventList.Add(e);                
                if (DataGrid_Logger.SelectedIndex < 0) DataGrid_Logger.ScrollIntoView(e);
            });
        }
    }
}
