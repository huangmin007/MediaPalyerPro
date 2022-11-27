using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows.Media;
using log4net.Core;
using SpaceCG.WindowsAPI.User32;

namespace SpaceCG.Log4Net.Controls
{
    /// <summary>
    /// 独立的日志窗体对象
    /// <para>使用 Ctrl+L 显示激活窗体/隐藏窗体 </para>
    /// </summary>
    public partial class LoggerWindow : Window
    {
        private IntPtr Handle;
        private HwndSource HwndSource;

        /// <summary> TextBox </summary>
        protected TextBox TextBox;
        /// <summary> ListView </summary>
        protected ListView ListView;
        /// <summary> ListBoxAppender </summary>
        protected ListBoxAppender ListBoxAppender;

        /// <summary>
        /// 最大显示行数
        /// </summary>
        protected int MaxLines = 512;

        /// <summary>
        /// Logger Window
        /// <para>使用 Ctrl+L 显示激活窗体/隐藏窗体 </para>
        /// </summary>
        /// <param name="maxLines"></param>
        public LoggerWindow(int maxLines = 512)
        {
            this.MaxLines = maxLines;
            OnInitializeControls();
        }
        /// <inheritdoc/>
        protected override void OnClosing(CancelEventArgs e)
        {
            this.Hide();
            base.OnClosing(e);
            if (HwndSource != null)  e.Cancel = true;
        }
        /// <inheritdoc/>
        protected override void OnClosed(EventArgs e)
        {
            if (HwndSource != null)
            {
                HwndSource.Dispose();
                HwndSource = null;
            }

            if (Handle != null)
            {
                bool result = User32.UnregisterHotKey(Handle, 0);
                result = result || User32.UnregisterHotKey(Handle, 1);
                //Console.WriteLine("Logger Window UnregisterHotKey State:{0}", result);

                Handle = IntPtr.Zero;
            }

            ListView.SelectionChanged -= ListView_SelectionChanged;
        }

        /// <summary>
        /// 初使化 UI 控件
        /// </summary>
        protected void OnInitializeControls()
        {
            //Grid
            Grid grid = new Grid();
            //grid.ShowGridLines = true;
            grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(0.85, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(0.15, GridUnitType.Star) });

            //ListView
            this.ListView = new ListView();
            this.ListView.SelectionChanged += ListView_SelectionChanged;
            this.ListBoxAppender = new ListBoxAppender(ListView, MaxLines);

            //GridSplitter
            GridSplitter splitter = new GridSplitter()
            {
                Height = 4.0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            //TextBox
            this.TextBox = new TextBox()
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.WrapWithOverflow,
            };
            TextBox.MouseDoubleClick += (s, e) => { ClearTextBox(); };
            TextBox.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);

            grid.Children.Add(ListView);
            grid.Children.Add(splitter);
            grid.Children.Add(TextBox);
            Grid.SetRow(ListView, 0);
            Grid.SetRow(splitter, 1);
            Grid.SetRow(TextBox, 2);

            this.Title = "Local Logger Window (Ctrl+L 隐藏/唤起)";
            this.Width = 1280;
            this.Height = 720;
            this.Content = grid;
            this.Loaded += LoggerWindow_Loaded;

            this.WindowState = WindowState.Minimized;
            this.Show();
        }

        /// <summary>
        /// 添加日志事件对象
        /// </summary>
        /// <param name="loggingEvent"></param>
        public void AppendLoggingEvent(LoggingEvent loggingEvent)
        {
            this.ListBoxAppender.AppendLoggingEvent(loggingEvent);
        }
        /// <summary>
        /// 清除日志列表内容
        /// </summary>
        public void ClearLogger()
        {
            this.ListView.Items.Clear();
        }

        /// <summary>
        /// 清除 TextBox 内容
        /// </summary>
        public void ClearTextBox()
        {
            this.TextBox.Text = "";
            this.TextBox.Foreground = Brushes.Black;
            this.TextBox.FontWeight = FontWeights.Normal;
        }

        /// <summary>
        /// ListView Select Changed Handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListViewItem item = (ListViewItem)this.ListView.SelectedItem;
            
            if(item != null)
            {
                this.TextBox.Text = item.ToolTip.ToString();

                LoggingEvent logger = (LoggingEvent)item.Content;
                this.TextBox.Foreground = logger.Level >= Level.Error ? Brushes.Red : Brushes.Black;
                this.TextBox.FontWeight = logger.Level >= Level.Warn ? FontWeights.Black : FontWeights.Normal;
            }
            else
            {
                ClearTextBox();
            }
        }

        /// <summary>
        /// Window Loaded Event Handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LoggerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Handle = new WindowInteropHelper(this).Handle;

            bool result = User32.RegisterHotKey(Handle, 0, RhkModifier.CONTROL, VirtualKeyCode.VK_L);
            result = result || User32.RegisterHotKey(Handle, 1, RhkModifier.CONTROL, VirtualKeyCode.VK_M);
            //Console.WriteLine("Logger Window RegisterHotKey State:{0}", result);

            if (result)
            {
                HwndSource = HwndSource.FromHwnd(Handle);
                HwndSource.AddHook(WindowProcHandler);
            }

            this.InsertAfter(SwpInsertAfter.HWND_TOPMOST);
            //User32.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, SwpFlags.NOMOVE | SwpFlags.NOSIZE);

            this.Hide();
        }

        /// <summary>
        /// Window Process Handler
        /// </summary>
        /// <param name="hwnd"></param>
        /// <param name="msg"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <param name="handled"></param>
        /// <returns></returns>
        protected IntPtr WindowProcHandler(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            MessageType msgType = (MessageType)msg;
            if(msgType == MessageType.WM_HOTKEY)
            {
                RhkModifier rhk = (RhkModifier)(lParam.ToInt32() & 0xFFFF);     //低双字节
                VirtualKeyCode key = (VirtualKeyCode)(lParam.ToInt32() >> 16);  //高双字节 key

                if(rhk == RhkModifier.CONTROL)
                {
                    if (key == VirtualKeyCode.VK_L)
                    {
                        if (this.WindowState == WindowState.Minimized || this.Visibility == Visibility.Hidden)
                        {
                            this.Show();
                            this.Activate();
                            this.WindowState = WindowState.Normal;
                        }
                        else
                        {
                            this.WindowState = WindowState.Minimized;
                            this.Hide();
                        }
                    }
                    if(key == VirtualKeyCode.VK_M)
                    {
                        // ...
                    }

                    handled = true;
                }
            }

            return IntPtr.Zero;
        }
    }
}
