using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Sttplay.MediaPlayer;

namespace MediaPalyerPro
{
    public partial class MainWindow : Window
    {
        private void Window_Drop(object sender, DragEventArgs e)
        {
            CenterContainer.Visibility = Visibility.Hidden;
            BackgroundContainer.Visibility = Visibility.Hidden;

            try
            {
                var fileName = ((Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();

                if (IsVideoFile(fileName))
                {
                    ForegroundContainer.Visibility = Visibility.Visible;
                    ForegroundPlayer.Visibility = Visibility.Visible;

                    ForegroundPlayer.Open(MediaType.Link, fileName);
                }
                else if (IsImageFile(fileName))
                {
                    ForegroundPlayer.Close();
                    ForegroundContainer.Visibility = Visibility.Visible;
                    ForegroundPlayer.Visibility = Visibility.Visible;

                    ForegroundPlayer.Source = new BitmapImage(new Uri(fileName, UriKind.RelativeOrAbsolute));
                }
                else
                {
                    MessageBox.Show($"不支持的文件类型 {fileName}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
