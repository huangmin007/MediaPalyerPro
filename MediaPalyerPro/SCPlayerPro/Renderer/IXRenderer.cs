using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Sttplay.MediaPlayer
{
    public class IXRenderer
    {
        private const string dllname = "XRenderer";

        /// <summary>
        /// 图像处理接口
        /// </summary>
        public enum GraphicsType
        {
            Direct3D9,
            Direct3D11
        };

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

            //HW FMT
            D3D11 = 174
        }

        [DllImport(dllname, CallingConvention = CallingConvention.Cdecl)]
        public static extern int RegisterAdapter(int adapterIndex);

        [DllImport(dllname, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CreateRenderer(int type, IntPtr hwnd);

        [DllImport(dllname, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ReleaseRenderer(IntPtr renderer);

        [DllImport(dllname, CallingConvention = CallingConvention.Cdecl)]
        public static extern void PresentRenderer(IntPtr renderer);

        [DllImport(dllname, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ResizeRenderer(IntPtr renderer, int width, int height);

        [DllImport(dllname, CallingConvention = CallingConvention.Cdecl)]
        public static extern void CameraSetPosition(IntPtr renderer, float x, float y, float z);

        [DllImport(dllname, CallingConvention = CallingConvention.Cdecl)]
        public static extern void CameraSetRotation(IntPtr renderer, float pitch, float yaw);

        [DllImport(dllname, CallingConvention = CallingConvention.Cdecl)]
        public static extern void CameraTranslate(IntPtr renderer, float deltaX, float deltaY, float deltaZ);

        [DllImport(dllname, CallingConvention = CallingConvention.Cdecl)]
        public static extern void CameraRotate(IntPtr renderer, float deltaPitch, float deltaYaw);

        [DllImport(dllname, CallingConvention = CallingConvention.Cdecl)]
        public static extern int BindTexture(IntPtr renderer, IntPtr texture);

        [DllImport(dllname, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRenderModel(IntPtr renderer, int modelType);

        [DllImport(dllname, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRenderColorSpace(IntPtr renderer, int space);

        [DllImport(dllname, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRenderBackgroundColor(IntPtr renderer, float r, float g, float b);

        [DllImport(dllname, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetRenderTarget(IntPtr renderer);

        [DllImport(dllname, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ForceEnableBlend(IntPtr renderer, int enable);


        [DllImport(dllname, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CreateTexture(int type, int width, int height, int[] linesize, int format, IntPtr[] opaque);

        [DllImport(dllname, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ReleaseTexture(IntPtr texture);

        [DllImport(dllname, CallingConvention = CallingConvention.Cdecl)]
        public static extern void UpdateTexture(IntPtr texture, IntPtr[] data);
    }
}
