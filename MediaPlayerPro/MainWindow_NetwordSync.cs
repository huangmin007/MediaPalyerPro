using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using SpaceCG.Extensions;
using SpaceCG.Net;
using Sttplay.MediaPlayer;

namespace MediaPlayerPro
{
    public enum PlayState:byte
    {
        PAUSE,
        PLAYING,
        STOP,
    }

    /// <summary>
    /// 同步数据信息
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct SyncPackData
    {
        /// <summary>
        /// 消息标志符
        /// </summary>
        public byte MessageFlags;
        /// <summary>
        /// 播放状态
        /// </summary>
        public PlayState PlayState;
        /// <summary>
        /// 视频当前时间
        /// </summary>
        public int CurrentTime;
        /// <summary>
        /// 视频持续时间
        /// </summary>
        public int DurationTime;
        /// <summary>
        /// 时间差值
        /// </summary>
        public int Difference;
        /// <summary>
        /// 参数，多端同步，可接受的时间误差(ms)
        /// </summary>
        public ushort SyncCalibr;
        /// <summary>
        /// 参数，多端同步后，等待的帧间隔
        /// </summary>
        public ushort SyncWaitFrame;
        
        //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        //public byte[] Reserve;
    }

    public partial class MainWindow : Window
    {
        /// <summary>
        /// 指定的多端同步播放器
        /// </summary>
        private WPFSCPlayerPro SyncPlayer = null;
        /// <summary>
        /// 多端同步，从机对象
        /// </summary>
        private IAsyncClient NetworkSlave = null;
        /// <summary>
        /// 多端同步，主机对象
        /// </summary>
        private IAsyncServer NetworkMaster = null;

        /// <summary>
        /// 多端同步校准误差时间(ms)
        /// </summary>
        private ushort SyncCalibr = 120;
        /// <summary>
        /// 多端同步后，等待的帧间隔
        /// </summary>
        private ushort SyncWaitFrame = 120;

        private int SyncPackSize;
        private string[] SlaveConnectArgs;
        private readonly byte[] HeartbeatPack = new byte[] { 0x00, 0x00, 0x00, 0x00 };

        /// <summary>
        /// 创建网络同步对象
        /// </summary>
        private void CreateNetworkSyncObject(XElement syncElement)
        {
            if (syncElement == null) return;

            SyncPlayer = null;
            if (NetworkSlave != null)
            {
                NetworkSlave.Dispose();
                NetworkSlave = null;
            }
            if (NetworkMaster != null)
            {
                NetworkMaster.Dispose();
                NetworkMaster = null;
            }

            //多端同步 播放器对象
            if (!String.IsNullOrWhiteSpace(syncElement.Attribute("Player")?.Value))
            {
                if (!InstanceExtensions.GetInstanceFieldValue(this, syncElement.Attribute("Player").Value, out object player)) return;
                if (player.GetType() == typeof(WPFSCPlayerPro))
                {
                    SyncPlayer = (WPFSCPlayerPro)player;
                }
            }
            if (SyncPlayer == null) return;

            //多端同步 连接对象
            if (!String.IsNullOrWhiteSpace(syncElement.Attribute("Slave")?.Value))
            {
                SlaveConnectArgs = syncElement.Attribute("Slave").Value.Split(',');
                if (SlaveConnectArgs.Length >= 2 && ushort.TryParse(SlaveConnectArgs[1], out ushort port) && port > 1024)
                {
                    NetworkSlave = new AsyncUdpClient();
                    NetworkSlave.Connected += NetworkConnection_Connected;
                    NetworkSlave.DataReceived += NetworkSlave_DataReceived;
                    NetworkSlave.Disconnected += NetworkConnection_Disconnected;
                    NetworkSlave.Connect(SlaveConnectArgs[0], port);
                }
            }
            if (NetworkSlave == null && !String.IsNullOrWhiteSpace(syncElement.Attribute("Master")?.Value))
            {
                if (ushort.TryParse(syncElement.Attribute("Master").Value, out ushort listenPort) && listenPort > 1024)
                {
                    NetworkMaster = new AsyncUdpServer(listenPort);
                    NetworkMaster.ClientConnected += NetworkConnection_Connected;
                    NetworkMaster.ClientDataReceived += NetworkMaster_ClientDataReceived;
                    NetworkMaster.ClientDisconnected += NetworkConnection_Disconnected;
                }
            }

            SyncPackSize = Marshal.SizeOf(typeof(SyncPackData));
            if (NetworkSlave != null && SyncPlayer != null) SyncPlayer.Volume = 0.0f;
            if (ushort.TryParse(syncElement.Attribute("Calibr")?.Value, out ushort calibr)) SyncCalibr = calibr;
            if (ushort.TryParse(syncElement.Attribute("WaitFrame")?.Value, out ushort waitFrame)) SyncWaitFrame = waitFrame;
        }

