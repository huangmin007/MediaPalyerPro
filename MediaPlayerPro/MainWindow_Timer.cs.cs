using System.Timers;
using System.Windows;
using System.Xml.Linq;

namespace MediaPlayerPro
{
    public partial class MainWindow : Window
    {
        private Timer Timer;
        private int CurrentTimerCount = 0;

        protected int TimerNextItemID = -1;
        protected int TargetTimerCount = -1;

        private void InitializeTimer(XElement timerElement)
        {
            if (timerElement == null) return;

            TimerNextItemID = -1;
            TargetTimerCount = -1;
            CurrentTimerCount = 0;

            if (Timer == null)
            {
                Timer = new Timer();
                Timer.Interval = 1000;
                Timer.Elapsed += Timer_Elapsed;
            }

#if DEBUG
            TargetTimerCount = 10;
#else
            if(int.TryParse(timerElement.Attribute("Count")?.Value, out int count))
            {
                TargetTimerCount = count;
            }
#endif
            if (int.TryParse(timerElement.Attribute("LoadItem")?.Value, out int nextId))
            {
                TimerNextItemID = nextId;
            }

            if (TargetTimerCount > 0) Timer?.Start();
            else Timer?.Stop();
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
                if (CurrentItemID == TimerNextItemID) return;

                this.Dispatcher.InvokeAsync(() =>
                {
                    Log.Info($"Current Timer Count: {CurrentTimerCount}  Load Item ID: {TimerNextItemID}  ListAutoLoop: {ListAutoLoop}");
                    LoadItem(TimerNextItemID);
                });
            }
        }

        /// <summary>
        /// 重置 Timer 计数器
        /// </summary>
        public void RestartTimer()
        {
            if (Timer == null) return;

            CurrentTimerCount = 0;
            if (!Timer.Enabled) Timer.Start();
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

            RestartTimer();
        }

    }
}