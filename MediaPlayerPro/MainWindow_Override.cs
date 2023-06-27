using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using SpaceCG.Extensions;
using SpaceCG.Generic;

namespace MediaPlayerPro
{
    public partial class MainWindow : Window
    {
        /// <inheritdoc/>
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            Console.WriteLine("Closing ... ");
            foreach (FrameworkElement child in LogicalTreeHelper.GetChildren(RootContainer))
            {
                child.IsVisibleChanged -= UIElement_IsVisibleChanged;
                foreach (FrameworkElement subChild in LogicalTreeHelper.GetChildren(child))
                {
                    subChild.IsVisibleChanged -= UIElement_IsVisibleChanged;
                }
            }

            InstanceExtensions.RemoveInstanceEvents(MiddlePlayer);
            InstanceExtensions.RemoveInstanceEvents(ForegroundPlayer);
            InstanceExtensions.RemoveInstanceEvents(BackgroundPlayer);

            this.Pause();
            Timer?.Dispose();
            LoggerWindow?.Close();
            ControlInterface?.Dispose();

            NetworkSlave?.Dispose();
            NetworkMaster?.Dispose();

            SyncPlayer = null;
            CurrentPlayer = null;

            MainWindowExtensions.DisposeProcessModule(ref ProcessModule);
        }
        /// <inheritdoc/>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Console.WriteLine("Closed ... ");

            MiddlePlayer?.ReleaseCore();
            ForegroundPlayer?.ReleaseCore();
            BackgroundPlayer?.ReleaseCore();
        }

        /// <inheritdoc/>
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (e.IsRepeat) return;

            RestartTimer();
            Log.Info($"OnKeyDown: {e.KeyboardDevice.Modifiers} - {e.Key}");

            switch (e.Key)
            {
                case Key.D0:
                case Key.D1:
                case Key.D2:
                case Key.D3:
                case Key.D4:
                case Key.D5:
                case Key.D6:
                case Key.D7:
                case Key.D8:
                case Key.D9:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.None)
                        LoadItem((ushort)(e.Key - Key.D0));
                    break;

                case Key.D:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        LoggerTrace.FileTraceLevels = SourceLevels.All;

                        this.Topmost = false;
                        this.WindowState = WindowState.Normal;
                        this.WindowStyle = WindowStyle.SingleBorderWindow;
                    }
                    break;
                case Key.R:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        LoadConfig(MEDIA_CONFIG_FILE);
                    }
                    break;

                case Key.F:
                    this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                    break;
                case Key.S:
                    if (!this.AllowsTransparency)
                        this.WindowStyle = this.WindowStyle == WindowStyle.None ? WindowStyle.SingleBorderWindow : WindowStyle.None;
                    break;
                case Key.T:
                    this.Topmost = !this.Topmost;
                    break;

                case Key.Down:
                case Key.Right:
                    NextNode();
                    break;
                case Key.Up:
                case Key.Left:
                    PrevNode();
                    break;

                case Key.PageDown:
                    break;

                case Key.PageUp:
                    break;

                case Key.VolumeUp: this.VolumeUp(); break;
                case Key.VolumeDown: this.VolumeDown(); break;
                case Key.Play: this.Play();  break;
                case Key.Pause: this.Pause(); break;                    
                case Key.MediaPlayPause: PlayPause(); break;

                case Key.Space:
                case Key.Enter:
                    PlayPause();
                    break;

                case Key.Escape:
                    this.Close();
                    Application.Current.Shutdown(0);
                    break;
            }
        }

        /// <inheritdoc/>
        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            RestartTimer();
            base.OnPreviewMouseDown(e);
            if (Log.IsDebugEnabled) Log.Debug($"On Preview Mouse Down");
        }

    }
}