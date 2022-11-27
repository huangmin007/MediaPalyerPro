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

        private void InitializeTimer()
        {
            Timer = new Timer();
            Timer.Enabled = true;
            Timer.Interval = 1000;
            Timer.Elapsed += Timer_Elapsed;
            Timer.Start();

            if (int.TryParse(ConfigurationManager.AppSettings["Timer.Count"], out int count))
                TargetTimerCount = count;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            CurrentTimerCount++;

#if DEBUG
            //Console.WriteLine($"TimerCount: {TimerCount}");
            if (CurrentTimerCount >= 10)
#else
            if(CurrentTimerCount >= TargetTimerCount)
#endif
            {
                Timer.Stop();
                if (CurrentItem.Attribute("ID")?.Value.ToString().Trim() == "0") return;

                this.Dispatcher.Invoke(() =>
                {
                    Log.Info($"Current Timer Count: {CurrentItem}");
                    LoadItem(0);
                });
            }
        }

        public void TimerRestart()
        {
            CurrentTimerCount = 0;
            if(!Timer.Enabled)   Timer.Start();
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
