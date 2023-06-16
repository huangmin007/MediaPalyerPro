using System;
using System.Linq;
using System.Runtime.InteropServices;

/// <summary>
/// This file defines the C# interface of the external function,
/// Of course you can port it to other programs
/// </summary>
namespace Sttplay.MediaPlayer
{
    /// <summary>
    /// log level , such as Log, LogWarning, LogError
    /// </summary>
    public enum LogLevel
    {
        Info,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// All hardware deviceType data types are enumerated here
    /// This does not mean that the platform supports all types
    /// </summary>
    public enum HWDeviceType
    {
        AUTO = 0,
        VDPAU,
        CUDA,
        VAAPI,
        DXVA2,
        QSV,
        VIDEOTOOLBOX,
        D3D11VA,
        DRM,
        OPENCL,
        MEDIACODEC,
        VULKAN
    }

    /// <summary>
    /// All Output pixel format data types are enumerated here
    /// Automatic selection will be set as the default output format. 
    /// If the replaced output format is not in the enumerated range, 
    /// it will be automatically selected as BGRA again. 
    /// </summary>
    public enum PixelFormat
    {
        AUTO = -1,

        YUV420P = 0,
        YUYV422 = 1,
        UYVY422 = 15,
        YUV422P = 4,
        YUV444P = 5,

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

    };

    public enum SampleFormat
    {

        SAM_FMT_UNKOW = -1,
        SAM_FMT_U8,          ///< unsigned 8 bits
        SAM_FMT_S16,         ///< signed 16 bits
        SAM_FMT_S32,         ///< signed 32 bits
        SAM_FMT_FLT,         ///< float
        SAM_FMT_DBL,         ///< double

        SAM_FMT_U8P,         ///< unsigned 8 bits, planar
        SAM_FMT_S16P,        ///< signed 16 bits, planar
        SAM_FMT_S32P,        ///< signed 32 bits, planar
        SAM_FMT_FLTP,        ///< float, planar
        SAM_FMT_DBLP,        ///< double, planar
        SAM_FMT_S64,         ///< signed 64 bits
        SAM_FMT_S64P,        ///< signed 64 bits, planar
    };

    public enum AudioOutputFormat
    {
        S16 = SampleFormat.SAM_FMT_S16,
        S32 = SampleFormat.SAM_FMT_S32,
        FLT = SampleFormat.SAM_FMT_FLT
    }

    /// <summary>
    /// Create SCFrame mark, mark whether to copy or move data when calling ImageCopy
    /// </summary>
    public enum SCFrameFlag
    {
        Copy = 0,
        Move
    }