        private void NetworkConnection_Connected(object sender, AsyncEventArgs e)
        {
            Log.Info($"{sender}: {e.EndPoint} Connected.");
        }
        private void NetworkConnection_Disconnected(object sender, AsyncEventArgs e)
        {
            Log.Info($"{sender}: {e.EndPoint} Disconnected.");

            if (sender.GetType() == typeof(AsyncTcpClient))
            {
                Task.Run(() =>
                {
                    Thread.Sleep(2000);
                    NetworkSlave?.Connect(SlaveConnectArgs[0], ushort.Parse(SlaveConnectArgs[1]));
                });
            }
        }
        private void NetworkMaster_ClientDataReceived(object sender, AsyncDataEventArgs e)
        {
            if (SyncPlayer == null || NetworkMaster != null || e.Bytes.Length != SyncPackSize) return;

            //处理响应信息
            if (e.Bytes[0] == 0x01)    //消息类型
            {
                SyncPackData slavePack = BytesToStruct(e.Bytes, SyncPackSize);
                Log.Info($"远程主机(Slave) {e.EndPoint} 校准时间，时间差：{slavePack.Difference} ms");
                Log.Info($"Current(Master)Video: {SyncPlayer.CurrentTime}/{SyncPlayer.Duration}    SlaveVideo: {slavePack.CurrentTime}/{slavePack.DurationTime}    时间差: {slavePack.Difference}ms");
            }
        }
        private void NetworkSlave_DataReceived(object sender, AsyncDataEventArgs e)
        {
            if (SyncPlayer == null || NetworkMaster != null || e.Bytes.Length != SyncPackSize) return;

            SyncPackData masterPack = BytesToStruct(e.Bytes, SyncPackSize);
            if (masterPack.PlayState == PlayState.PAUSE)
            {
                if (!SyncPlayer.IsPaused) SyncPlayer.Pause();
            }
            else if (masterPack.PlayState == PlayState.PLAYING)
            {
                if (SyncPlayer.IsPaused) SyncPlayer.Play();
            }
            else
            {
                // ...
            }

            if (SyncWaitFrame != 0)
            {
                SyncWaitFrame--;
                return;
            }

            if (masterPack.PlayState != PlayState.PLAYING) return;
            if (masterPack.CurrentTime == 0 || SyncPlayer.CurrentTime == 0 || SyncPlayer.Duration == 0) return;

            int diff = (int)Math.Abs(SyncPlayer.CurrentTime - masterPack.CurrentTime);
            if (diff > masterPack.SyncCalibr)
            {
                SyncWaitFrame = masterPack.SyncWaitFrame;
                this.Dispatcher.Invoke(() => SyncPlayer.SeekFastMilliSecond(masterPack.CurrentTime + 4));

                Log.Info($"远程主机地址 {e.EndPoint} ，校准时间时间差：{diff} ms");
                Log.Info($"Current(Slave)Video: {SyncPlayer.CurrentTime}/{SyncPlayer.Duration}    MasterVideo: {masterPack.CurrentTime}/{masterPack.DurationTime}    时间差: {diff}ms");

                //响应信息
                masterPack.MessageFlags = 0x01;   //消息类型
                masterPack.Difference = diff;
                masterPack.CurrentTime = (int)SyncPlayer.CurrentTime;
                masterPack.DurationTime = (int)SyncPlayer.Duration;

                byte[] data = StructToBytes(masterPack, SyncPackSize);
                NetworkSlave.SendBytes(data);
            }
        }

        /// <summary>
        /// 检查网络同步状态
        /// </summary>
        protected void CheckNetworkSyncStatus()
        {
            if (SyncPlayer == null) return;

            if(NetworkSlave != null)
            {
                NetworkSlave.SendBytes(HeartbeatPack);
                return;
            }

            if (NetworkMaster != null && NetworkMaster.ClientCount > 0)
            {
                SyncPackData masterPack = new SyncPackData();
                masterPack.MessageFlags = 0x00;
                masterPack.Difference = 0;
                masterPack.CurrentTime = (int)SyncPlayer.CurrentTime;
                masterPack.DurationTime = (int)SyncPlayer.Duration;
                masterPack.SyncCalibr = SyncCalibr;
                masterPack.SyncWaitFrame = SyncWaitFrame;
                masterPack.PlayState = SyncPlayer.IsPaused ? PlayState.PAUSE : PlayState.PLAYING;

                byte[] data = StructToBytes(masterPack, SyncPackSize);
                foreach (var client in NetworkMaster.Clients) NetworkMaster.SendBytes(data, client);
            }
        }


        /// <summary>
        /// 结构体转化成byte[]
        /// </summary>
        /// <param name="structure"></param>
        /// <returns></returns>
        public static Byte[] StructToBytes(SyncPackData structure, int size)
        {
            //Int32 size = Marshal.SizeOf(structure);
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr<SyncPackData>(structure, buffer, false);
                Byte[] bytes = new Byte[size];
                Marshal.Copy(buffer, bytes, 0, size);

                return bytes;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        /// <summary>
        /// byte[]转化成结构体
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static SyncPackData BytesToStruct(Byte[] bytes, int size)
        {
            //Int32 size = Marshal.SizeOf(typeof(T));
            IntPtr buffer = Marshal.AllocHGlobal(size);

            try
            {
                Marshal.Copy(bytes, 0, buffer, size);
                return Marshal.PtrToStructure<SyncPackData>(buffer);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

}