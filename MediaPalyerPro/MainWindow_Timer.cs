using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;

namespace MediaPalyerPro
{
    public partial class MainWindow : Window
    {
        private Timer Timer;

        private int TargetTimerCount = 480;
        private int CurrentTimerCount = 0;
        private int NextItemID = -1;

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
                NextItemID = id;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            CurrentTimerCount++;

            if (TargetTimerCount > 0 && CurrentTimerCount >= TargetTimerCount)
            {
                Timer.Stop();
                CurrentTimerCount = 0;

                if (int.TryParse(CurrentItem.Attribute("ID")?.Value, out int id))
                {
                    if (NextItemID == id) return;
                }

                this.Dispatcher.Invoke(() =>
                {
                    Log.Info($"Current Timer Count: {CurrentTimerCount}  Load Item ID: {NextItemID}");
                    LoadItem(NextItemID);
                });
            }
        }

        /// <summary>
        /// 重置 Timer 计数器
        /// </summary>
        public void TimerReset()
        {
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
            NextItemID = id;
            TargetTimerCount = timerCount;

            TimerReset();
        }

    }
}