    /// <summary>
    /// open configuration
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 120)]
    public class SCConfiguration
    {
        [FieldOffset(0)]
        public int disableVideo = 0;
        [FieldOffset(4)]
        public int disableAudio = 0;

        [FieldOffset(8)]
        public int disableSubtitle = 1;

        [FieldOffset(12)]
        public int videoTrack = 0;
        [FieldOffset(16)]
        public int audioTrack = 0;
        [FieldOffset(20)]
        public int subtitleTrack = 0;

        [FieldOffset(24)]
        public int enableHWAccel = 1;
        [FieldOffset(28)]
        public int hwaccelType = (int)HWDeviceType.AUTO;
        [FieldOffset(32)]
        public int extractHWFrame = 1;       //extract hwaccel frame, there set 1
        [FieldOffset(36)]
        public int outputPixfmt = (int)PixelFormat.AUTO;

        [FieldOffset(40)]
        public int openMode = (int)MediaType.LocalOrNetworkFile;

        [FieldOffset(44)]
        public int cameraWidth = 640;
        [FieldOffset(48)]
        public int cameraHeight = 480;
        [FieldOffset(52)]
        public float cameraFPS = 30.0f;
        [FieldOffset(56)]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] cameraExtended;

        [FieldOffset(120)]
        public int nobuffer = 0;
        [FieldOffset(124)]
        public int lowDelay = 0;
        [FieldOffset(128)]
        public int rtspTransportTcp = 0;
    };

    /// <summary>
    /// Basic video parameters 
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public class VideoParams
    {
        public int pixfmt;
        public int width;
        public int height;
        public float fps;
    };

    /// <summary>
    /// Basic audio parameters 
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public class AudioParams
    {
        public int freq;
        public int channels;
        public long channel_layout;
        public int fmt;
    };

    /// <summary>
    /// Basic audio parameters 
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public class SubtitleParams
    {
        public int reserve;
    };

    /// <summary>
    /// Basic media parameters
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public class OpenCallbackContext
    {
        public long duration;
        public VideoParams videoParams;
        public AudioParams audioParams;
        public SubtitleParams subtitleParams;
        public bool realtime;
        public bool localfile;
    };

    /// <summary>
    /// This parameter will be used after audioplayer is successfully opened
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public class PlayerParams
    {
        /// <summary>
        /// Media audio parameters 
        /// </summary>
        public AudioParams srcap;

        /// <summary>
        /// In some special cases, the device does not support the audio format of the media, 
        /// so you need to change the audio output format
        /// </summary>
        public AudioParams dstap;

        /// <summary>
        /// hardware buffer size
        /// </summary>
        public int hwSize;
    }

    /// <summary>
    /// video, audio
    /// </summary>
    public enum FrameType
    {
        Video = 0,
        Audio = 1,
        Subtitle = 3
    }

    /// <summary>
    /// The meaning of context_type extension of SCFrame structure
    /// </summary>
    public enum FrameContextType
    {
        FRAME = 0,
        EOF             //Everything in this frame is invalid, to the end of the video
    }

    /// <summary>
    /// An important structure 
    /// You can get useful data from this structure 
    /// </summary>
    public struct SCFrame
    {
        public int media_type;          //For details, please see FrameType
        public int context_type;        //For details, please see FrameContextType
        public int width;               //frame pixel width
        public int height;              //frame pixel height
        public int width_ps;            //frame physics width (calculate aspect ratio)
        public int height_ps;           //frame physics height (calculate aspect ratio)
        public int format;              //frame format
        public int color_range;         //color range
        public int color_space;         //color space
        public long pts;                //pts of media files 
        public long pts_ms;             //The pts of media files are based on physical time
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public int[] linesize;          //line size, and width are not necessarily the same
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public IntPtr[] data;           //data pointer
        public double duration;         //duration

        public int nb_samples;          //number of audio samples (per channel) described by this frame
        public int sample_rate;         //sample rate

        public IntPtr avframe;          //avframe
        public IntPtr subtitle;
        public int uploaded;
    }

    /// <summary>
    /// capture open result
    /// </summary>
    public enum CaptureOpenResult
    {
        SUCCESS = 0,
        CERTIFICATE_INVALID = -1,
        PARAMETERS_ERROR = -2,
        FAILED = -3,
    }

    public enum DeviceType
    {
        VideoInput = 0,
        AudioInput
    }

    /// <summary>
    /// audio player open result
    /// </summary>
    public enum AudioPlayerOpenResult
    {
        SUCCESS = 0,
        DeviceError = -1,
        FormatNotSupport = -2
    }

    /// <summary>
    /// resample struct
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ResampleData
    {
        public int length;
        public int nbSamples;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public IntPtr[] data;
    }

    /// <summary>
    /// resample struct
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    struct SoundData
    {
        public int length;
        public IntPtr data;
    };

    public enum AudioDriverType
    {
        Auto,
        DirectSound,
        Winmm
    }

    public class ISCNative
    {
        public const string SCCore = "SCCore.dll";

        public const string SCUtility = "SCUtility.dll";

        public const string AudioPlayer = "SCAudioPlayer.dll";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LogCallbackDelegate(int level, IntPtr log);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void CaptureOpenCallbackDelegate(IntPtr user, int state, IntPtr error, IntPtr ctx);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void InterruptCallbackDelegate(IntPtr user, IntPtr error);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void AudioPlayCallbackDelegate(IntPtr user, IntPtr buffer, int len);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void AudioOpenCallbackDelegate(IntPtr user, IntPtr playerParams, int code, IntPtr err);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void CaminfoCallbackDelegate(IntPtr user, IntPtr info);

        //**********************************************************************************************
        //                                  SCUtility
        //**********************************************************************************************
        [DllImport(SCUtility, CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitSCLog(LogCallbackDelegate cb);

        [DllImport(SCUtility, CallingConvention = CallingConvention.Cdecl)]
        public static extern void DeInitSCLog();

        [DllImport(SCUtility, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SCLog(int level, byte[] log);

        [DllImport(SCUtility, CallingConvention = CallingConvention.Cdecl)]
        public static extern long GetTimestampUTC();

        [DllImport(SCUtility, CallingConvention = CallingConvention.Cdecl)]
        public static extern long GetTimestamp();

        [DllImport(SCUtility, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SleepMs(int ms);

        [DllImport(SCUtility, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Memset(IntPtr dst, byte val, int len);

        [DllImport(SCUtility, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Memcopy(IntPtr dst, IntPtr src, int len);

        //**********************************************************************************************
        //                                  SCCore
        //**********************************************************************************************

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GlobalThreadPoolTaskCount();

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr StreamCaptureConfiguration();

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeStreamCapturePro();

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern void TerminateStreamCapturePro();

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CreateStreamCapture(IntPtr user);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ReleaseStreamCapture(IntPtr capture);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern void AsyncOpenStreamCapture(IntPtr capture, byte[] url, SCConfiguration configuration, CaptureOpenCallbackDelegate cb);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern void CloseStreamCapture(IntPtr capture);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetInterruptCallback(IntPtr capture, InterruptCallbackDelegate cb);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetCaptureLoop(IntPtr capture, int loop);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SeekFastPercent(IntPtr capture, double percent);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SeekFastMs(IntPtr capture, int ms);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern int TryGrabFrame(IntPtr capture, int type, ref IntPtr frame);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern int TryGrabLastFrame(IntPtr capture, int type, ref IntPtr frame);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern int TryGrabNextFrame(IntPtr capture, int type, ref IntPtr frame);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetStreamIndex(IntPtr capture, int type, int index);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetStreamIndex(IntPtr capture, int type, ref int index);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ClearAllCache(IntPtr capture);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FrameMoveToLast(IntPtr capture, int type);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetDevicesList(int type, int raw);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ReleaseDevicesList(IntPtr capture);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr StringIterate(IntPtr list, ref IntPtr opaque);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern void GetCameraInfomation(byte[] camname, IntPtr user, CaminfoCallbackDelegate cb, int async);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CreateSCFrame(SCFrame src, int align, int flags);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ReleaseSCFrame(IntPtr src);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ImageCopy(IntPtr dst, SCFrame src);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr MemoryAlignment(IntPtr srcdata, int linepixelsize, int height, int linesize, byte[] destdata);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr FrameMoveRef(IntPtr dst, SCFrame src);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetMediaInfo(IntPtr capture);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetSubtitle(IntPtr capture, IntPtr frame);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetPFCount(IntPtr capture, int type, ref int packetCount, ref int frameCount);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CreateFrameConvert();

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ReleaseFrameConvert(IntPtr convert);

        [DllImport(SCCore, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ConvertFrame(IntPtr convert, SCFrame scframe, int format);


        //**********************************************************************************************
        //                                  AudioPlayer
        //**********************************************************************************************
        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr AudioPlayerConfiguration();

        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitilizeAudioPlayer(int audioDriver);

        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern void TerminateAudioPlayer();

        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CreateAudioPlayer(IntPtr user);

        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ReleaseAudioPlayer(IntPtr player);

        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern void AsyncOpenAudioPlayer(IntPtr player, int format, AudioParams inputAS, int paused, AudioOpenCallbackDelegate opencb, AudioPlayCallbackDelegate playcb);

        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern void CloseAudioPlayer(IntPtr player);

        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl, SetLastError =true)]
        public static extern void PausedAudioPlayer(IntPtr player, int isPaused);

        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern void MixAudioFormat(IntPtr stream, IntPtr src, int len, float volume, int format);
        
        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetBytesPerSample(int format);

        //**********************************************************************************************
        //                                  Resampler
        //**********************************************************************************************
        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CreateResampler();

        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ReleaseResampler(IntPtr resampler);

        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern int OpenResampler(IntPtr resampler, AudioParams srcap, AudioParams dstap);

        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern void CloseResampler(IntPtr resampler);

        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ResamplePush(IntPtr resampler, IntPtr[] data, int nbSamples);

        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ResamplePop(IntPtr resampler, int nbSamples);

        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ResampleTempo(IntPtr resampler, IntPtr data, int nbSamples, float tempo);

        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetUnprocessedSamples(IntPtr resampler);

        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ResampleClear(IntPtr resampler);

        //=====================================================================
        // ByteArray
        //=====================================================================
        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CreateByteArray();

        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ReleaseByteArray(IntPtr arr);

        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetByteArraySize(IntPtr arr);

        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetByteArrayData(IntPtr arr);

        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern void PushDataToByteArray(IntPtr arr, IntPtr data, int len);

        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern void RemoveRangeFromByteArray(IntPtr arr, int len);

        [DllImport(AudioPlayer, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ClearByteArray(IntPtr arr);

        public static LogCallbackDelegate logCallback = DefaultLogCallback;
        public static CaptureOpenCallbackDelegate captureOpenCallback = DefaultCaptureOpenCallback;
        public static InterruptCallbackDelegate interruptCallback = DefaultInterruptCallback;
        public static AudioOpenCallbackDelegate audioOpenCallback = DefaultAuidoOpenCallback;
        public static AudioPlayCallbackDelegate audioPlayCallback = DefaultAudioPlayCallback;

        private static void DefaultLogCallback(int level, IntPtr log)
        {
            SCMGR.LogCallback((LogLevel)level, Marshal.PtrToStringAnsi(log));
        }
        private static void DefaultCaptureOpenCallback(IntPtr user, int state, IntPtr error, IntPtr ctx)
        {
            GCHandle handle = GCHandle.FromIntPtr(user);
            IPlayer owner = (IPlayer)handle.Target;
            if (owner == null)
            {
                SCMGR.LogCallback(LogLevel.Error, "Owner ptr is invaild!");
                return;
            }
            OpenCallbackContext cbctx = null;
            if (state == (int)CaptureOpenResult.SUCCESS)
            {
                cbctx = new OpenCallbackContext();
                cbctx.duration = Marshal.ReadInt64(ctx);
                System.IntPtr _vs = Marshal.ReadIntPtr(ctx, 8);
                System.IntPtr _as = Marshal.ReadIntPtr(ctx, 16);
                System.IntPtr _ss = Marshal.ReadIntPtr(ctx, 24);
                if (_vs != System.IntPtr.Zero)
                    cbctx.videoParams = Marshal.PtrToStructure<VideoParams>(_vs);
                if (_as != System.IntPtr.Zero)
                    cbctx.audioParams = Marshal.PtrToStructure<AudioParams>(_as);
                if (_ss != System.IntPtr.Zero)
                    cbctx.subtitleParams = Marshal.PtrToStructure<SubtitleParams>(_ss);
                cbctx.realtime = Marshal.ReadInt32(ctx, 32) == 1;
                cbctx.localfile = Marshal.ReadInt32(ctx, 36) == 1;

            }
            owner.CaptureOpenCallback((CaptureOpenResult)state, Marshal.PtrToStringAnsi(error), cbctx);
        }

        private static void DefaultInterruptCallback(IntPtr user, IntPtr error)
        {
            GCHandle handle = GCHandle.FromIntPtr(user);
            IPlayer owner = (IPlayer)handle.Target;
            if (owner == null)
            {
                SCMGR.LogCallback(LogLevel.Error, "Owner ptr is invaild!");
                return;
            }
            owner.InterruptCallback(Marshal.PtrToStringAnsi(error));
        }

        private static void DefaultAuidoOpenCallback(IntPtr user, IntPtr playerParams, int code, IntPtr err)
        {
            GCHandle handle = GCHandle.FromIntPtr(user);
            IPlayer owner = (IPlayer)handle.Target;
            if (owner == null)
            {
                SCMGR.LogCallback(LogLevel.Error, "Owner ptr is invaild!");
                return;
            }
            PlayerParams pp = null;
            if (code == (int)AudioPlayerOpenResult.SUCCESS)
            {
                pp = new PlayerParams();
                System.IntPtr _srcap = Marshal.ReadIntPtr(playerParams, 0);
                System.IntPtr _dstap = Marshal.ReadIntPtr(playerParams, 8);
                if (_srcap != System.IntPtr.Zero)
                    pp.srcap = Marshal.PtrToStructure<AudioParams>(_srcap);
                if (_dstap != System.IntPtr.Zero)
                    pp.dstap = Marshal.PtrToStructure<AudioParams>(_dstap);
                pp.hwSize = Marshal.ReadInt32(playerParams, 16);

            }
            owner.AudioPlayerOpenCallback((AudioPlayerOpenResult)code, Marshal.PtrToStringAnsi(err), pp);
        }
        private static void DefaultAudioPlayCallback(IntPtr user, IntPtr buffer, int len)
        {
            GCHandle handle = GCHandle.FromIntPtr(user);
            SCPlayerPro owner = (SCPlayerPro)handle.Target;
            if (owner == null)
            {
                SCMGR.LogCallback(LogLevel.Error, "Owner ptr is invaild!");
                return;
            }
            owner.AudioPlayCallback(buffer, len);
        }

        public static void SCLog(LogLevel level, string log)
        {
            SCLog((int)level, StringToByteArray(log));
        }
        public static byte[] StringToByteArray(string str)
        {
            return System.Text.Encoding.UTF8.GetBytes(str).Concat(new byte[1] { 0 }).ToArray();
        }
    }


    public interface IPlayer
    {
        void CaptureOpenCallback(CaptureOpenResult state, string error, OpenCallbackContext ctx);

        void AudioPlayerOpenCallback(AudioPlayerOpenResult code, string error, PlayerParams param);

        void AudioPlayCallback(IntPtr buffer, int len);

        void InterruptCallback(string error);
    }

}