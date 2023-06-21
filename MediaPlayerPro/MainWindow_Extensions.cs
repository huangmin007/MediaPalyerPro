﻿using System;
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

        }

        /// <summary>
        /// 设置播放器音量, 0.0~1.0f
        /// </summary>
        /// <param name="volume"></param>
        public void SetVolume(float volume)
        {
            //CenterPlayer.SetVolume(volume);
            //BackgroundPlayer.SetVolume(volume);
            //ForegroundPlayer.SetVolume(volume);
        }

        /// <summary>
        /// 音量增加 10%
        /// </summary>
        public void VolumeUp()
        {
            //CenterPlayer.VolumeUp();
            //BackgroundPlayer.VolumeUp();
            //ForegroundPlayer.VolumeUp();
        }
        /// <summary>
        /// 音量减小 10%
        /// </summary>
        public void VolumeDown()
        {
            //CenterPlayer.VolumeDown();
            //BackgroundPlayer.VolumeDown();
            //ForegroundPlayer.VolumeDown();
        }

        /// <summary>
        /// 临近的 ID 项，下一个项
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
        /// 临近的 ID 项，上一个项
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