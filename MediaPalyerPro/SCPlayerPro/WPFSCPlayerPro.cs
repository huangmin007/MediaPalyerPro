﻿using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sttplay.ThreadPool;

namespace Sttplay.MediaPlayer
{
    public class WPFSCPlayerPro : Image
    {
        private SCPlayerPro core;
        private Renderer renderer;
        private Window window;

        public Renderer Renderer { get { return renderer; } }

        /// <summary>
        /// URL When the choice of mediatype is different, url has different meanings
        /// LocalOrNetworkFile:local file, http, https
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Mark whether player is closed
        /// Open failure is also considered not close
        /// </summary>
        public bool Closed { get { return core.Closed; } }

        /// <summary>
        /// Mark whether player successfully opened media
        /// </summary>
        public bool OpenSuccessed { get { return core.OpenSuccessed; } }

        /// <summary>
        /// Whether to disable video 
        /// </summary>
        public bool DisableVideo { get; set; }
        /// <summary>
        /// Whether to disable audio
        /// </summary>
        public bool DisableAudio { get; set; }

        public bool DisableSubtitle { get; set; }

        public int DefaultVideoTrack { get; set; }
        public int DefaultAudioTrack { get; set; }
        public int DefaultSubtitleTrack { get; set; }

        /// <summary>
        /// Whether to enable hardware acceleration
        /// Not all videos support hardware acceleration.
        /// If you enable this option, hardware acceleration will be tried first, 
        /// and if it fails, the CPU will be used for decoding. 
        /// </summary>
        public bool EnableHWAccel { get; set; }

        /// <summary>
        /// Hardware device type when video hardware accelerates decoding 
        /// Not all of the current platforms are supported, 
        /// if the current option does not support, set as the default 
        /// </summary>
        public HWDeviceType HWAccelType { get; set; }

        /// <summary>
        /// Pixel format of output SCFrame 
        /// </summary>
        public PixelFormat OutputFmt { get; set; }

        public MediaType OpenMode { get; set; }

        public int CameraWidth { get; set; }
        public int CameraHeight { get; set; }
        public float CameraFPS { get; set; }
        public string CameraExtended { get; set; }

        public bool Nobuffer { get; set; }
        public bool LowDelay { get; set; }
        public bool RtspTransportTCP { get; set; }


        /// <summary>
        /// Whether to open the media when UnityPlayer starts 
        /// </summary>
        public bool AutoOpen { get; set; }

        /// <summary>
        /// Play directly after opening or stay at the first frame
        /// </summary>
        public bool OpenAndPlay { get; set; }


        /// <summary>
        /// Media volume
        /// </summary>
        public float Volume { get; set; }

        /// <summary>
        /// Playback speed
        /// </summary>
        public float Speed { get { Verfiy(); return core.Speed; } set { Verfiy(); core.Speed = value; } }

        /// <summary>
        /// Whether the marker is in a paused state 
        /// </summary>
        public bool IsPaused { get { return core.IsPaused; } }

        /// <summary>
        /// Current playback timestamp, valid when the mediaType is LocalOrNetFile
        /// </summary>
        public long CurrentTime { get { return core.CurrentTime; } }

        /// <summary>
        /// The total duration of the media, valid when the mediaType is LocalOrNetFile 
        /// </summary>
        public long Duration { get { return core.Duration; } }


        /// <summary>
        /// Called when player demux succeeds or failed
        /// </summary>
        public event Action<WPFSCPlayerPro, CaptureOpenResult, string, OpenCallbackContext> onCaptureOpenCallbackEvent;

        /// <summary>
        /// Called when player demux read pakcet failed
        /// </summary>
        public event Action<WPFSCPlayerPro, string> onInterruptCallbackEvent;

        /// <summary>
        /// Called when opening 
        /// </summary>
        public event Action<WPFSCPlayerPro> onOpenEvent;

