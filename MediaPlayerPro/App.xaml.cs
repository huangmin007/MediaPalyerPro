using System.Windows;
using SpaceCG.Generic;

namespace MediaPlayerPro
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        static readonly LoggerTrace Logger = new LoggerTrace(nameof(Application));

        /// <inheritdoc/>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            this.ShutdownMode = ShutdownMode.OnMainWindowClose;
            this.DispatcherUnhandledException += Application_DispatcherUnhandledException;

            Logger.Info($"Application OnStartup.");
        }

        /// <inheritdoc/>
        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            Logger.Info($"Application OnExit, ExitCode: {e.ApplicationExitCode}");
        }

        /// <summary>
        /// 在由应用程序引发异常，但未进行处理时发生
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.Error($"应用程序未处理的异常：{e.Exception}");
        }
    }
}
