using System;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Media;

namespace Sttplay.MediaPlayer
{
    public class Renderer
    {
        public int Width { get; protected set; }
        public int Height { get; protected set; }
        public int Format { get; protected set; }

        protected Image ui;

        public bool IsSystemSource { get; set; }

        public virtual bool Initialize(int width, int height, int[] linesize, int format, bool enableWindow) { return false; }

        public virtual void Render(int width, int height, int format, IntPtr[] data, int[] linesize) { }

        public virtual void SetRenderModel(int model) { }

        public virtual void SetDefaultVideoSource() { }

        public virtual void Terminate() { }

        public virtual void Release() { }

        public virtual void ForceEnableBlend(bool enable) { }

        public Func<SCFrame> LockFrame;

        public Action UnlockFrame;


        protected bool CheckHardWareSupport()//查询硬件支持
        {
            int level = RenderCapability.Tier >> 16;
            return level == 0 ? true : false;
        }

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        public static extern void memcpy(IntPtr dst, IntPtr src, int len);
    }
}