        /// <summary>
        /// Called when closing 
        /// </summary>
        public event Action<WPFSCPlayerPro> onCloseEvent;

        /// <summary>
        /// Called when renderer is changed
        /// </summary>
        public event Action<WPFSCPlayerPro, SCFrame> onRendererChangedEvent;

        /// <summary>
        /// Called when the video has finished playing, whether looping or not 
        /// </summary>
        public event Action<WPFSCPlayerPro> onStreamFinishedEvent;

        /// <summary>
        /// Called after the first frame is drawn, if there is no video stream, this event will not be called 
        /// </summary>
        public event Action<WPFSCPlayerPro, SCFrame> onFirstFrameRenderEvent;

        /// <summary>
        /// Called after each frame of video is drawn , if there is no video stream
        /// @Tip:
        /// The alignment of the frame here is 1-byte alignment
        /// </summary>
        public event Action<WPFSCPlayerPro, SCFrame> onRenderFrameEvent;

        public event Action<WPFSCPlayerPro, IntPtr, int> onRenderAudioEvent;
        public event Action<WPFSCPlayerPro> onStatusChangeEvent;


        /// <summary>
        /// File type to open
        /// </summary>
        public MediaType mediaType = MediaType.LocalOrNetworkFile;

        private bool isFirst = true;

        /// <summary>
        /// Whether the media is played in a loop 
        /// This option is valid only when the mediaType is LocalOrNetworkFile 
        /// </summary>
        public bool Loop
        {
            get { Verfiy(); return core.Loop; }
            set { Verfiy(); core.Loop = value; }
        }


        private bool isInit = false;
        private static SCMGR scmgr;

        private void Verfiy()
        {
            if (scmgr == null)
                scmgr = new SCMGR();

            if (isInit) return;
            isInit = true;
            if (core == null)
                core = new SCPlayerPro();

            EnableHWAccel = true;
            Loop = false;
            Volume = 1.0f;
            OutputFmt = PixelFormat.AUTO;
            HWAccelType = HWDeviceType.AUTO;
            OpenAndPlay = true;
        }
        private SynchronizationContext context;
        MiniThreadPool pool;
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            window = Application.Current.MainWindow;
            window.Closed += OnClose;

            CompositionTarget.Rendering += Update;

            Verfiy();
            try
            {
                RendererUtility.CheckDirect3D9(SCMGR.UpdateDirect3D ? 1 : 0);
            }
            catch (Exception ex)
            {
                SCMGR.Renderer = SCMGR.RendererType.RendererX;
                Console.WriteLine(ex);
            }
            switch (SCMGR.Renderer)
            {
                case SCMGR.RendererType.D3D9:
                    renderer = new RendererD3D9(this);
                    break;
                case SCMGR.RendererType.RendererX:
                    renderer = new RendererX(this);
                    break;
                case SCMGR.RendererType.Bitmap:
                    renderer = new RendererBitmap(this);
                    break;
                default:
                    break;
            }
            renderer.LockFrame = core.LockFrame;
            renderer.UnlockFrame = core.UnlockFrame;
            renderer.IsSystemSource = isSysSource;

            core.onStreamFinishedEvent += OnStreamFinished;
            core.onCaptureOpenCallbackEvent += OnCaptureOpenCallback;
            core.onInterruptCallbackEvent += OnInterruptCallback;
            core.onDrawVideoFrameEvent += OnDrawVideoFrame;
            core.onDrawAudioFrameEvent += OnDrawAudioFrame;

            if (AutoOpen)
                Open(OpenMode);

            pool = new MiniThreadPool(5);

        }

        private void OnDrawAudioFrame(IntPtr arg1, int arg2)
        {
            onRenderAudioEvent?.Invoke(this, arg1, arg2);
        }

        private void OnInterruptCallback(string error)
        {
            if (onInterruptCallbackEvent != null)
            {
                try
                {
                    onInterruptCallbackEvent(this, error);
                }
                catch { }
            }
        }

