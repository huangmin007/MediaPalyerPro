using System;
using System.Runtime.InteropServices;


namespace Sttplay.MediaPlayer
{

    public class RendererUtility
    {
        private const string dllname = "RendererUtility";

        /// <summary>
        /// 渲染器支持的色彩空间
        /// </summary>
        public enum ColorSpace
        {
            BT601 = 0,
            BT709,
            JPEG
        }


        /// <summary>
        /// 渲染器支持的像素格式
        /// </summary>
        public enum PixelFormat
        {
            YUV420P = 0,
            YUV422P = 4,
            YUV444P = 5,

            YUYV422 = 1,
            UYVY422 = 15,

            GRAY8 = 8,

            YUVJ420P = 12,
            YUVJ422P = 13,
            YUVJ444P = 14,

            NV12 = 23,
            NV21 = 24,

            RGB24 = 2,
            BGR24 = 3,

            ARGB = 25,
            RGBA = 26,
            ABGR = 27,
            BGRA = 28,
        }

        /// <summary>
        /// 视频帧数据
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public class Frame
        {
            public Frame()
            {
                data = new IntPtr[4];
                linesize = new int[4];
            }
            public int width;
            public int height;
            public int format;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.SysUInt)]
            public IntPtr[] data;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.I4)]
            public int[] linesize;
        }

        /// <summary>
        /// 检测D3D9模块
        /// <returns>成功返回1</returns>
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall)]
        public static extern int CheckDirect3D9(int forceUpdateDirect3D);

        /// <summary>
        /// 创建D3D渲染器
        /// </summary>
        /// <param name="videoWidth">视频宽</param>
        /// <param name="videoHeight">视频高</param>
        /// <param name="format">像素格式（参考PixelFormat）</param>
        /// <param name="colorSpace">色彩空间（ColorSpace） </param>
        /// <param name="enableWindow">启用窗口</param>
        /// <returns>成功返回渲染器，失败返回IntPtr.Zero</returns>
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr CreateRenderer(int videoWidth, int videoHeight, int format, int colorSpace, int enableWindow);

        /// <summary>
        /// 销毁D3D渲染器
        /// </summary>
        /// <param name="renderer">渲染器</param>
        /// <returns></returns>
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr DestroyRenderer(IntPtr renderer);

        /// <summary>
        /// 绘制视频帧
        /// </summary>
        /// <param name="renderer">渲染器</param>
        /// <param name="frame">帧数据</param>
        /// <returns>1 success </returns>
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall)]
        public static extern int Render(IntPtr renderer, Frame frame);

        /// <summary>
        /// 启用Alpha混合
        /// </summary>
        /// <param name="renderer">渲染器</param>
        /// <param name="enable">是否启用， 1启用，0不启用</param>
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall)]
        public static extern void EnableAlphaBlend(IntPtr renderer, int enable);

        /// <summary>
        /// 获取D3D surface
        /// </summary>
        /// <param name="renderer">渲染器</param>
        /// <returns>surface</returns>
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr GetSurface(IntPtr renderer);

        [DllImport(dllname, CallingConvention = CallingConvention.StdCall)]
        public static extern int LockSurface(IntPtr surface, ref IntPtr data, ref int pitch);

        [DllImport(dllname, CallingConvention = CallingConvention.StdCall)]
        public static extern void UnlockSurface(IntPtr surface);

        /// <summary>
        /// byte[] 转换 IntPtr
        /// </summary>
        /// <param name="arr">byte数组</param>
        /// <param name="offset">偏移量</param>
        /// <returns>数组指针</returns>
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, EntryPoint = "ConvertPointer")]
        public static extern IntPtr ConvertPointer(byte[] arr, int offset);

        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, EntryPoint = "ConvertPointer")]
        public static extern IntPtr ConvertPointer(IntPtr[] arr, int offset);

    }
}

