using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Sttplay.MediaPlayer
{
    public class RendererBitmap : Renderer
    {
        private WriteableBitmap writeableBitmap;

        private byte[] cache;

        public RendererBitmap(Image control)
        {
            ui = control;
        }
        public override bool Initialize(int width, int height, int[] linesize, int format, bool enableWindow)
        {
            Terminate();
            Width = width;
            Height = height;
            Format = format;

            if (width != linesize[0] / 4)
                cache = new byte[Width * Height * 4];
            else
                cache = null;
            writeableBitmap = new WriteableBitmap(Width, Height, 96, 96, PixelFormats.Bgra32, null);
            return true;
        }

        public override void Render(int width, int height, int format, IntPtr[] data, int[] linesize)
        {
            writeableBitmap.Lock();
            if (cache != null)
            {
                ISCNative.MemoryAlignment(data[0], Width * 4, Height, linesize[0], cache);
                Marshal.Copy(cache, 0, writeableBitmap.BackBuffer, cache.Length);
            }
            else
            {
                memcpy(writeableBitmap.BackBuffer, data[0], linesize[0] * Height);
            }

            writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, Width, Height));
            writeableBitmap.Unlock();
            if (IsSystemSource)
                ui.Source = writeableBitmap;
        }

        public override void SetDefaultVideoSource()
        {
            ui.Source = writeableBitmap;
        }

        public override void Terminate()
        {
            writeableBitmap = null;
        }
    }
}