        private bool isSysSource = true;
        public new ImageSource Source
        {
            get { return base.Source; }
            set
            {
                isSysSource = false;
                if (renderer != null)
                    renderer.IsSystemSource = isSysSource;
                base.Source = value;
            }
        }

        private void OnClose(object sender, EventArgs e)
        {
            ReleaseCore();
            window.Closed -= OnClose;
            CompositionTarget.Rendering -= Update;
            pool.Close(true);

            onStatusChangeEvent?.Invoke(this);
        }

        private IntPtr GetAVFramePointer()
        {
            return core.GetAVFramePointer();
        }

        private void OnDrawVideoFrame(SCFrame frame)
        {
            pool.PushJob(RendererVideoFrame, null);
        }

        private void RendererVideoFrame(object param)
        {
            Dispatcher.Invoke(() =>
            {
                if (core == null) return;
                SCFrame frame = core.LockFrame();
                bool isChanged = false;
                if (renderer.Width != frame.width || renderer.Height != frame.height || renderer.Format != frame.format)
                {
                    renderer.Initialize(frame.width, frame.height, frame.linesize, frame.format, false);
                    isChanged = true;
                }
                renderer.Render(frame.width, frame.height, frame.format, frame.data, frame.linesize);
                core.UnlockFrame();
                if (isChanged)
                {
                    if (onRendererChangedEvent != null)
                    {
                        try
                        {
                            onRendererChangedEvent(this, frame);
                        }
                        catch { }
                    }
                }
                if (isFirst)
                {
                    isFirst = false;
                    if (onFirstFrameRenderEvent != null)
                    {
                        try
                        {
                            onFirstFrameRenderEvent(this, frame);
                        }
                        catch { }
                    }
                }
                if (onRenderFrameEvent != null)
                {
                    try
                    {
                        onRenderFrameEvent(this, frame);
                    }
                    catch { }
                }

            });
        }
        private void Update(object sender, EventArgs e)
        {
            MediaPlayer.Dispatcher.WakeAll();
            if (core == null) return;
            core.Volume = Volume;
        }

        /// <summary>
        /// When the Open function is called, 
        /// the function will be called back regardless of whether it is opened or not, 
        /// unless you call the Close function before the successful opening
        /// </summary>
        /// <param name="result">open result</param>
        /// <param name="error">error infomation</param>
        /// <param name="context">video or audio param</param>
        private void OnCaptureOpenCallback(CaptureOpenResult result, string error, OpenCallbackContext context)
        {
            if (onCaptureOpenCallbackEvent != null)
            {
                try
                {
                    onCaptureOpenCallbackEvent(this, result, error, context);
                }
                catch { }
            }

        }

        /// <summary>
        /// open media
        /// </summary>
        /// <param name="url"></param>
        public void Open(MediaType openMode, string url = null)
        {
            this.OpenMode = openMode;
            if (onOpenEvent != null)
            {
                try
                {
                    onOpenEvent(this);
                }
                catch { }
            }

            Close();
            if (core == null) return;
            isFirst = true;
            if (string.IsNullOrEmpty(url))
                url = this.Url;

            core.OpenNoPlay = !OpenAndPlay;

            core.DisableAudio = DisableAudio;
            core.DisableVideo = DisableVideo;
            core.DisableSubtitle = DisableSubtitle;
            core.DefaultVideoTrack = DefaultVideoTrack;
            core.DefaultAudioTrack = DefaultAudioTrack;
            core.DefaultSubtitleTrack = DefaultSubtitleTrack;
            core.EnableHWAccel = EnableHWAccel;
            if (SCMGR.Renderer == SCMGR.RendererType.Bitmap)
                core.OutputPixelFormat = PixelFormat.BGRA;
            core.HWAccelType = HWAccelType;
            core.OpenMode = OpenMode;
            core.CameraWidth = CameraWidth;
            core.CameraHeight = CameraHeight;
            core.CameraFPS = CameraFPS;
            core.CameraExtended = CameraExtended;
            core.Nobuffer = Nobuffer;
            core.LowDelay = LowDelay;
            core.RtspTransportTCP = RtspTransportTCP;

            core.Loop = Loop;
            core.Volume = Volume;

            this.Url = url;
            core.Open(OpenMode, url);
            context = SynchronizationContext.Current;

            onStatusChangeEvent?.Invoke(this);
        }

