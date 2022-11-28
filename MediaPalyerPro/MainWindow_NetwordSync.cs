using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using HPSocket;
using SpaceCG.Generic;
using Sttplay.MediaPlayer;

namespace MediaPalyerPro
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 多端同步，从机对象
        /// </summary>
        private HPSocket.IClient UDPClientSync;
        /// <summary>
        /// 多端同步，主机对象
        /// </summary>
        private HPSocket.IServer UDPServerSync;

        /// <summary>
        /// 多端同步校准误差时间(ms)
        /// </summary>
        private ushort SyncCalibr = 120;
        /// <summary>
        /// 多端同步后，等待的帧间隔
        /// </summary>
        private ushort SyncWaitCount = 120;
        /// <summary>
        /// 多端同步消息
        /// </summary>
        private byte[] SyncMessage = new byte[16];
        /// <summary>
        /// 指定的多端同步播放器
        /// </summary>
        private WPFSCPlayerPro SyncPlayer;

        /// <summary>
        /// 创建网络同步对象
        /// </summary>
        private void CreateNetworkSyncObject()
        {
            if(String.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["Synchronize.Player"]))
            {

            }

            //多端同步
            UDPClientSync = InstanceExtension.CreateNetworkClient("Synchronize.Slave", OnUdpSyncClientReceiveEventHandler);
            if (UDPClientSync == null)
                UDPServerSync = InstanceExtension.CreateNetworkServer("Synchronize.Master", OnUdpSyncServerReceiveEventHandler);

            if (UDPServerSync != null && ushort.TryParse(ConfigurationManager.AppSettings["Synchronize.Calibr"], out ushort calibr)) SyncCalibr = calibr;
            if (UDPServerSync != null && ushort.TryParse(ConfigurationManager.AppSettings["Synchronize.WaitCount"], out ushort waitCount)) SyncWaitCount = waitCount;
            if (UDPClientSync != null && SyncPlayer != null) SyncPlayer.Volume = 0.0f;
        }

        private void CheckNetworkSyncStatus()
        {
            if (SyncPlayer == null || UDPServerSync == null) return;

            List<IntPtr> clients = UDPServerSync.GetAllConnectionIds();
            if (clients.Count > 0)
            {
                byte[] ct = BitConverter.GetBytes((int)SyncPlayer.CurrentTime);  //4 Bytes  currentTime
                Array.Copy(ct, 0, SyncMessage, 0, ct.Length);

                byte[] sc = BitConverter.GetBytes(SyncCalibr);          //2 Bytes   校准误差时间ms
                Array.Copy(sc, 0, SyncMessage, 4, sc.Length);

                byte[] sw = BitConverter.GetBytes(SyncWaitCount);      //2 Bytes   校准后等待帧数
                Array.Copy(sc, 0, SyncMessage, 6, sw.Length);

                // ... 

                byte[] dt = BitConverter.GetBytes((int)SyncPlayer.Duration);        //4 Bytes  视频的持续时长
                Array.Copy(dt, 0, SyncMessage, SyncMessage.Length - dt.Length, dt.Length);

                foreach (IntPtr client in clients)
                    UDPServerSync.Send(client, SyncMessage, SyncMessage.Length);
            }
        }

        private HandleResult OnUdpSyncClientReceiveEventHandler(IClient sender, byte[] data)
        {
            if (SyncPlayer == null || UDPServerSync != null || data.Length != SyncMessage.Length) return HandleResult.Ok;

            if (SyncWaitCount != 0)
            {
                SyncWaitCount--;
                return HandleResult.Ok;
            }

            int ct = BitConverter.ToInt32(data, 0);
            ushort sc = BitConverter.ToUInt16(data, 4);
            ushort sw = BitConverter.ToUInt16(data, 6);
            int dt = BitConverter.ToInt32(data, data.Length - 4);

            int DT = (int)SyncPlayer.Duration;
            int CT = (int)SyncPlayer.CurrentTime;
            int Diff = (int)Math.Abs(CT - ct);

            if (Diff > sc && ct != 0 && CT != 0)
            {
                SyncWaitCount = sw;
                this.Dispatcher.Invoke(() => SyncPlayer.SeekFastMilliSecond(ct + 4));

                //响应信息
                SyncMessage[0] = 0x01;
                Array.Copy(BitConverter.GetBytes(Diff), 0, SyncMessage, 2, 4);
                Array.Copy(BitConverter.GetBytes((int)CT), 0, SyncMessage, 6, 4);
                Array.Copy(BitConverter.GetBytes((int)DT), 0, SyncMessage, SyncMessage.Length - 4, 4);
                UDPClientSync.Send(SyncMessage, SyncMessage.Length);

                UDPClientSync.GetRemoteHost(out string host, out ushort port);
                Log.Info($"远程主机地址 {host}:{port} ，校准时间时间差：{Diff}");
                Log.Info($"Current(Slave)Video: {CT}/{DT}    MasterVideo: {ct}/{dt}    Diff: {Diff}");
            }

            return HandleResult.Ok;
        }

        private HandleResult OnUdpSyncServerReceiveEventHandler(IServer sender, IntPtr connId, byte[] data)
        {
            if (SyncPlayer == null || UDPClientSync != null || data.Length != SyncMessage.Length) return HandleResult.Ok;

            UDPServerSync.GetRemoteAddress(connId, out String ip, out ushort port);
            //处理响应信息
            if (data[0] == 0x01)
            {
                int diff = BitConverter.ToInt32(data, 2);
                int ct = BitConverter.ToInt32(data, 6);
                int dt = BitConverter.ToInt32(data, data.Length - 4);
                Log.Info($"远程主机(Slave) {ip}:{port} 校准时间，时间差：{diff}");
                Log.Info($"Current(Master)Video: {SyncPlayer.CurrentTime}/{SyncPlayer.Duration}    SlaveVideo: {ct}/{dt}    Diff: {diff}");
            }

            return HandleResult.Ok;
        }
        
    }
}
