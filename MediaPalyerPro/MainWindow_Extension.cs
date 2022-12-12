using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MediaPalyerPro
{
    public partial class MainWindow : Window
    {
        public void SetVolume(float volume)
        {
            MiddlePlayer.SetVolume(volume);
            BackgroundPlayer.SetVolume(volume);
            ForegroundPlayer.SetVolume(volume);
        }

        /// <summary>
        /// 音量增加一次
        /// </summary>
        public void VolumeUp()
        {
            MiddlePlayer.VolumeUp();
            BackgroundPlayer.VolumeUp();
            ForegroundPlayer.VolumeUp();
        }
        /// <summary>
        /// 音量减少一次
        /// </summary>
        public void VolumeDown()
        {
            MiddlePlayer.VolumeDown();
            BackgroundPlayer.VolumeDown();
            ForegroundPlayer.VolumeDown();
        }


        public void Sleep(int ms)
        {
            System.Threading.Thread.Sleep(ms);
        }

        public void TestEcho(string message = null)
        {
            if (String.IsNullOrWhiteSpace(message))
                Log.Info("This is test message, Hello World ...");
            else
                Log.Info(message);
        }
    }
}