        /// <summary>
        /// replay video
        /// </summary>
        /// <param name="paused">pause or play</param>
        public void Replay(bool paused)
        {
            if (core == null) return;
            core.Replay(paused);
        }

        /// <summary>
        /// close media
        /// </summary>
        public void Close()
        {
            //SynchronizationContext.SetSynchronizationContext(null);
            renderer.Terminate();
            if (onCloseEvent != null)
            {
                try
                {
                    onCloseEvent(this);
                }
                catch { }
            }

            if (core == null) return;
            isFirst = false;
            core.Close();

            onStatusChangeEvent?.Invoke(this);
        }

        /// <summary>
        /// Set whether the video is played in a loop
        /// </summary>
        /// <param name="isLoop">loop or not</param>
        public void SetLoop(bool loop)
        {
            if (core == null) return;
            this.Loop = loop;
            core.SetLoop(loop);
        }

        public void SetVolume(float volume)
        {
            if (core == null) return;
            this.Volume = volume;
            core.Volume = Volume;
        }

        public void VolumeUp()
        {
            if (core == null) return;
            this.Volume = this.Volume >= 1.0f ? 1.0f : this.Volume + 0.1f;
            core.Volume = Volume;
        }

        public void VolumeDown()
        {
            if (core == null) return;
            this.Volume = this.Volume <= 0.0f ? 0.0f : this.Volume - 0.1f;
            core.Volume = Volume;
        }

        /// <summary>
        /// Seek to key frame quickly according to percentage
        /// </summary>
        /// <param name="percent"></param>
        public void SeekFastPercent(double percent)
        {
            if (core == null) return;
            core.SeekFastPercent(percent);
        }

        /// <summary>
        /// Seek to key frame quickly according to ms
        /// </summary>
        /// <param name="ms"></param>
        public void SeekFastMilliSecond(int ms)
        {
            if (core == null) return;
            core.SeekFastMilliSecond(ms);
        }

        /// <summary>
        /// play
        /// </summary>
        public void Play()
        {
            if (core == null) return;
            core.Play();

            onStatusChangeEvent?.Invoke(this);
        }

        /// <summary>
        /// pause
        /// </summary>
        public void Pause()
        {
            if (core == null) return;
            core.Pause();
            onStatusChangeEvent?.Invoke(this);
        }


        public void SetDefaultVideoSource()
        {
            renderer.IsSystemSource = isSysSource = true;
            renderer.SetDefaultVideoSource();
        }

        /// <summary>
        /// Whether the current playback mode is looping, it will be called after media playback is complete.
        /// </summary>
        private void OnStreamFinished()
        {
            if (onStreamFinishedEvent != null)
            {
                try
                {
                    onStreamFinishedEvent(this);
                }
                catch { }
            }
        }

        /// <summary>
        /// Release all resources of the player. 
        /// The user does not need to call this function. 
        /// All operations will be invalid after the function is called.
        /// </summary>
        public void ReleaseCore()
        {
            Close();
            if (core == null) return;
            if (renderer != null)  renderer.Release();

            core.onDrawAudioFrameEvent -= OnDrawAudioFrame;
            core.onStreamFinishedEvent -= OnStreamFinished;
            core.onCaptureOpenCallbackEvent -= OnCaptureOpenCallback;
            core.onInterruptCallbackEvent -= OnInterruptCallback;
            core.onDrawVideoFrameEvent -= OnDrawVideoFrame;

            core.Release();
            core = null;
        }
    }
}
