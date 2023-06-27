using System;
using System.Windows;
using Sttplay.MediaPlayer;

namespace MediaPlayerPro
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Window Drop
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Drop(object sender, DragEventArgs e)
        {
            try
            {
                var fileName = ((Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();

                if (MainWindowExtensions.IsVideoFile(fileName) || MainWindowExtensions.IsImageFile(fileName))
                {
                    ForegroundPlayer.Close();
                    ForegroundPlayer.Visibility = Visibility.Visible;
                    ForegroundContainer.Visibility = Visibility.Visible;

                    ForegroundPlayer.Open(MediaType.Link, fileName);
                }
                else
                {
                    MessageBox.Show($"不支持的文件类型 {fileName}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MiddleContainer.Visibility = Visibility.Hidden;
            BackgroundContainer.Visibility = Visibility.Hidden;
        }
    }
}