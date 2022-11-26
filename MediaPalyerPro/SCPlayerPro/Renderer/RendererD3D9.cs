using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace Sttplay.MediaPlayer
{
    public class RendererD3D9 : Renderer
    {
        private bool useSoftRender;

        private IntPtr render;

        private D3DImage d3dImage;

        private bool needSetBackbuffer = false;


        public RendererD3D9(Image control)
        {
            ui = control;
            if (!SCMGR.UpdateDirect3D)
            {
                ui.Source = d3dImage = new D3DImage();
                d3dImage.IsFrontBufferAvailableChanged += OnFrontBufferAvailableChanged;
            }
            useSoftRender = CheckHardWareSupport();
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
            if (render != IntPtr.Zero)
                RendererUtility.DestroyRenderer(render);
            render = IntPtr.Zero;
            Width = width;
            Height = height;
            Format = format;
            needSetBackbuffer = true;
            if (SCMGR.UpdateDirect3D)
            {
                ui.Source = d3dImage = new D3DImage();
                d3dImage.IsFrontBufferAvailableChanged += OnFrontBufferAvailableChanged;
            }
            render = RendererUtility.CreateRenderer(Width, Height, Format, (int)RendererUtility.ColorSpace.BT601, enableWindow ? 1 : 0);
            if (render == IntPtr.Zero)
                throw new Exception("render create failed!");
            return render == IntPtr.Zero ? false : true;
        }

        public override void Render(int width, int height, int format, IntPtr[] data, int[] linesize)
        {
            RendererUtility.Frame rframe = new RendererUtility.Frame();
            for (int i = 0; i < rframe.data.Length; i++)
            {
                rframe.data[i] = data[i];
                rframe.linesize[i] = linesize[i];
            }
            rframe.width = Width;
            rframe.height = Height;
            rframe.format = Format;
            if (RendererUtility.Render(render, rframe) != 1)
            {
                throw new Exception("render failed!");
            }
            UpdateImage();
        }

        private void UpdateImage()
        {
            if (!d3dImage.IsFrontBufferAvailable)
            {
                if (SCMGR.UpdateDirect3D)
                {
                    SCFrame frame = LockFrame();
                    Initialize(frame.width, frame.height, null, frame.format, false);
                    if (!d3dImage.IsFrontBufferAvailable)
                        throw new Exception("IsFrontBufferAvailable");
                    Render(frame.width, frame.height, frame.format, frame.data, frame.linesize);
                    UnlockFrame();
                }
                return;
            }

            IntPtr surface = RendererUtility.GetSurface(render);
            if (surface != IntPtr.Zero)
            {
                if (needSetBackbuffer)
                {
                    d3dImage.Lock();
                    d3dImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, surface, useSoftRender);
                    d3dImage.Unlock();
                    needSetBackbuffer = false;
                }
                d3dImage.Lock();
                d3dImage.AddDirtyRect(new Int32Rect(0, 0, d3dImage.PixelWidth, d3dImage.PixelHeight));
                d3dImage.Unlock();

            }
        }


        public override void SetDefaultVideoSource()
        {

            ui.Source = d3dImage;
        }

        public override void Terminate()
        {
            Width = Height = 0;
        }
    }
}
