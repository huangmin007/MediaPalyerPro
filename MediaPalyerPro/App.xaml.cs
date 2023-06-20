using System.Windows;
using SpaceCG.Generic;

namespace MediaPalyerPro
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        public static readonly LoggerTrace Log = new LoggerTrace(nameof(App));

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            this.ShutdownMode = ShutdownMode.OnMainWindowClose;
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error($"应用程序未处理的异常：{e.Exception}");
        }
    }
}
