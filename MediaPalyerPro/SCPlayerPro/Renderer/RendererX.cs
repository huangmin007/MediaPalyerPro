using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace Sttplay.MediaPlayer
{
    public class RendererX : Renderer
    {
        private Image image;
        private D3DImage d3dImage;
        private IntPtr xrenderer;
        private IntPtr xtexture;
        private IntPtr xsurface;
        private static int failedCount;
        private bool needSetBackbuffer = false;
        private const int MAX_FAILED_COUNT = 30;

        public RendererX(Image control)
        {
            ui = control;
            image = control;
            try
            {
                xrenderer = IXRenderer.CreateRenderer((int)IXRenderer.GraphicsType.Direct3D9, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnFrontBufferAvailableChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            needSetBackbuffer = true;
            SCFrame frame = LockFrame();
            Render(frame.width, frame.height, frame.format, frame.data, frame.linesize);
            UnlockFrame();
        }

        public override bool Initialize(int width, int height, int[] linesize, int format, bool enableWindow)
        {
            Terminate();
            if (xtexture != IntPtr.Zero)
                IXRenderer.ReleaseTexture(xtexture);
            xtexture = IntPtr.Zero;
            xsurface = IntPtr.Zero;
            image.Source = null;
            if (d3dImage != null)
                d3dImage.IsFrontBufferAvailableChanged -= OnFrontBufferAvailableChanged;
            image.Source = d3dImage = new D3DImage();
            d3dImage.IsFrontBufferAvailableChanged += OnFrontBufferAvailableChanged;
            needSetBackbuffer = false;
            return true;
        }

        public override void Render(int width, int height, int format, IntPtr[] data, int[] linesize)
        {
            if (width != Width || height != Height || format != Format || needSetBackbuffer)
            {
                Initialize(width, height, linesize, format, false);
                try
                {
                    xtexture = IXRenderer.CreateTexture((int)IXRenderer.GraphicsType.Direct3D9, width, height, linesize, format, null);
                    IXRenderer.ResizeRenderer(xrenderer, width, height);
                    IXRenderer.BindTexture(xrenderer, xtexture);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                Width = width;
                Height = height;
                Format = format;
                GC.Collect();
            }

            IXRenderer.UpdateTexture(xtexture, data);
            IXRenderer.PresentRenderer(xrenderer);

            if (!d3dImage.TryLock(new Duration(new TimeSpan(0, 0, 0, 0, 100))))
            {
                if (failedCount++ > MAX_FAILED_COUNT)
                {
                    Console.WriteLine("TryLock timeout");
                    needSetBackbuffer = true;
                }
                return;
            }
            failedCount = 0;
            if (xsurface == IntPtr.Zero)
            {
                xsurface = IXRenderer.GetRenderTarget(xrenderer);
                d3dImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, xsurface, CheckHardWareSupport());
            }
            if (xsurface != IntPtr.Zero)
                d3dImage.AddDirtyRect(new Int32Rect(0, 0, d3dImage.PixelWidth, d3dImage.PixelHeight));
            d3dImage.Unlock();
        }

        public override void SetRenderModel(int model)
        {
            IXRenderer.SetRenderModel(xrenderer, model);
        }

        public override void ForceEnableBlend(bool enable)
        {
            IXRenderer.ForceEnableBlend(xrenderer, enable ? 1 : 0);
        }


        public override void SetDefaultVideoSource()
        {
            ui.Source = d3dImage;
        }

        public override void Terminate()
        {
            Width = Height = 0;
        }

        public override void Release()
        {
            if (xtexture != IntPtr.Zero)
                IXRenderer.ReleaseTexture(xtexture);
            xtexture = IntPtr.Zero;
            if (xrenderer != IntPtr.Zero)
                IXRenderer.ReleaseRenderer(xrenderer);
            xrenderer = IntPtr.Zero;
            xsurface = IntPtr.Zero;
        }
    }
}
