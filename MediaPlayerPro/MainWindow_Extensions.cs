using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

namespace MediaPlayerPro
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 设置列表循环
        /// </summary>
        /// <param name="loop"></param>
        public void SetListLoop(bool loop)
        {
            ListAutoLoop = loop;
        }

        /// <summary>
        /// 设置播放器音量, 0.0~1.0f
        /// </summary>
        /// <param name="volume"></param>
        public void SetVolume(float volume)
        {
            CenterPlayer.Volume = volume;
            BackgroundPlayer.Volume = volume;
            ForegroundPlayer.Volume = volume;
        }

        /// <summary>
        /// 音量增加 10%
        /// </summary>
        public void VolumeUp()
        {
            float volume = CurrentPlayer.Volume;
            volume = volume + 0.1f >= 1.0f ? 1.0f : volume + 0.1f;

            CenterPlayer.Volume = volume;
            BackgroundPlayer.Volume = volume;
            ForegroundPlayer.Volume = volume;
        }
        /// <summary>
        /// 音量减小 10%
        /// </summary>
        public void VolumeDown()
        {
            float volume = CurrentPlayer.Volume;
            volume = volume - 0.1f <= 0.0f ? 0.0f : volume - 0.1f;

            CenterPlayer.Volume = volume;
            BackgroundPlayer.Volume = volume;
            ForegroundPlayer.Volume = volume;
        }

        /// <summary>
        /// 临近的 ID 项，下一个项
        /// </summary>
        public void NextItem()
        {
            if (CurrentItem == null) return;

            //向下查找 Item
            int nextId = CurrentItemID + 1;
            IEnumerable<XElement> nextItems = from item in ItemElements
                                              where item.Attribute("ID")?.Value == nextId.ToString()
                                              select item;
            if (nextItems?.Count() == 1)
            {
                LoadItem(nextId);
                return;
            }

            //向上查找 Item
            int prevId = CurrentItemID - 1;
            while (true)
            {
                IEnumerable<XElement> prevItems = from item in ItemElements
                                                  where item.Attribute("ID")?.Value == prevId.ToString()
                                                  select item;

                if (prevItems?.Count() <= 0)
                {
                    LoadItem(prevId + 1);
                    return;
                }

                prevId --;
            }
        }
        /// <summary>
        /// 临近的 ID 项，上一个项
        /// </summary>
        public void PrevItem()
        {
            if (CurrentItem == null) return;

            //向上查找 Item
            int prevId = CurrentItemID - 1;
            IEnumerable<XElement> prevItems = from item in ItemElements
                                              where item.Attribute("ID")?.Value == prevId.ToString()
                                              select item;
            if (prevItems?.Count() == 1)
            {
                LoadItem(prevId);
                return;
            }

            //向下查找 Item
            int nextId = CurrentItemID + 1;
            while(true)
            {
                IEnumerable<XElement> nextItems = from item in ItemElements
                                                  where item.Attribute("ID")?.Value == nextId.ToString()
                                                  select item;
                if (nextItems?.Count() <= 0)
                {
                    LoadItem(nextId - 1);
                    return;
                }

                nextId ++;
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
                LoadItem((XElement)CurrentItem.Parent.FirstNode);
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
                LoadItem((XElement)CurrentItem.Parent.LastNode);
        }
        /// <summary>
        /// 播放暂停当前视频
        /// </summary>
        public void PlayPause()
        {
            if (CurrentPlayer == null) return;
            if (CurrentPlayer.IsPaused) CurrentPlayer.Play();
            else CurrentPlayer.Pause();
        }

        /// <summary>
        /// 播放当前视频
        /// </summary>
        public void Play()
        {
            if (CurrentPlayer == null) return;
            if (CurrentPlayer.IsPaused) CurrentPlayer.Play();
        }

        /// <summary>
        /// 暂停当前视频
        /// </summary>
        public void Pause()
        {
            if (CurrentPlayer == null) return;
            CurrentPlayer.Pause();
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