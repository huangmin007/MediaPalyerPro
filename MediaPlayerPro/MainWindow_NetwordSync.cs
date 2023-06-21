using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using SpaceCG.Extensions;
using SpaceCG.Generic;
using SpaceCG.Net;
using Sttplay.MediaPlayer;

namespace MediaPlayerPro
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 多端同步，从机对象
        /// </summary>
        private IAsyncClient NetworkSlave;
        /// <summary>
        /// 多端同步，主机对象
        /// </summary>
        private IAsyncServer NetworkMaster;

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
        private WPFSCPlayerPro SyncPlayer = null;

        /// <summary>
        /// 创建网络同步对象
        /// </summary>
        private void CreateNetworkSyncObject()
        {
            if (!InstanceExtensions.GetInstanceFieldValue(this, ConfigurationManager.AppSettings["Synchronize.Player"], out object player)) return;

            if (player.GetType() == typeof(WPFSCPlayerPro))
            {
                SyncPlayer = (WPFSCPlayerPro)player;
            }

            if (SyncPlayer == null) return;

            //多端同步
            //NetworkSlave = InstanceExtensions.CreateNetworkClient("Synchronize.Slave", OnUdpSyncClientReceiveEventHandler);
            //if (NetworkSlave == null)
            //    NetworkMaster = InstanceExtensions.CreateNetworkServer("Synchronize.Master", OnUdpSyncServerReceiveEventHandler);

            if (NetworkMaster != null && ushort.TryParse(ConfigurationManager.AppSettings["Synchronize.Calibr"], out ushort calibr)) SyncCalibr = calibr;
            if (NetworkMaster != null && ushort.TryParse(ConfigurationManager.AppSettings["Synchronize.WaitCount"], out ushort waitCount)) SyncWaitCount = waitCount;
            if (NetworkSlave != null && SyncPlayer != null) SyncPlayer.Volume = 0.0f;
        }

        private void CheckNetworkSyncStatus()
        {
            if (SyncPlayer == null || NetworkMaster == null) return;

            List<IntPtr> clients = null;// NetworkMaster.GetAllConnectionIds();
            //Console.WriteLine($"clients: {clients.Count()}");
            if (clients.Count > 0)
            {
                byte[] ct = BitConverter.GetBytes((int)SyncPlayer.CurrentTime);  //4 Bytes  currentTime
                Array.Copy(ct, 0, SyncMessage, 0, ct.Length);

                byte[] sc = BitConverter.GetBytes(SyncCalibr);          //2 Bytes   校准误差时间ms
                Array.Copy(sc, 0, SyncMessage, 4, sc.Length);

                byte[] sw = BitConverter.GetBytes(SyncWaitCount);      //2 Bytes   校准后等待帧数
                Array.Copy(sc, 0, SyncMessage, 6, sw.Length);

                SyncMessage[8] = (byte)(SyncPlayer.IsPaused ? 0x00 : 0x01);    //1 Bytes 播放状态
                // ... 
                //Console.WriteLine(SyncMessage[8]);

                byte[] dt = BitConverter.GetBytes((int)SyncPlayer.Duration);        //4 Bytes  视频的持续时长
                Array.Copy(dt, 0, SyncMessage, SyncMessage.Length - dt.Length, dt.Length);

                //foreach (IntPtr client in clients)
                //    NetworkMaster.Send(client, SyncMessage, SyncMessage.Length);
            }
        }
#if false
        private HandleResult OnUdpSyncClientReceiveEventHandler(IClient sender, byte[] data)
        {
            if (SyncPlayer == null || NetworkMaster != null || data.Length != SyncMessage.Length) return HandleResult.Ok;

            int ct = BitConverter.ToInt32(data, 0);         //4 Bytes  currentTime
            ushort sc = BitConverter.ToUInt16(data, 4);     //2 Bytes   校准后等待帧数
            ushort sw = BitConverter.ToUInt16(data, 6);     //2 Bytes   校准后等待帧数
            byte status = data[8];                          //1 Bytes   播放状态
                                                            // ...
            int dt = BitConverter.ToInt32(data, data.Length - 4);   //4 Bytes  视频的持续时长

            if (status == 0x00)
            {
                if (!SyncPlayer.IsPaused) SyncPlayer.Pause();
            }
            if (status == 0x01)
            {
                if (SyncPlayer.IsPaused) SyncPlayer.Play();
            }
            else
            {
                // ...
            }

            int DT = (int)SyncPlayer.Duration;
            int CT = (int)SyncPlayer.CurrentTime;
            int Diff = (int)Math.Abs(CT - ct);

            if (SyncWaitCount != 0)
            {
                SyncWaitCount--;
                return HandleResult.Ok;
            }            
            if (Diff > sc && ct != 0 && CT != 0 && !SyncPlayer.IsPaused)
            {
                SyncWaitCount = sw;
                this.Dispatcher.Invoke(() => SyncPlayer.SeekFastMilliSecond(ct + 4));

                //响应信息
                SyncMessage[0] = 0x01;  //消息类型
                //SyncMessage[1] = (byte)(SyncPlayer.IsPaused ? 0x00 : 0x01);
                Array.Copy(BitConverter.GetBytes(Diff), 0, SyncMessage, 2, 4);      //4 Bytes 时间差值
                Array.Copy(BitConverter.GetBytes((int)CT), 0, SyncMessage, 6, 4);   //4 Bytes CurrentTime
                Array.Copy(BitConverter.GetBytes((int)DT), 0, SyncMessage, SyncMessage.Length - 4, 4);  //4 Bytes Duration
                NetworkSlave.Send(SyncMessage, SyncMessage.Length);

                NetworkSlave.GetRemoteHost(out string host, out ushort port);
                Log.Info($"远程主机地址 {host}:{port} ，校准时间时间差：{Diff} ms");
                Log.Info($"Current(Slave)Video: {CT}/{DT}    MasterVideo: {ct}/{dt}    时间差: {Diff}ms");
            }

            return HandleResult.Ok;
        }

        private HandleResult OnUdpSyncServerReceiveEventHandler(IServer sender, IntPtr connId, byte[] data)
        {
            if (SyncPlayer == null || NetworkSlave != null || data.Length != SyncMessage.Length) return HandleResult.Ok;

            NetworkMaster.GetRemoteAddress(connId, out String ip, out ushort port);
            //处理响应信息
            if (data[0] == 0x01)    //消息类型
            {
                int diff = BitConverter.ToInt32(data, 2);       //4 Bytes 时间差值
                int ct = BitConverter.ToInt32(data, 6);         //4 Bytes CurrentTime
                int dt = BitConverter.ToInt32(data, data.Length - 4);   //4 Bytes Duration
                Log.Info($"远程主机(Slave) {ip}:{port} 校准时间，时间差：{diff}ms");
                Log.Info($"Current(Master)Video: {SyncPlayer.CurrentTime}/{SyncPlayer.Duration}    SlaveVideo: {ct}/{dt}    时间差: {diff}ms");
            }

            return HandleResult.Ok;
        }
#endif
    }
}