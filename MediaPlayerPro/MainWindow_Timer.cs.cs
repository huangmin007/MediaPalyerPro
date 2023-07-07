using System.Linq;
using System.Timers;
using System.Windows;
using System.Xml.Linq;

namespace MediaPlayerPro
{
    public partial class MainWindow : Window
    {
        private Timer Timer;
        private XElement TimerElement;
        internal static int CurrentTickCount = 0;

        private void InitializeTimer(XElement timerElement)
        {
            CurrentTickCount = 0;
            TimerElement = timerElement;

            if (Timer == null)
            {
                Timer = new Timer();
                Timer.Elapsed += Timer_Elapsed;
                Timer.Interval = 1000;                
                ControlInterface.AccessObjects.Add("Timer", Timer);
                ControlInterface.NetworkMessageEvent += (s, e) => { RestartTimer(); };
            }

            Timer.Interval = int.TryParse(TimerElement?.Attribute("Interval")?.Value, out int interval) ? interval : 1000;
            if(Timer.Interval >= 30) Timer.Start();
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (RootConfiguration == null || TimerElement == null) return;

            SyncSlaveOnline();
            CurrentTickCount++;

            var events = from evt in TimerElement.Elements(XEvent)
                         where evt.Attribute(XType)?.Value == "Tick" && evt.Attribute("Count")?.Value == CurrentTickCount.ToString() 
                         select evt;
            if (events?.Count() <= 0) return;

            Log.Info($"Timer Current Tick Count: {CurrentTickCount}, Events Count: {events.Count()}");
            this.Dispatcher.InvokeAsync(() =>
            {
                foreach (XElement element in events.Elements())
                {
                    if (element.Name.LocalName == XAction)
                    {
                        ControlInterface.TryParseControlMessage(element);
                    }
                }
            });
        }

        /// <summary>
        /// 重置 Timer 计数器
        /// </summary>
        protected void RestartTimer()
        {
            if (Timer == null) return;

            CurrentTickCount = 0;
            Timer?.Restart();
        }

    }
}