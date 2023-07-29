using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SpaceCG.Extensions;
using SpaceCG.Generic;
using Sttplay.MediaPlayer;

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
                if(child is WPFSCPlayerPro)
                    child.IsVisibleChanged -= WPFSCPlayerPro_IsVisibleChanged;
            }

            ConnectionManagement.Instance.Disconnections();
            InstanceExtensions.RemoveInstanceEvents(MiddlePlayer);
            InstanceExtensions.RemoveInstanceEvents(ForegroundPlayer);
            InstanceExtensions.RemoveInstanceEvents(BackgroundPlayer);

            this.Pause();            
            LoggerWindow?.Close();            

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

            Timer?.Dispose();
            hwndSource?.Dispose();
            ControlInterface?.Dispose();

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
                case Key.NumPad0:
                case Key.NumPad1:
                case Key.NumPad2:
                case Key.NumPad3:
                case Key.NumPad4:
                case Key.NumPad5:
                case Key.NumPad6:
                case Key.NumPad7:
                case Key.NumPad8:
                case Key.NumPad9:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.None)
                        LoadItem((ushort)(e.Key - Key.NumPad0));
                    break;

                case Key.R: if (e.KeyboardDevice.Modifiers == ModifierKeys.Control) LoadConfig(MEDIA_CONFIG_FILE); break;
                case Key.F5: LoadConfig(MEDIA_CONFIG_FILE); break;
                case Key.F6: this.Topmost = !this.Topmost; break;
                case Key.F11:
                    if (this.WindowState != WindowState.Maximized)
                    {
                        LoggerTrace.FileTraceLevels = SourceLevels.Information;

                        this.Topmost = true;
                        if (!this.AllowsTransparency) this.WindowStyle = WindowStyle.None;
                        this.WindowState = WindowState.Maximized;
                    }
                    else
                    {
                        LoggerTrace.FileTraceLevels = SourceLevels.All;

                        this.Topmost = false;
                        if (!this.AllowsTransparency) this.WindowStyle = WindowStyle.SingleBorderWindow;
                        this.WindowState = WindowState.Normal;
                    }
                    break;

                case Key.T: this.Topmost = !this.Topmost; break;
                case Key.F: this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; break;
                case Key.S: if (!this.AllowsTransparency) this.WindowStyle = this.WindowStyle == WindowStyle.None ? WindowStyle.SingleBorderWindow : WindowStyle.None; break;
                case Key.W: if(e.KeyboardDevice.Modifiers == ModifierKeys.Control)  OpenLoggerWindow(); break;

                case Key.Left: PrevNode(); break;
                case Key.Right: NextNode(); break;

                case Key.Up:
                case Key.PageUp: PrevItem(); break;

                case Key.Down:
                case Key.PageDown: NextItem(); break;

                case Key.VolumeUp: this.VolumeUp(); break;
                case Key.VolumeDown: this.VolumeDown(); break;

                case Key.Play: this.Play(); break;
                case Key.Pause: this.Pause(); break;
                case Key.MediaPlayPause: PlayPause(); break;

                case Key.Space:
                case Key.Enter: PlayPause(); break;

                case Key.Escape:
                    this.Close();
                    Application.Current.Shutdown(0);
                    break;
            }
        }

        /// <inheritdoc/>
        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            RestartTimer();
            base.OnPreviewMouseLeftButtonDown(e);
            Console.WriteLine($"OnPreviewMouseLeftButtonDown::{e.ButtonState} {e.Timestamp} {e.GetPosition(this)}");

            if (e.ClickCount == 1)
            {
                string elementType = e.Source.GetType().Name;
                string elementName = (e.Source as FrameworkElement)?.Name;
                e.Handled = CallFrameworkElementEvent("MouseDown", elementType, elementName);
            }
            else if (e.ClickCount == 2)
            {
                if (e.Source is Canvas || e.Source is Grid)
                {
                    this.PlayPause();
                    e.Handled = true;
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            RestartTimer();
            base.OnPreviewMouseLeftButtonUp(e);
            Console.WriteLine($"OnPreviewMouseLeftButtonUp::{e.ButtonState} {e.Timestamp} {e.GetPosition(this)}");

            if (e.ClickCount == 1)
            {
                string elementType = e.Source.GetType().Name;
                string elementName = (e.Source as FrameworkElement)?.Name;
                e.Handled = CallFrameworkElementEvent("MouseUp", elementType, elementName);
            }
        }

    }
}