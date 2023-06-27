using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using SpaceCG.Extensions;
using Sttplay.MediaPlayer;

namespace MediaPlayerPro
{
    public partial class MainWindow : Window
    {
        internal const string OnLastFrame = "OnLastFrame";
        internal const string OnFirstFrame = "OnFirstFrame";
        internal const string OnRenderFrame = "OnRenderFrame";
        internal const string OnVideoRenderFrame = "OnVideoRenderFrame";
        internal const string OnAudioRenderFrame = "OnAudioRenderFrame";

        private Dictionary<String, double> playerLastTimer = new Dictionary<string, double>();
        private Dictionary<String, IEnumerable<XElement>> playerRenderEvents = new Dictionary<string, IEnumerable<XElement>>();

        private void OnCaptureOpenCallbackEvent(WPFSCPlayerPro player, CaptureOpenResult result, string message, OpenCallbackContext context)
        {
            Log.Info($"WPFSCPlayerPro({player.Name}) On Capture Open Callback Event URL:{player.Url}, Result: {result}  Message: {message}");

            playerLastTimer.Remove(player.Name);
            playerRenderEvents.Remove(player.Name);
            if (result != CaptureOpenResult.SUCCESS) return;

            if (!playerLastTimer.ContainsKey(player.Name))  playerLastTimer.Add(player.Name, 0.0f);
            if (!playerRenderEvents.ContainsKey(player.Name)) playerRenderEvents.Add(player.Name, null);

            playerRenderEvents[player.Name] = null;
            if ((player.ToolTip as XElement) != null)
            {
                playerRenderEvents[player.Name] = from evt in ((XElement)player.ToolTip).Elements(XEvents)
                                                  let name = evt.Attribute("Name")?.Value
                                                  where name == OnRenderFrame || name == OnVideoRenderFrame || name == OnAudioRenderFrame
                                                  select evt;
                if (playerRenderEvents[player.Name]?.Count() == 0) playerRenderEvents[player.Name] = null;
            }
        }
        private void OnFirstFrameRenderEventHandler(WPFSCPlayerPro player)
        {
            double currentTime = Math.Round(player.CurrentTime / 1000.0f, 2);
            Log.Info($"WPFSCPlayerPro({player.Name}) First Frame Render Evnet. URL: {player.Url} CurrentTime: {currentTime:F2}");

            CallPlayerEvent(player, OnFirstFrame);
            playerLastTimer[player.Name] = currentTime;
        }
        private void OnRenderVideoFrameEventHandler(WPFSCPlayerPro player)
        {
            if (playerRenderEvents[player.Name] != null)
            {
                double currentTime = Math.Round(player.CurrentTime / 1000.0f, 2);               
                if (playerLastTimer[player.Name] == currentTime) return;
                
                CallPlayerEvent(player, OnRenderFrame, currentTime, playerLastTimer[player.Name]);
                CallPlayerEvent(player, OnVideoRenderFrame, currentTime, playerLastTimer[player.Name]);
                playerLastTimer[player.Name] = currentTime;
            }
        }
        private void OnRenderAudioFrameEventHandler(WPFSCPlayerPro player)
        {
            if (playerRenderEvents[player.Name] != null)
            {
                double currentTime = Math.Round(player.CurrentTime / 1000.0f, 2);
                if (playerLastTimer[player.Name] == currentTime) return;
                
                CallPlayerEvent(player, OnRenderFrame, currentTime, playerLastTimer[player.Name]);
                CallPlayerEvent(player, OnAudioRenderFrame, currentTime, playerLastTimer[player.Name]);
                playerLastTimer[player.Name] = currentTime;
            }
        }
        private void OnStreamFinishedEventHandler(WPFSCPlayerPro player)
        {
            double currentTime = Math.Round(player.CurrentTime / 1000.0f, 2);
            Log.Info($"WPFSCPlayerPro({player.Name}) Stream Finish Event. URL: {player.Url}  CurrentTime: {currentTime:F2}  ListAutoLoop: {ListAutoLoop}");

            CallPlayerEvent(player, OnLastFrame);
            if (ListAutoLoop) NextNode();
        }


        /// <summary>
        /// 执行配置事件
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="currentTime"></param>
        /// <param name="lastTime"></param>
        protected void CallPlayerEvent(WPFSCPlayerPro player, String eventName, double currentTime = -1.0f, double lastTime = -1.0f)
        {
            IEnumerable<XElement> events = from evt in (player.ToolTip as XElement)?.Elements(XEvents)
                                           where evt.Attribute("Name")?.Value == eventName
                                           select evt;
            if (events?.Count() == 0) return;

            foreach (XElement element in events.Elements())
            {
                if (currentTime >= 0 && lastTime >= 0)
                {
                    if (!double.TryParse(element.Parent.Attribute("Position")?.Value, out double position)) continue;
                    if (!(position <= currentTime && position > lastTime)) continue;
                    Log.Info($"WPFSCPlayerPro({player.Name}) Render Frame Evnet ({eventName})  CurrentTimer: {currentTime:F2}");
                }

                Task.Run(() =>
                {
                    if (element.Name.LocalName == "Action")
                    {
                        ControlInterface.TryParseControlMessage(element, out object returnResult);
                    }
                    else
                    {
                        this.Dispatcher.InvokeAsync(() => InstanceExtensions.SetInstancePropertyValues(this, element));
                    }
                });
            }
        }

    }
}
