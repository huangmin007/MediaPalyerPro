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
        /// <inheritdoc/>
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            DragMove();
            base.OnMouseLeftButtonDown(e);
        }

        /// <inheritdoc/>
        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);

            MiddlePlayer.Pause();
            BackgroundPlayer.Pause();
            MiddleGroup.Visibility = Visibility.Hidden;
            BackgroundGroup.Visibility = Visibility.Hidden;

            try
            {
                var fileName = ((Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();

                if (IsVideoFile(fileName))
                {
                    ForegroundGroup.Visibility = Visibility.Visible;
                    ForegroundPlayer.Visibility = Visibility.Visible;

                    ForegroundPlayer.Open(MediaType.Link, fileName);
                }
                else if (IsImageFile(fileName))
                {
                    ForegroundPlayer.Close();
                    ForegroundGroup.Visibility = Visibility.Visible;
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

        #region Static Functions
        public static bool IsVideoFile(String fileName)
        {
            if (String.IsNullOrWhiteSpace(fileName)) return false;

            FileInfo fileInfo = new FileInfo(fileName);
            if (!fileInfo.Exists) return false;
            String extension = fileInfo.Extension.ToUpper();

            switch (extension)
            {
                case ".MP3":    //音频格式
                case ".WAV":    //音频格式
                case ".MP4":
                case ".MOV":
                case ".M4V":
                case ".MKV":
                case ".MPG":
                case ".WEBM":
                case ".FLV":
                case ".F4V":
                case ".OGV":
                case ".TS":
                case ".MTS":
                case ".M2T":
                case ".M2TS":
                case ".3GP":
                case ".AVI":
                case ".WMV":
                case ".WTV":
                case ".MPEG":
                case ".RM":
                case ".RAM":
                    return true;
            }

            return false;
        }
        public static bool IsImageFile(String fileName)
        {
            if (String.IsNullOrWhiteSpace(fileName)) return false;

            FileInfo fileInfo = new FileInfo(fileName);
            if (!fileInfo.Exists) return false;
            String extension = fileInfo.Extension.ToUpper();

            switch (extension.ToUpper())
            {
                case ".JPG":
                case ".PNG":
                case ".BMP":
                case ".JPEG":
                    return true;
            }

            return false;
        }
        private static void OpenMediaFile(WPFSCPlayerPro player, String filename)
        {
            if (IsVideoFile(filename))
                player.Open(MediaType.Link);
            else if (IsImageFile(filename))
                player.Source = new BitmapImage(new Uri(filename, UriKind.RelativeOrAbsolute));
            else
                Log.Error($"打开文件 {filename} 失败，不支持的文件类型");
        }
        #endregion
    }
}
