using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Xml.Linq;
using Sttplay.MediaPlayer;

namespace MediaPlayerPro
{
    public partial class MainWindow : Window
    {
        private Dictionary<String, double> playerLastTimer = new Dictionary<string, double>();
        private Dictionary<String, IEnumerable<XElement>> playerRenderEvents = new Dictionary<string, IEnumerable<XElement>>();

        private void OnCaptureOpenCallbackEvent(WPFSCPlayerPro player, CaptureOpenResult result, string message, OpenCallbackContext context)
        {
            if (Log.IsDebugEnabled)
                Log.Debug($"WPFSCPlayerPro({player.Name}) On Capture Open Callback Event, Result: {result}  Message: {message}");

            if (result != CaptureOpenResult.SUCCESS) return;

            if (!playerLastTimer.ContainsKey(player.Name))  playerLastTimer.Add(player.Name, 0.0f);
            if (!playerRenderEvents.ContainsKey(player.Name)) playerRenderEvents.Add(player.Name, null);

            playerRenderEvents[player.Name] = null;
            if (CurrentItem?.Element(player.Name) != null)
            {
                IEnumerable<XElement> events = CurrentItem.Element(player.Name)?.Elements("Events");
                if (events.Count() > 0)
                {
                    playerRenderEvents[player.Name] = from ev in events
                                                      where ev.Attribute("Name")?.Value == "OnRenderFrame"
                                                      select ev;
                    if (playerRenderEvents[player.Name].Count() == 0) playerRenderEvents[player.Name] = null;
                }
            }
        }
        private void OnFirstFrameRenderEventHandler(WPFSCPlayerPro player)
        {
            if (Log.IsDebugEnabled)
                Log.Debug($"WPFSCPlayerPro({player.Name}) First Frame Render Evnet. URL: {player.Url}");

            //if (NetworkSlave != null)  //4字节心跳
            //    NetworkSlave.Send(SyncMessage, SyncMessage.Length - 4, 4);

            CallPlayerEvent(player, "OnFirstFrame");
            playerLastTimer[player.Name] = Math.Round(player.CurrentTime / 1000.0f, 2);
        }
        private void OnRenderVideoFrameEventHandler(WPFSCPlayerPro player)
        {
            CheckNetworkSyncStatus();

            if (playerRenderEvents[player.Name] != null)
            {
                double currentTime = Math.Round(player.CurrentTime / 1000.0f, 2);
                if (playerLastTimer[player.Name] == currentTime) return;

                //CallPlayerEvent(player, "OnRenderFrame", currentTime, playerLastTimer[player.Name]);
                CallPlayerEvent(player, "OnVideoRenderFrame", currentTime, playerLastTimer[player.Name]);
                playerLastTimer[player.Name] = currentTime;
            }
        }
        private void OnRenderAudioFrameEventHandler(WPFSCPlayerPro player)
        {
            this.Dispatcher.Invoke(() =>
            {
                if (playerRenderEvents[player.Name] != null)
                {
                    double currentTime = Math.Round(player.CurrentTime / 1000.0f, 2);
                    if (playerLastTimer[player.Name] == currentTime) return;

                    //CallPlayerEvent(player, "OnRenderFrame", currentTime, playerLastTimer[player.Name]);
                    CallPlayerEvent(player, "OnAudioRenderFrame", currentTime, playerLastTimer[player.Name]);
                    playerLastTimer[player.Name] = currentTime;
                }
            });

        }
        private void OnStreamFinishedEventHandler(WPFSCPlayerPro player)
        {
            if (Log.IsDebugEnabled)
                Log.Debug($"WPFSCPlayerPro({player.Name}) Stream Finish Event. URL: {player.Url}  ListAutoLoop: {ListAutoLoop}");

            //if (NetworkSlave != null)  //4字节心跳
            //    NetworkSlave.Send(SyncMessage, SyncMessage.Length - 4, 4);

            CallPlayerEvent(player, "OnLastFrame");

            if (ListAutoLoop) NextNode();
        }

    }
}
