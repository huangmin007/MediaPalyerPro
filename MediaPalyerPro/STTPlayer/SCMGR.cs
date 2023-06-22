using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace Sttplay.MediaPlayer
{
    class SCMGR
    {
        public enum RendererType
        {
            D3D9,
            RendererX,
            Bitmap
        }
        /// <summary>
        /// 当值为Bitmap时候解码后的数据会直接进行离屏绘制，通过D3DImage进行更新，效率较高。
        /// 当值为RendererX和D3D9时候解码后的数据会默认转成BGRA，这一步是CPU进行处理比耗时，最后通过WriteableBitmap进行更新，效率较低。
        /// </summary>
        public static RendererType Renderer { get; set; }

        public static AudioDriverType AudioDriver { get; set; }

        private static DispatcherTimer timer;
        private static List<ISCPlayerPro> cores = new List<ISCPlayerPro>();
        public SCMGR()
        {
            Renderer = RendererType.RendererX;
            AudioDriver = AudioDriverType.Auto;
            InitializeSCPlayerPro();
            timer = new DispatcherTimer();
            timer.Tick += Update;
            timer.Interval = new TimeSpan(0, 0, 0, 0, 16);
            timer.Start();
        }

        private static void Update(object sender, EventArgs e)
        {
            int level = 0;
            System.IntPtr log = System.IntPtr.Zero;
            while ((log = ISCNative.PeekSCLog(ref level)) != System.IntPtr.Zero)
                LogCallback((LogLevel)level, Marshal.PtrToStringAnsi(log));
            foreach (var item in cores)
                item.Update();
        }

        public static bool IsPaused { get; set; }
        public static void LogCallback(LogLevel level, string msg)
        {
            Console.WriteLine("[{0}]:{1}", level, msg);
        }

        private static void InitializeSCPlayerPro()
        {
            try
            {
                ISCNative.InitSCLog(IntPtr.Zero);
                ISCNative.InitializeStreamCapturePro();
                ISCNative.InitilizeAudioPlayer((int)AudioDriver);
            }
            catch (Exception ex)
            {
                MessageBox.Show("The MICROSOFT VISUAL C++ 2015 - 2022 RUNTIME library is missing, please install it and try again.\n" + ex.ToString(), "error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        private static void TerminateSCPlayerPro()
        {
            timer.Stop();
            ISCNative.TerminateAudioPlayer();
            ISCNative.TerminateStreamCapturePro();
            ISCNative.DeInitSCLog();
            SCMGR.Update(null, null);
        }

        public static void AddPlayer(ISCPlayerPro player)
        {
            cores.Add(player);
        }
        public static void RemovePlayer(ISCPlayerPro player)
        {
            cores.Remove(player);
        }

        ~SCMGR()
        {
            //FIXED ME !!!
            //Dispatcher.InvokeAsync(() =>
            //{
            //    TerminateSCPlayerPro();
            //});
        }
    }
}
