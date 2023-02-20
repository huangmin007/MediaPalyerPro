using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

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

        /// <summary>
        /// 临近的ID项，下一个项
        /// </summary>
        public void NextItem()
        {
            if (CurrentItem == null) return;
            if (int.TryParse(CurrentItem.Attribute("ID")?.Value, out int id))
            {
                LoadItem(id + 1);
            }
        }
        /// <summary>
        /// 临近的ID项，上一个项
        /// </summary>
        public void PrevItem()
        {
            if (CurrentItem == null) return;
            if (int.TryParse(CurrentItem.Attribute("ID")?.Value, out int id))
            {
                LoadItem(id - 1);
            }
        }
        /// <summary>
        /// 下一个节点
        /// </summary>
        public void NextNode()
        {
            if (CurrentItem == null) return;

            if (CurrentItem.NextNode != null)
                LoadItem((XElement)CurrentItem.NextNode);
            else
                LoadItem((XElement)(CurrentItem.Parent.FirstNode));
        }
        /// <summary>
        /// 上一个节点
        /// </summary>
        public void PrevNode()
        {
            if (CurrentItem == null) return;
            if (CurrentItem.PreviousNode != null)
                LoadItem((XElement)CurrentItem.PreviousNode);
            else
                LoadItem((XElement)(CurrentItem.Parent.LastNode));
        }
        /// <summary>
        /// 全局播放暂停
        /// </summary>
        public void PlayPause()
        {
            if (ForegroundPlayer.Visibility == Visibility.Visible)
            {
                if (ForegroundPlayer.IsPaused)
                    ForegroundPlayer.Play();
                else
                    ForegroundPlayer.Pause();
                return;
            }
            if (MiddlePlayer.Visibility == Visibility.Visible)
            {
                if (MiddlePlayer.IsPaused)
                    MiddlePlayer.Play();
                else
                    MiddlePlayer.Pause();
                return;
            }
            if (BackgroundPlayer.Visibility == Visibility.Visible)
            {
                if (BackgroundPlayer.IsPaused)
                    BackgroundPlayer.Play();
                else
                    BackgroundPlayer.Pause();
                return;
            }
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
