using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Xml.Linq;
using SpaceCG.Extensions;

namespace MediaPlayerPro
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 加载指定项的内容
        /// </summary>
        /// <param name="id">指定 ID 属性值</param>
        public void LoadItem(int id)
        {
            if (CurrentItemID == id) return;
            if (ItemElements?.Count() <= 0) return;
            IEnumerable<XElement> items = from item in ItemElements
                                          where item.Attribute("ID")?.Value.Trim() == id.ToString()
                                          select item;
            if (items?.Count() != 1)
            {
                Log.Warn($"配置项列表中不存在指定的 ID: {id} 项");
                return;
            }

            Log.Info($"Ready Load Item ID: {id}");
            LoadItem(items.First());
        }
        /// <summary>
        /// 加载指定项的内容
        /// </summary>
        /// <param name="name">指定 Name 属性值</param>
        public void LoadItem(string name)
        {
            if (ItemElements?.Count() <= 0) return;
            IEnumerable<XElement> items = from item in ItemElements
                                          where item.Attribute("Name")?.Value.Trim() == name
                                          select item;
            if (items?.Count() != 1)
            {
                Log.Warn($"配置项列表中不存在指定的 Name: {name} 项");
                return;
            }

            Log.Info($"Ready Load Item Name: {name}");
            LoadItem(items.First());
        }

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
            MiddlePlayer.Volume = volume;
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

            MiddlePlayer.Volume = volume;
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

            MiddlePlayer.Volume = volume;
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
        }
        /// <summary>
        ///  临近的 ID 项，下一个项
        /// </summary>
        /// <param name="logicLoop">逻辑循环，如果没找到下一个项，则往上查找同级第一个项</param>
        public void NextItem(bool logicLoop)
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
            if (!logicLoop) return;

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

                prevId--;
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
        }
        /// <summary>
        /// 临近的 ID 项，上一个项
        /// </summary>
        /// <param name="logicLoop">逻辑循环，如果没找到上一个项，则往下查找同级最后一个项</param>
        public void PrevItem(bool logicLoop)
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
            if (!logicLoop) return;

            //向下查找 Item
            int nextId = CurrentItemID + 1;
            while (true)
            {
                IEnumerable<XElement> nextItems = from item in ItemElements
                                                  where item.Attribute("ID")?.Value == nextId.ToString()
                                                  select item;
                if (nextItems?.Count() <= 0)
                {
                    LoadItem(nextId - 1);
                    return;
                }

                nextId++;
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
            if (CurrentPlayer == null || !MainWindowExtensions.IsVideoFile(CurrentPlayer.Url)) return;
            if (CurrentPlayer.IsPaused) CurrentPlayer.Play();
            else CurrentPlayer.Pause();
        }
        /// <summary>
        /// 播放当前视频
        /// </summary>
        public void Play()
        {
            if (CurrentPlayer == null || !MainWindowExtensions.IsVideoFile(CurrentPlayer.Url)) return;
            if (CurrentPlayer.IsPaused) CurrentPlayer.Play();
        }
        /// <summary>
        /// 暂停当前视频
        /// </summary>
        public void Pause()
        {
            if (CurrentPlayer == null || !MainWindowExtensions.IsVideoFile(CurrentPlayer.Url)) return;
            CurrentPlayer.Pause();
        }


        /// <summary>
        /// Call Event Name
        /// </summary>
        /// <param name="eventName"></param>
        public void CallEventName(string eventName)
        {
            IEnumerable<XElement> events = from evt in RootConfiguration.Descendants(XEvent)
                                           where evt.Attribute("Name")?.Value == eventName
                                           select evt;
            Log.Info($"CallEventName: Name {eventName}  Count: {events?.Count()}");
            CallEventElements(events);
        }
        /// <summary>
        /// Call Event Name From Item
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="itemID"></param>
        public void CallEventName(string eventName, string itemID)
        {
            IEnumerable<XElement> events = from item in RootConfiguration.Descendants("Item")
                                           where item.Attribute("ID")?.Value == itemID
                                           from evt in item.Descendants(XEvent)
                                           where evt.Attribute("Name")?.Value == eventName
                                           select evt;
            Log.Info($"CallEventName: Name {eventName}  ItemID:{itemID}  Count: {events?.Count()}");
            CallEventElements(events);
        }

        /// <summary>
        /// 调用 指定项 => 指定按扭容器 => 指定的按扭 => 事件或属性
        /// </summary>
        /// <param name="buttonName"></param>
        /// <param name="buttonContainer"></param>
        /// <param name="itemID">页面ID</param>
        public void CallButtonEvent(string buttonName, string buttonContainer, string itemID)
        {
            IEnumerable<XElement> events = from item in ItemElements
                                           where item.Attribute("ID")?.Value.Trim() == itemID
                                           from container in item.Elements(buttonContainer)
                                           from evt in container.Elements(XEvent)
                                           where evt.Attribute(XType)?.Value.Trim() == "Click" && evt.Attribute("Element")?.Value.Trim() == buttonName
                                           select evt;

            Log.Info($"CallButtonEvent: ItemID: {itemID}  ButtonContainer:{buttonContainer}  ButtonName: {buttonName}  Count: {events?.Count()}");

            CallEventElements(events);
        }
        /// <summary>
        /// 调用 当前项(<see cref="CurrentItemID"/>) => 指定按扭容器 => 指定的按扭 => 事件或属性
        /// </summary>
        /// <param name="buttonName"></param>
        /// <param name="buttonContainer"></param>
        public void CallButtonEvent(string buttonName, string buttonContainer) => CallButtonEvent(buttonName, buttonContainer, CurrentItemID.ToString());
        /// <summary>
        /// 调用 当前项(<see cref="CurrentItemID"/>) => 当前最顶端的显示按扭容器 => 指定的按扭 => 事件或属性
        /// </summary>
        /// <param name="buttonName"></param>
        public void CallButtonEvent(string buttonName)
        {
            var btnContainer = ForegroundContainer.Visibility == Visibility.Visible && ForegroundButtons.Visibility == Visibility.Visible ? ForegroundButtons :
                               MiddleContainer.Visibility == Visibility.Visible && MiddleButtons.Visibility == Visibility.Visible ? MiddleButtons :
                               BackgroundContainer.Visibility == Visibility.Visible && BackgroundButtons.Visibility == Visibility.Visible ? BackgroundButtons : null;

            CallButtonEvent(buttonName, btnContainer.Name, CurrentItemID.ToString());
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

        /// <summary>
        /// Call Event Elements
        /// </summary>
        /// <param name="events"></param>
        protected void CallEventElements(IEnumerable<XElement> events)
        {
            if (events?.Count() == 0) return;

            stopwatch.Restart();
            foreach (XElement element in events.Elements())
            {
                if (element.Name.LocalName == XAction)
                {
                    ControlInterface.TryParseControlMessage(element);
                }
                else
                {
                    InstanceExtensions.SetInstancePropertyValues(this, element);
                }
            }

            stopwatch.Stop();
            Log.Info($"Call Event Elements({events?.Count()}) use {stopwatch.ElapsedMilliseconds} ms");
        }

    }
}