using System.Configuration;
using System.Net.Sockets;
using System.Timers;
using System.Windows;

namespace MediaPalyerPro
{
    public partial class MainWindow : Window
    {
        private Timer Timer;

        private int CurrentTimerCount = 0;

        private int TimerNextItemID = -1;
        private int TargetTimerCount = 480;

        private void InitializeTimer()
        {
            Timer = new Timer();
            Timer.Enabled = true;
            Timer.Interval = 1000;
            Timer.Elapsed += Timer_Elapsed;
            Timer.Start();

#if DEBUG
            TargetTimerCount = 10;
#else
            if (int.TryParse(ConfigurationManager.AppSettings["Timer.Count"], out int count))
                TargetTimerCount = count;
#endif
            if (int.TryParse(ConfigurationManager.AppSettings["Timer.LoadItem"], out int id))
                TimerNextItemID = id;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (RootConfiguration == null || CurrentItem == null) return;
            CurrentTimerCount++;            

            if (TargetTimerCount > 0 && CurrentTimerCount >= TargetTimerCount)
            {
                if (Log.IsDebugEnabled) Log.Debug($"TimerEvent::{CurrentTimerCount}/{TargetTimerCount}");

                Timer.Stop();
                CurrentTimerCount = 0;

                if (int.TryParse(CurrentItem.Attribute("ID")?.Value, out int id))
                {
                    if (TimerNextItemID == id) return;
                }

                this.Dispatcher.Invoke(() =>
                {
                    Log.Info($"Current Timer Count: {CurrentTimerCount}  Load Item ID: {TimerNextItemID}  ListAutoLoop: {ListAutoLoop}");
                    LoadItem(TimerNextItemID);
                });
            }
        }

        /// <summary>
        /// 重置 Timer 计数器
        /// </summary>
        public void TimerReset()
        {
            if (Timer == null) return;

            ListAutoLoop = false;
            CurrentTimerCount = 0;
            if(!Timer.Enabled)   Timer.Start();
        }

        /// <summary>
        /// 定时加载 NextItem
        /// </summary>
        /// <param name="timerCount"></param>
        /// <param name="id"></param>
        public void LoadNextItem(int timerCount, int id)
        {
            TimerNextItemID = id;
            TargetTimerCount = timerCount;

            TimerReset();
        }

        public static bool IsOnline(TcpClient _TcpClient)
        {
            if (_TcpClient?.Client == null) return false;
            return !((_TcpClient.Client.Poll(1000, SelectMode.SelectRead) && (_TcpClient.Client.Available == 0)) || !_TcpClient.Client.Connected);
        }

    }
}
