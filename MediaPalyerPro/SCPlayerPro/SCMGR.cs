using System;
using System.Windows;

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
        /// 当值为true时候解码后的数据会直接进行离屏绘制，通过D3DImage进行更新，效率较高。
        /// 当值为false时候解码后的数据会默认转成BGRA，这一步是CPU进行处理比耗时，最后通过WriteableBitmap进行更新，效率较低。
        /// </summary>
        public static RendererType Renderer { get; set; }

        public static AudioDriverType AudioDriver { get; set; }

        /// <summary>
        /// 是否在每次创建D3D9渲染器的时候重置IDirect3DEx
        /// </summary>
        public static bool UpdateDirect3D = true;
        public SCMGR()
        {
            Renderer = RendererType.RendererX;
            AudioDriver = AudioDriverType.Auto;
            InitializeSCPlayerPro();
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
                ISCNative.InitSCLog(ISCNative.logCallback);
                ISCNative.InitializeStreamCapturePro();
                ISCNative.InitilizeAudioPlayer((int)AudioDriver);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
        private static void TerminateSCPlayerPro()
        {
            ISCNative.TerminateAudioPlayer();
            ISCNative.TerminateStreamCapturePro();
            ISCNative.DeInitSCLog();
        }

        ~SCMGR()
        {
            Dispatcher.Invoke(() =>
            {
                TerminateSCPlayerPro();
            });
        }
    }
}
