using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Sttplay.MediaPlayer
{
    /// <summary>
    /// Audio and video synchronization strategy
    /// </summary>
    public enum SYNCType
    {
        /// <summary>
        /// Audio is primary, video is synchronized to audio
        /// </summary>
        AudioMaster,
    };

    /// <summary>
    /// Type of media to open
    /// </summary>
    public enum MediaType
    {
        /// <summary>
        /// local file, http, https, (not hls)
        /// </summary>
        LocalOrNetworkFile = 0,

        /// <summary>
        /// rtp, rtsp, rtmp, hls ...
        /// </summary>
        Link,

        /// <summary>
        /// camera, virtual camera
        /// </summary>
        Camera
    }


    /// <summary>
    /// SCPlayer is a player class
    /// It is also feasible if you need to use it in WPF or WinForm,
    /// but you need to encapsulate a management class similar to UnitySCPlayerPro by yourself.
    /// We provide a relatively open development environment, 
    /// not only limited to Unity, if you want to use SCPlayerPro on other platforms or other languages, 
    /// there is no problem, just call the corresponding interface program.
    /// The interface part can refer to SCInterface.cs
    /// </summary>
    public class SCPlayerPro : IPlayer
    {

        private const int AV_SYNC_THRESHOLD_MIN = 40;
        private const int AV_SYNC_THRESHOLD_MAX = 100;
        private const int AV_SYNC_FRAMEDUP_THRESHOLD = 100;


        /// <summary>
        /// Whether to disable video 
        /// </summary>
        public bool DisableVideo { get; set; }

        /// <summary>
        /// Whether to disable audio
        /// </summary>
        public bool DisableAudio { get; set; }

        /// <summary>
        /// Whether to disable subtitle
        /// </summary>
        public bool DisableSubtitle { get; set; }

        /// <summary>
        /// The video track selected by default after opening the media
        /// </summary>
        public int DefaultVideoTrack { get; set; }

        /// <summary>
        /// The audio track selected by default after opening the media
        /// </summary>
        public int DefaultAudioTrack { get; set; }

        /// <summary>
        /// The subtitle track selected by default after opening the media
        /// </summary>
        public int DefaultSubtitleTrack { get; set; }

        /// <summary>
        /// Whether to enable hardware acceleration
        /// Not all videos support hardware acceleration.
        /// If you enable this option, hardware acceleration will be tried first, 
        /// and if it fails, the CPU will be used for decoding. 
        /// </summary>
        public bool EnableHWAccel { get; set; }

        /// <summary>
        /// The data frame decoded by hardware is actually in GPU memory, 
        /// and this mark indicates whether it is extracted into CPU memory
        /// </summary>
        public bool ExtractHWFrame { get; set; }

        /// <summary>
        /// Hardware device type when video hardware accelerates decoding 
        /// Not all of the current platforms are supported, 
        /// if the current option does not support, set as the default 
        /// </summary>
        public HWDeviceType HWAccelType { get; set; }

        /// <summary>
        /// Pixel format of output SCFrame 
        /// </summary>
        public PixelFormat OutputPixelFormat { get; set; }

        /// <summary>
        /// Media type
        /// Refer to MediaType for details
        /// </summary>
        public MediaType OpenMode { get; set; }

        /// <summary>
        /// Width camera resolution 
        /// </summary>
        public int CameraWidth { get; set; }

        /// <summary>
        /// Height camera resolution
        /// </summary>
        public int CameraHeight { get; set; }

        /// <summary>
        /// Camera fps
        /// </summary>
        public float CameraFPS { get; set; }

        /// <summary>
        /// format
        /// </summary>
        public string CameraExtended { get; set; }

        /// <summary>
        /// Mark whether the media is a file
        /// </summary>
        private bool IsFile { get; set; }

        /// <summary>
        /// No buffer read packet
        /// </summary>
        public bool Nobuffer { get; set; }

        /// <summary>
        /// Codec low delay
        /// </summary>
        public bool LowDelay { get; set; }

        /// <summary>
        /// RTSP transport by tcp
        /// </summary>
        public bool RtspTransportTCP { get; set; }

        /// <summary>
        /// Play directly after opening or stay at the first frame
        /// </summary>
        public bool OpenNoPlay { get; set; }

        /// <summary>
        /// Whether the media is played in a loop 
        /// This option is valid only when the mediaType is LocalOrNetworkFile 
        /// </summary>
        public bool Loop { get { return _loop; } set { _loop = value; SetLoop(_loop); } }
        private bool _loop;

        /// <summary>
        /// Current playback timestamp, valid when the mediaType is LocalOrNetFile
        /// </summary>
        public long CurrentTime { get; private set; }

        /// <summary>
        /// The total duration of the media, valid when the mediaType is LocalOrNetFile 
        /// </summary>
        public long Duration { get; private set; }

        /// <summary>
        /// Whether the marker is in a paused state 
        /// </summary>
        public bool IsPaused { get; private set; }

        /// <summary>
        /// Mark whether player is closed
        /// Open failure is also considered not close
        /// </summary>
        public bool Closed { get; set; }

        /// <summary>
        /// Mark whether player successfully opened media
        /// </summary>
        public bool OpenSuccessed { get; set; }

        /// <summary>
        /// Audio and video synchronization strategy, currently only supports AudioMaster 
        /// </summary>
        private SYNCType syncType = SYNCType.AudioMaster;

        /// <summary>
        /// Mark whether the video can be drawn currently for external use. 
        /// When the external drawing is completed, the mark value should be set to false
        /// </summary>
        public bool AllowDraw { get; set; }

        /// <summary>
        /// Audio volume
        /// </summary>
        public float Volume { get; set; }

        /// <summary>
        /// Playback Speed
        /// </summary>
        public float Speed { get { return adjust; } set { SetPlaybackSpeed(value); } }

        #region Internal use
        private GCHandle _gcHandle;                     //GC handle
        private IntPtr _capture;                        //Stream capture pointer
        private System.IntPtr audioPlayer;              //Audio player pointer
        private System.IntPtr resampler;                //Resampler pointer
        private System.IntPtr pcm;                      //PCM data buffer

        private bool _openNoPlay;                       //Whether to pause at the first frame
        private int _openIndex;                         //Mark open index
        private Thread _videoThreadHandle;              //Video thread handle
        private long _frameTime = 0;                    //Time line
        private bool waitAudioPlayer = false;           //Mark whether to wait for AudioPlayer 
        private bool isFirst = false;                   //Whether the mark is the first rendering after opening 
        private OpenCallbackContext streamCtx;          // Media information preview  
        private bool videoIsFinished = false;           //Mark the end of the video 
        private bool audioIsFinished = false;           //Mark the end of the audio 
        private bool isStep = false;                    //Mark step next video frame

        private SCClock videoClock;                     //clock
        private SCClock audioClock;                     //clock
        private SCClock externClock;                    //clock
        private int audioHWBufSize = 0;                 //audio device hardware buffer size
        private int bytesPerSec = 0;                    //The amount of data required for 1s audio
        private int sampleRate = 0;                     //The audio sample rate
        private double audioDiffThreshold;              //This attribute has no effect now 
        private bool nextClearPCMCache = false;         //Mark the next audio callback to clean up the data

        private readonly Mutex mux = new Mutex();       //Lock video frame
        private readonly Mutex resamplerMux = new Mutex();//Lock resampler frame
        private IntPtr frameBK;                         //Backup video frame
         private static Action<string> cameraInfoCallback;

        private float adjust;
        private bool canSpeed;
        private AudioOutputFormat audioOutputFmt = AudioOutputFormat.S16;
        #endregion // Internal use



        /// <summary>
        /// SCPlayer log, Convenient to use across platforms
        /// </summary>
        public static Action<LogLevel, string> onLog;

        /// <summary>
        /// Called when a video frame needs to be rendered, of course, you can choose to do nothing.
        /// @Tip:
        /// The buffer here is the video data. 
        /// The linesize[0] in the frame may be different from the width. 
        /// The alignment is related to the CPU. 
        /// You can use these video frame data directly. 
        /// If you expect to use 1-byte aligned video frames then please refer to onRenderFrameEvent in UnitySCPlayerPro
        /// </summary>
        public event Action<SCFrame> onDrawVideoFrameEvent;

        /// <summary>
        /// Called when a audio frame needs to be rendered, of course, you can choose to do nothing.
        /// @Tip:
        /// The buffer here is audio data, the format is s16, 
        /// if you need to use these data, you can use Marshal.
        /// Copy to copy the data, so that you can make full use of the data
        /// </summary>
        public event Action<IntPtr, int> onDrawAudioFrameEvent;

        /// <summary>
        /// Called when the video has finished playing, whether looping or not 
        /// </summary>
        public event Action onStreamFinishedEvent;

        /// <summary>
        /// Called when player demux succeeds or failed
        /// </summary>
        public event Action<CaptureOpenResult, string, OpenCallbackContext> onCaptureOpenCallbackEvent;

        /// <summary>
        /// Called when player demux read pakcet failed
        /// </summary>
        public event Action<string> onInterruptCallbackEvent;

        /// <summary>
        /// Initialize player
        /// We have to set some parameters and set some handles
        /// Every step here is important
        /// </summary>
        public SCPlayerPro()
        {
            DisableAudio = false;
            DisableVideo = false;
            DisableSubtitle = true;
            DefaultVideoTrack = DefaultAudioTrack = DefaultSubtitleTrack = 0;

            CameraWidth = 640;
            CameraHeight = 480;
            CameraFPS = 30;

            EnableHWAccel = true;
            ExtractHWFrame = true;

            HWAccelType = HWDeviceType.AUTO;
            OutputPixelFormat = PixelFormat.AUTO;
            OpenMode = MediaType.LocalOrNetworkFile;
            Volume = 1.0f;
            Speed = 1.0f;
            Closed = true;
            OpenSuccessed = false;
            _gcHandle = GCHandle.Alloc(this);
            _capture = ISCNative.CreateStreamCapture(GCHandle.ToIntPtr(_gcHandle));
            audioPlayer = ISCNative.CreateAudioPlayer(GCHandle.ToIntPtr(_gcHandle));
            resampler = ISCNative.CreateResampler();
            pcm = ISCNative.CreateByteArray();
            frameBK = ISCNative.CreateSCFrame(new SCFrame(), 0, (int)SCFrameFlag.Move);
            ISCNative.SetInterruptCallback(_capture, ISCNative.interruptCallback);
        }

        /// <summary>
        /// Lock current video frame, Use this method must be used together with UnlockFrame
        /// The update of video frame drawing is not necessarily in the main thread, so thread safety must be ensured
        /// </summary>
        /// <returns>current video frame</returns>
        public SCFrame LockFrame()
        {
            mux.WaitOne();
            return Marshal.PtrToStructure<SCFrame>(frameBK);
        }

        /// <summary>
        /// Unlock current video frame, Use this method must be used together with LockFrame
        /// The update of video frame drawing is not necessarily in the main thread, so thread safety must be ensured
        /// </summary>
        /// <returns>current video frame</returns>
        public void UnlockFrame()
        {
            mux.ReleaseMutex();
        }

        private void SetPlaybackSpeed(float speed)
        {
            adjust = speed;
            if (adjust > 2.0f)
                adjust = 2.0f;
            if (adjust < 0.5f)
                adjust = 0.5f;
        }

        public IntPtr GetAVFramePointer()
        {
            return frameBK;
        }

        /// <summary>
        /// Open media
        /// </summary>
        /// <param name="url">When Null is passed, it will be opened by default according to the global variable url</param>
        public void Open(MediaType openMode, string url)
        {
            this.OpenMode = openMode;
            if (_capture == IntPtr.Zero) return;
            if (!CheckUrlSupport(url))
                return;
            Close();
            Closed = false;
            SCConfiguration config = new SCConfiguration();
            config.disableAudio = DisableAudio ? 1 : 0;
            config.disableVideo = DisableVideo ? 1 : 0;
            config.disableSubtitle = DisableSubtitle ? 1 : 0;
            config.videoTrack = DefaultVideoTrack;
            config.audioTrack = DefaultAudioTrack;
            config.subtitleTrack = DefaultSubtitleTrack;
            config.enableHWAccel = EnableHWAccel ? 1 : 0;
            config.extractHWFrame = ExtractHWFrame ? 1 : 0;
            config.hwaccelType = (int)HWAccelType;
            config.outputPixfmt = (int)OutputPixelFormat;
            config.openMode = (int)openMode;
            config.cameraWidth = CameraWidth;
            config.cameraHeight = CameraHeight;
            config.cameraFPS = CameraFPS;
            config.cameraExtended = GetCameraExtended();
            config.nobuffer = Nobuffer ? 1 : 0;
            config.lowDelay = LowDelay ? 1 : 0;
            config.rtspTransportTcp = RtspTransportTCP ? 1 : 0;

            _openNoPlay = OpenNoPlay;
            _openIndex++;
            isFirst = true;
            audioClock = new SCClock();
            videoClock = new SCClock();
            externClock = new SCClock();
            SetLoop(Loop);
            AllowDraw = false;
            ISCNative.AsyncOpenStreamCapture(_capture, ISCNative.StringToByteArray(url.Trim()), config, ISCNative.captureOpenCallback);

        }

        private byte[] GetCameraExtended()
        {
            byte[] retbuffer = new byte[64];
            if (string.IsNullOrEmpty(CameraExtended))
                return retbuffer;
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(CameraExtended);
            int minLen = Math.Min(buffer.Length, retbuffer.Length);
            for (int i = 0; i < minLen; i++)
                retbuffer[i] = buffer[i];
            return retbuffer;
        }

        /// <summary>
        /// Close media
        /// </summary>
        public void Close()
        {
            Closed = true;
            OpenSuccessed = false;
            if (_videoThreadHandle != null)
            {
                _videoThreadHandle.Join();
                _videoThreadHandle = null;
            }
            if (_capture == IntPtr.Zero) return;

            ISCNative.PausedAudioPlayer(audioPlayer, 1);
            ISCNative.CloseStreamCapture(_capture);
            ISCNative.CloseAudioPlayer(audioPlayer);
            resamplerMux.WaitOne();
            ISCNative.CloseResampler(resampler);
            resamplerMux.ReleaseMutex();
            nextClearPCMCache = true;
        }

        /// <summary>
        /// Check url support or not
        /// </summary>
        /// <param name="url">Input url</param>
        /// <returns></returns>
        private bool CheckUrlSupport(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                if (onLog != null)
                    onLog(LogLevel.Error, "The URL cannot be empty or NULL .");
                return false;
            }
            if (OpenMode == MediaType.LocalOrNetworkFile)
            {
                string internalUrl = url.Trim().ToLower();

                if (internalUrl.StartsWith("http"))
                {
                    if (internalUrl.EndsWith("m3u8"))
                    {
                        if (onLog != null)
                            onLog(LogLevel.Error, "User-specified URL types are not supported in the current version, Please refer to the user manual for details .");

                        return false;
                    }
                }
                else
                {
                    if (!System.IO.File.Exists(url))
                    {
                        if (onLog != null)
                            onLog(LogLevel.Error, "No such file or directory: " + url.Trim());
                        return false;
                    }
                }
                return true;
            }
            else if (OpenMode == MediaType.Link)
            {
                string internalUrl = url.Trim().ToLower();
                return true;
            }
            else if (OpenMode == MediaType.Camera)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Set whether the video is played in a loop
        /// </summary>
        /// <param name="isLoop">loop or not</param>
        public void SetLoop(bool isLoop)
        {
            if (_capture == System.IntPtr.Zero)
                return;
            _loop = isLoop;
            ISCNative.SetCaptureLoop(_capture, isLoop ? 1 : 0);
        }

        /// <summary>
        /// stream is finished
        /// </summary>
        /// <param name="type"></param>
        private void OnStreamFinished(FrameType type)
        {
            if (streamCtx.videoParams == null) videoIsFinished = true;
            if (streamCtx.audioParams == null) audioIsFinished = true;
            if (type == FrameType.Video) videoIsFinished = true;
            if (type == FrameType.Audio) audioIsFinished = true;
            if (audioIsFinished && videoIsFinished)
            {
                Dispatcher.Invoke(() =>
                {
                    if (onStreamFinishedEvent != null)
                    {
                        try
                        {
                            onStreamFinishedEvent();
                        }
                        catch { }
                    }
                    if (!Loop)
                        CurrentTime = Duration;
                });
                videoIsFinished = audioIsFinished = false;
            }
        }

        /// <summary>
        /// update pts 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="pts"></param>
        private void UpdatePTS(FrameType type, long pts)
        {
            if (streamCtx.videoParams != null)
            {
                if (type == FrameType.Video)
                    CurrentTime = pts;
            }
            else if (streamCtx.audioParams != null)
            {
                if (type == FrameType.Audio)
                    CurrentTime = pts;
            }
            if (CurrentTime > Duration)
                CurrentTime = Duration;
        }

        /// <summary>
        /// Video thread 
        /// </summary>
        private void VideoThread()
        {
            int remainingTime = 0;
            _frameTime = 0;
            while (!Closed)
            {
                if (waitAudioPlayer)
                {
                    Thread.Sleep(2);
                    continue;
                }
                if (remainingTime > 0)
                    Thread.Sleep(remainingTime);
                remainingTime = 10;
                if (streamCtx.realtime && Nobuffer)
                {
                    int pCount = 0, fCount = 0;
                    ISCNative.GetPFCount(_capture, (int)FrameType.Video, ref pCount, ref fCount);
                    if (pCount > 10)
                        _frameTime -= 2;
                }
                if (!IsPaused)
                    RenderVideo(ref remainingTime);
            }
        }


        /// <summary>
        /// Control video playback 
        /// </summary>
        /// <param name="remaining_time">remaining time</param>
        private void RenderVideo(ref int remaining_time, bool dropOnly = false)
        {
            SCFrame frame = new SCFrame();
            SCFrame lastFrame = new SCFrame();
            int ret = TryGrabFrame(FrameType.Video, ref frame);
            if (ret == 0)
                return;
            if (ret < 0)
                throw new Exception("TryGrabFrame Error");


            ret = TryGrabLastFrame(FrameType.Video, ref lastFrame);
            if (ret < 0)
                throw new Exception("TryGrabFrame Error");

            if (dropOnly)
                goto finished;
            if (frame.context_type == (int)FrameContextType.EOF)
            {
                OnStreamFinished(FrameType.Video);
                ISCNative.FrameMoveToLast(_capture, (int)FrameType.Video);
                return;
            }
            UpdatePTS((FrameType)frame.media_type, frame.pts_ms);
            double last_duration = LastDuration(lastFrame, frame);
            double delay = ComputeTargetDelay(last_duration);
            double time = ISCNative.GetTimestampUTC() / 1000;
            if (time < _frameTime + delay)
            {
                remaining_time = Math.Min((int)(_frameTime + delay - time), remaining_time);
                return;
            }

            float adjust = canSpeed ? this.adjust : 1.0f;
            if (OpenMode != MediaType.Camera)
            {
                _frameTime += (int)(delay * (1.0f / adjust));
                if (delay > 0 && time - _frameTime > AV_SYNC_THRESHOLD_MAX)
                    _frameTime = (long)time;
            }


            videoClock.SetClock(frame.pts_ms);
            externClock.SyncClockToSlave(videoClock, false);

            mux.WaitOne();
            ISCNative.ImageCopy(frameBK, frame);
            AllowDraw = true;
            mux.ReleaseMutex();
            if (onDrawVideoFrameEvent != null)
            {
                try
                {
                    onDrawVideoFrameEvent(frame);
                }
                catch { }
            }
            if (isFirst)
            {
                isFirst = false;
                Dispatcher.Invoke(() =>
                {
                    if (!Closed && audioPlayer != IntPtr.Zero)
                        ISCNative.PausedAudioPlayer(audioPlayer, 0);
                });
            }


        finished:
            ISCNative.FrameMoveToLast(_capture, (int)FrameType.Video);
            if (isStep)
            {
                isStep = false;
                IsPaused = true;
            }
        }

        /// <summary>
        /// Calculate the time interval between two frames
        /// </summary>
        /// <param name="lastFrame">last frame</param>
        /// <param name="frame">current frame</param>
        /// <returns></returns>
        private double LastDuration(SCFrame lastFrame, SCFrame frame)
        {
            double duration = frame.pts_ms - lastFrame.pts_ms;
            if (double.IsNaN(duration) || duration <= 0 || duration > 10000)
                return lastFrame.duration;
            else
                return duration;
        }

        /// <summary>
        /// Calculate the delay time 
        /// </summary>
        /// <param name="delay"></param>
        /// <returns></returns>
        private double ComputeTargetDelay(double delay)
        {
            //Time difference between video clock and main clock 
            double diff = 0;
            if (streamCtx.realtime && Nobuffer)
                diff = videoClock.GetClock() - videoClock.GetClock();
            else
                diff = videoClock.GetClock() - GetMasterClock().GetClock();
            double sync_threshold = Math.Max(AV_SYNC_THRESHOLD_MIN, Math.Min(AV_SYNC_THRESHOLD_MAX, (float)delay));

            //When the video is slower than the audio and exceeds the threshold 
            if (diff <= -sync_threshold)
            {
                delay = Math.Max(0, (float)(delay + diff));
                //Debug.LogWarningFormat("Video is too slow, more than the threshold:{0:F1}", -diff / 1000);
            }
            else if (diff >= sync_threshold && delay > AV_SYNC_FRAMEDUP_THRESHOLD)
                delay = delay + diff;
            //When video is faster than audio 
            else if (diff >= sync_threshold)
                delay = 1.5 * delay;

            return delay;
        }

        /// <summary>
        /// Get master clock
        /// </summary>
        /// <returns></returns>
        private SCClock GetMasterClock()
        {
            SCClock clock = null;
            switch (syncType)
            {
                case SYNCType.AudioMaster:
                    clock = audioClock;
                    break;
            }
            return clock;
        }


        /// <summary>
        /// try grab frame
        /// </summary>
        /// <param name="type">frame type</param>
        /// <param name="frame">frame</param>
        /// <returns>
        /// return 0 capture is null, return -1 error, 0 is ok
        /// </returns>
        private int TryGrabFrame(FrameType type, ref SCFrame frame)
        {
            if (_capture == IntPtr.Zero)
                return 0;
            IntPtr ptr = new IntPtr();
            int ret = ISCNative.TryGrabFrame(_capture, (int)type, ref ptr);
            if (ret > 0)
            {
                frame = Marshal.PtrToStructure<SCFrame>(ptr);
                //HWCtx = Marshal.ReadIntPtr(ptr, 208);
            }
            return ret;
        }

        /// <summary>
        /// try grab last frame
        /// </summary>
        /// <param name="type">frame type</param>
        /// <param name="frame">frame</param>
        /// <returns>
        /// return 0 capture is null, return -1 error, 0 is ok
        /// </returns>
        private int TryGrabLastFrame(FrameType type, ref SCFrame frame)
        {
            if (_capture == IntPtr.Zero)
                return 0;
            IntPtr ptr = new IntPtr();
            int ret = ISCNative.TryGrabLastFrame(_capture, (int)type, ref ptr);
            if (ret > 0)
                frame = Marshal.PtrToStructure<SCFrame>(ptr);
            return ret;
        }

        public void FrameMoveToLast(FrameType type)
        {
            if (_capture == IntPtr.Zero)
                return;
            ISCNative.FrameMoveToLast(_capture, (int)type);
        }

        /// <summary>
        /// This function is valid if and only when mediaType is LocalOrNetworkFile
        /// seek media to first frame
        /// </summary>
        /// <param name="paused">paused or not</param>
        public void Replay(bool paused)
        {
            if (OpenMode != MediaType.LocalOrNetworkFile)
                return;

            bool isFirstFrame = CurrentTime == 0;
            if (!isFirstFrame)
                SeekFastPercent(0);

            IsPaused = paused;
            if (IsPaused)
            {
                if (!isFirstFrame)
                    StepNextFrame();
            }
            else
                Play();
        }

        /// <summary>
        /// Seek to key frame quickly according to percentage
        /// </summary>
        /// <param name="percent"></param>
        public void SeekFastPercent(double percent)
        {
            if (_capture == System.IntPtr.Zero || streamCtx == null)
                return;
            ISCNative.SeekFastPercent(_capture, percent);
            CurrentTime = (long)(Duration * percent);
            SeekReset();
        }

        /// <summary>
        /// Seek to key frame quickly according to ms
        /// </summary>
        /// <param name="ms"></param>
        public void SeekFastMilliSecond(int ms)
        {
            if (_capture == System.IntPtr.Zero || streamCtx == null)
                return;
            if (ms < 0) ms = 0;
            if (ms > Duration) ms = (int)Duration;
            ISCNative.SeekFastMs(_capture, ms);
            CurrentTime = ms;
            SeekReset();
        }

        /// <summary>
        /// Play media
        /// </summary>
        public void Play()
        {
            if (streamCtx != null && streamCtx.realtime && IsPaused)
                ISCNative.ClearAllCache(_capture);
            if (_openNoPlay)
                _openNoPlay = false;
            IsPaused = false;
            isStep = false;
        }

        /// <summary>
        /// Pause media
        /// </summary>
        public void Pause()
        {
            if (streamCtx != null && streamCtx.realtime)
                ISCNative.ClearAllCache(_capture);
            IsPaused = true;
            if (streamCtx != null && streamCtx.realtime)
                ISCNative.ClearAllCache(_capture);
        }

        /// <summary>
        /// Reset related attributes 
        /// </summary>
        private void SeekReset()
        {
            if (streamCtx.audioParams != null)
                nextClearPCMCache = true;
            audioIsFinished = false;
            videoIsFinished = false;
            _frameTime = 0;

            if (IsPaused && streamCtx.videoParams != null)
                StepNextFrame();
        }

        /// <summary>
        /// seek to the next video frame 
        /// </summary>
        private void StepNextFrame()
        {
            if (streamCtx.videoParams == null) return;
            IsPaused = false;
            isStep = true;
        }


        /// <summary>
        /// Release all resources of the player. 
        /// All operations will be invalid after the function is called.
        /// </summary>
        public void Release()
        {
            Close();
            if (_capture == IntPtr.Zero) return;
            ISCNative.ReleaseStreamCapture(_capture);
            _gcHandle.Free();
            _capture = IntPtr.Zero;

            ISCNative.ReleaseAudioPlayer(audioPlayer);
            audioPlayer = IntPtr.Zero;

            ISCNative.ReleaseResampler(resampler);
            resampler = IntPtr.Zero;

            ISCNative.ReleaseByteArray(pcm);
            pcm = IntPtr.Zero;

            ISCNative.ReleaseSCFrame(frameBK);
            frameBK = IntPtr.Zero;
        }

        /// <summary>
        /// When the Open function is called, 
        /// the function will be called back regardless of whether it is opened or not, 
        /// unless you call the Close function before the successful opening
        /// </summary>
        /// <param name="state">open result</param>
        /// <param name="error">error infomation</param>
        /// <param name="context">video or audio param</param>
        public void CaptureOpenCallback(CaptureOpenResult state, string error, OpenCallbackContext ctx)
        {
            int index = _openIndex;
            IsFile = ctx == null ? false : ctx.localfile;
            Dispatcher.Invoke(() =>
            {
                if (index != _openIndex || Closed) return;
                CaptureOpenCallback_MainThread(state, error, ctx);
            });
        }

        public void InterruptCallback(string error)
        {
            Dispatcher.Invoke(() =>
            {
                InterruptCallback_MainThread(error);
            });
        }

        /// <summary>
        /// When the Open function is called, 
        /// the function will be called back regardless of whether it is opened or not, 
        /// unless you call the Close function before the successful opening
        /// </summary>
        /// <param name="code">open result</param>
        /// <param name="error">error infomation</param>
        /// <param name="param">video or audio param</param>
        public void AudioPlayerOpenCallback(AudioPlayerOpenResult code, string error, PlayerParams param)
        {
            if (code == AudioPlayerOpenResult.SUCCESS)
            {
                audioHWBufSize = param.hwSize;
                bytesPerSec = param.dstap.freq * ISCNative.GetBytesPerSample((int)audioOutputFmt) * param.dstap.channels;
                sampleRate = param.dstap.freq;
                audioDiffThreshold = (double)audioHWBufSize / bytesPerSec * 1000;
                resamplerMux.WaitOne();
                int ret = ISCNative.OpenResampler(resampler, param.srcap, param.dstap);
                if (ret < 0)
                {
                    ISCNative.SCLog(LogLevel.Critical, "OpenResampler failed");
                }
                resamplerMux.ReleaseMutex();
                int index = _openIndex;
                Dispatcher.Invoke(() =>
                {
                    if (audioPlayer == IntPtr.Zero) return;
                    waitAudioPlayer = false;
                    if (!Closed && OpenMode == MediaType.Link && !IsFile && index == _openIndex)
                    {
                        if(audioPlayer != IntPtr.Zero)
                            ISCNative.PausedAudioPlayer(audioPlayer, 0);
                    }
                });
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    if (audioPlayer == IntPtr.Zero) return;
                    if (streamCtx != null && streamCtx.audioParams != null)
                    {
                        waitAudioPlayer = false;
                    }
                });
                if (onLog != null)
                    onLog(LogLevel.Warning, error);
            }

        }

        /// <summary>
        /// AudioPlayer callback function 
        /// Fill in the data for the audioplayer in this function
        /// </summary>
        /// <param name="buffer">Target buffer pointer </param>
        /// <param name="len">buffer len</param>
        public void AudioPlayCallback(System.IntPtr buffer, int len)
        {
            ISCNative.Memset(buffer, 0, len);
            if (SCMGR.IsPaused)
                return;

            if (nextClearPCMCache)
            {
                ISCNative.ClearByteArray(pcm);
                resamplerMux.WaitOne();
                ISCNative.ResampleClear(resampler);
                resamplerMux.ReleaseMutex();
                nextClearPCMCache = false;
            }

            long audioCallbackTime = ISCNative.GetTimestampUTC();
            int audioClockTs = 0;
            while (!Closed && !IsPaused && ISCNative.GetByteArraySize(pcm) < len)
            {
                SCFrame frame = new SCFrame();
                int ret = TryGrabFrame(FrameType.Audio, ref frame);
                if (ret < 0)
                    break;
                if (ret == 0)
                {
                    Thread.Sleep(2);
                    continue;
                }
                if (frame.context_type == (int)FrameContextType.EOF)
                {
                    OnStreamFinished(FrameType.Audio);
                    ISCNative.FrameMoveToLast(_capture, (int)FrameType.Audio);
                    break;
                }
                UpdatePTS((FrameType)frame.media_type, frame.pts_ms);
                float adjust = canSpeed ? this.adjust : 1.0f;
                if (streamCtx.realtime && Nobuffer)
                {
                    int pCount = 0, fCount = 0;
                    ISCNative.GetPFCount(_capture, (int)FrameType.Audio, ref pCount, ref fCount);
                    if (pCount <= 10)
                        adjust = 0.9f;
                    else if (pCount >= 20)
                        adjust = 1.05f;
                    else
                        adjust = 1.0f;
                }
                resamplerMux.WaitOne();
                System.IntPtr resampleDataPtr = ISCNative.ResamplePush(resampler, frame.data, frame.nb_samples);
                ResampleData rd = Marshal.PtrToStructure<ResampleData>(resampleDataPtr);
                System.IntPtr soundDataPtr = ISCNative.ResampleTempo(resampler, rd.data[0], rd.nbSamples, adjust);
                if (soundDataPtr != System.IntPtr.Zero)
                {
                    SoundData sd = Marshal.PtrToStructure<SoundData>(soundDataPtr);
                    ISCNative.PushDataToByteArray(pcm, sd.data, sd.length);
                    if (onDrawAudioFrameEvent != null)
                    {
                        try
                        {
                            onDrawAudioFrameEvent(sd.data, sd.length);
                        }
                        catch { }
                    }
                }
                ISCNative.ResamplePop(resampler, rd.nbSamples);
                resamplerMux.ReleaseMutex();
                audioClockTs = (int)(frame.pts_ms + (double)frame.nb_samples / frame.sample_rate * 1000);
                ISCNative.FrameMoveToLast(_capture, (int)FrameType.Audio);
            }
            if (IsPaused)
            {
                ISCNative.Memset(buffer, 0, len);
                return;
            }

            int minLen = Math.Min(len, ISCNative.GetByteArraySize(pcm));

            ISCNative.MixAudioFormat(buffer, ISCNative.GetByteArrayData(pcm), minLen, Volume, (int)audioOutputFmt);
            ISCNative.RemoveRangeFromByteArray(pcm, minLen);

            double noplayBuffSize = audioHWBufSize + ISCNative.GetByteArraySize(pcm);
            int noplayms = (int)(noplayBuffSize / bytesPerSec * 1000);
            resamplerMux.WaitOne();
            int noplaySamples = ISCNative.GetUnprocessedSamples(resampler) * 1000 / sampleRate;
            resamplerMux.ReleaseMutex();
            int crtpts = audioClockTs - noplayms - noplaySamples;
            if (audioClock != null) audioClock.SetClockAt(crtpts, audioCallbackTime / 1000);
        }

        private void CaptureOpenCallback_MainThread(CaptureOpenResult state, string error, OpenCallbackContext ctx)
        {
            if (state == CaptureOpenResult.SUCCESS)
            {
                OpenSuccessed = true;
                streamCtx = ctx;
                Duration = ctx.duration;
                IsPaused = _openNoPlay;
                if (IsPaused)
                    StepNextFrame();

                if (ctx.videoParams != null)
                {
                    if (_videoThreadHandle != null)
                        throw new System.Exception("video thread ready exists!");
                    _videoThreadHandle = new Thread(VideoThread);
                    _videoThreadHandle.Start();
                }
                if (ctx.audioParams != null)
                {
                    ISCNative.AsyncOpenAudioPlayer(audioPlayer, (int)audioOutputFmt, ctx.audioParams, ctx.videoParams != null ? 1 : 0, ISCNative.audioOpenCallback, ISCNative.audioPlayCallback);
                    waitAudioPlayer = true;
                }
                else
                    waitAudioPlayer = false;
                canSpeed = streamCtx.localfile;
            }
            else
            {
                if (onLog != null)
                    onLog(LogLevel.Warning, error);
            }

            if (onCaptureOpenCallbackEvent != null)
            {
                try
                {
                    onCaptureOpenCallbackEvent(state, error, ctx);
                }
                catch { }
            }
        }

        public void InterruptCallback_MainThread(string error)
        {
            if (Closed) return;
            if (onInterruptCallbackEvent != null)
            {
                try
                {
                    onInterruptCallbackEvent(error);
                }
                catch
                { }
            }
        }

        public static List<string> GetDeviceList(DeviceType type)
        {
            List<string> devices = new List<string>();
            IntPtr strlist = ISCNative.GetDevicesList((int)type, 0);
            IntPtr opaque = IntPtr.Zero;
            IntPtr name = ISCNative.StringIterate(strlist, ref opaque);
            while (name != IntPtr.Zero)
            {
                devices.Add(Marshal.PtrToStringAnsi(name));
                name = ISCNative.StringIterate(strlist, ref opaque);
            }
            ISCNative.ReleaseDevicesList(strlist);
            return devices;
        }

        public static void GetCameraInfomation(string deviceName, Action<string> cb)
        {
            cameraInfoCallback = cb;
            ISCNative.CaminfoCallbackDelegate icb = GetCameraInfoCallback;
            ISCNative.GetCameraInfomation(ISCNative.StringToByteArray(deviceName), IntPtr.Zero, icb, 0);
        }

        private static void GetCameraInfoCallback(IntPtr user, IntPtr info)
        {
            string infostr = Marshal.PtrToStringUni(info);
            byte[] bs = System.Text.Encoding.Unicode.GetBytes(infostr);
            infostr = System.Text.Encoding.UTF8.GetString(bs);
            if (cameraInfoCallback != null) cameraInfoCallback(infostr.Remove(infostr.LastIndexOf('\n')));
        }

        /// <summary>
        /// Release all
        /// </summary>
        ~SCPlayerPro()
        {
            Release();
        }
    }
}
