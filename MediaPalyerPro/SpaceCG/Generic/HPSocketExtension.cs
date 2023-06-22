#define HPSocket

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceCG.Generic
{
#if HPSocket
    public static partial class InstanceExtensions
    {
        #region 扩展的配置动态调用函数
        /// <summary>
        /// 扩展的配置调用，HPSocket.IServer.Send
        /// </summary>
        /// <param name="server"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static bool Send(this HPSocket.IServer server, byte[] data)
        {
            if (data?.Length <= 0) return false;
            List<IntPtr> clients = server.GetAllConnectionIds();

            foreach (IntPtr connId in clients)
            {
                server.Send(connId, data, data.Length);
            }

            return true;
        }
        public static bool SendBytes(this HPSocket.Tcp.TcpServer server, byte[] data) => Send(server, data);
        public static bool SendBytes(this HPSocket.Udp.UdpServer server, byte[] data) => Send(server, data);
        public static bool SendMessage(this HPSocket.Tcp.TcpServer server, string message) => SendBytes(server, Encoding.UTF8.GetBytes(message));
        public static bool SendMessage(this HPSocket.Udp.UdpServer server, string message) => SendBytes(server, Encoding.UTF8.GetBytes(message));

        /// <summary>
        /// 扩展的配置调用，HPSocket.IClient Send
        /// </summary>
        /// <param name="server"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static bool Send(this HPSocket.IClient client, byte[] data)
        {
            if (data?.Length <= 0) return false;
            return client.Send(data, data.Length);
        }

        public static bool SendBytes(this HPSocket.Tcp.TcpClient client, byte[] data) => Send(client, data);
        public static bool SendBytes(this HPSocket.Udp.UdpClient client, byte[] data) => Send(client, data);
        public static bool SendMessage(this HPSocket.Tcp.TcpClient client, string message) => SendBytes(client, Encoding.UTF8.GetBytes(message));
        public static bool SendMessage(this HPSocket.Udp.UdpClient client, string message) => SendBytes(client, Encoding.UTF8.GetBytes(message));

        public static void WriteMultipleCoils(this HPSocket.Tcp.TcpClient client, byte slaveAddress, ushort startAddress, bool[] data) => WriteMultipleCoilsAsync(client, slaveAddress, startAddress, data);
        public static void WriteMultipleCoilsAsync(this HPSocket.Tcp.TcpClient client, byte slaveAddress, ushort startAddress, bool[] data)
        {
            if (client?.IsConnected == false || data?.Length == 0) return;
            ushort length = (ushort)data.Length;
            byte size = (byte)Math.Ceiling(data.Length / 8.0f);
            if(size > 1)
            {
                Logger.Warn($"暂时只支持 8 位数据");
            }
            byte value = 0x00;
            for(int i = 0; i < length;i ++)
            {
                value |= (data[i]) ? (byte)(1 << i) : (byte)(0 << i);
            }

            byte[] bytes = new byte[] {slaveAddress, 0x0F, (byte)(startAddress >> 8), (byte)(startAddress & 0xFF), (byte)(length >> 8), (byte)(length & 0xFF), size, value};
            byte[] crc = CRC16(bytes);
            byte[] buffer = new byte[bytes.Length + crc.Length];

            Array.Copy(bytes, 0, buffer, 0, bytes.Length);
            buffer[buffer.Length - 2] = crc[0];
            buffer[buffer.Length - 1] = crc[1];

            client.Send(buffer, buffer.Length);
        }
        /// <summary>
        /// https://blog.csdn.net/b07lxh/article/details/125015033
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private static byte[] CRC16(byte[] data)
        {
            int len = data.Length;
            if (len > 0)
            {
                ushort crc = 0xFFFF;

                for (int i = 0; i < len; i++)
                {
                    crc = (ushort)(crc ^ (data[i]));
                    for (int j = 0; j < 8; j++)
                    {
                        crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ 0xA001) : (ushort)(crc >> 1);
                    }
                }
                byte hi = (byte)((crc & 0xFF00) >> 8);  //高位置
                byte lo = (byte)(crc & 0x00FF);         //低位置

                return new byte[] { lo, hi };
            }
            return new byte[] { 0, 0 };
        }
        #endregion

        /// <summary>
        /// 快速创建并启动本地 Server 对象
        /// </summary>
        /// <typeparam name="TServer"></typeparam>
        /// <param name="localPort"></param>
        /// <param name="onServerReceiveEventHandler"></param>
        /// <returns></returns>
        public static TServer CreateNetworkServer<TServer>(string localAddress, ushort localPort, HPSocket.ServerReceiveEventHandler onServerReceiveEventHandler = null) where TServer : class, HPSocket.IServer, new()
        {
            if (localPort <= 0) return null;

            TServer server = new TServer();
            server.Port = localPort;
            server.Address = localAddress;
            try
            {
                server.Start();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return default;
            }

            Logger.Info($"已启本地 ({server.GetType()}) 服务端口：{localPort}");
            String connType = server is HPSocket.Tcp.TcpServer ? "TCP" : "UDP";

            server.OnClose += (HPSocket.IServer sender, IntPtr connId, HPSocket.SocketOperation socketOperation, int errorCode) =>
            {
                sender.GetRemoteAddress(connId, out string ip, out ushort port);
                Logger.Warn($"客户端连接({connType},{connId}) {ip}:{port} 已关闭  Operation: {socketOperation}  Code: {errorCode}");
                return HPSocket.HandleResult.Ok;
            };
            server.OnAccept += (HPSocket.IServer sender, IntPtr connId, IntPtr client) =>
            {
                sender.GetRemoteAddress(connId, out string ip, out ushort port);
                Logger.Info($"已接受客户端({connType},{connId}) {ip}:{port} 连接");
                return HPSocket.HandleResult.Ok;
            };
            server.OnReceive += (HPSocket.IServer sender, IntPtr connId, byte[] data) =>
            {
                if (Logger.IsDebugEnabled)
                {
                    sender.GetRemoteAddress(connId, out string ip, out ushort port);
                    Logger.Debug($"接收客户端({connType},{connId}) {ip}:{port} 数据，长度：{data.Length}(Bytes)");
                }
                return HPSocket.HandleResult.Ok;
            };
            server.OnShutdown += (HPSocket.IServer sender) =>
            {
                RemoveInstanceEvents(server);
                return HPSocket.HandleResult.Ok;
            };

            if (onServerReceiveEventHandler != null)
                server.OnReceive += onServerReceiveEventHandler;

            return server;
        }
        /// <summary>
        /// 快速创建并启动本地 Server 对象
        /// <para>配置的键值格式：[ip,]port 或 [ip:]port</para>
        /// </summary>
        /// <typeparam name="TServer"></typeparam>
        /// <param name="cfgKey"></param>
        /// <param name="onServerReceiveEventHandler"></param>
        /// <returns></returns>
        public static TServer CreateNetworkServer<TServer>(string cfgKey, HPSocket.ServerReceiveEventHandler onServerReceiveEventHandler = null) where TServer : class, HPSocket.IServer, new()
        {
            if (String.IsNullOrWhiteSpace(ConfigurationManager.AppSettings[cfgKey])) return default;

            String configInfo = ConfigurationManager.AppSettings[cfgKey];
            if (String.IsNullOrWhiteSpace(configInfo)) return default;

            String[] info = configInfo.IndexOf(':') != -1 ?
                configInfo.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries) :
                configInfo.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (info.Length <= 0 || info.Length > 2) return default;

            if (info.Length == 1)
            {
                if (!ushort.TryParse(info[0], out ushort localPort)) return default;
                return CreateNetworkServer<TServer>("0.0.0.0", localPort, onServerReceiveEventHandler);
            }
            else if (info.Length == 2)
            {
                if (!ushort.TryParse(info[1], out ushort localPort)) return default;
                return CreateNetworkServer<TServer>(info[0], localPort, onServerReceiveEventHandler);
            }

            return default;
        }
        /// <summary>
        /// 快速创建并启动本地 Server 对象
        /// <para>配置的键值格式：type,ip,port 示例：TCP,0.0.0.0,5330</para>
        /// </summary>
        /// <param name="cfgKey"></param>
        /// <param name="onServerReceiveEventHandler"></param>
        /// <returns></returns>
        public static HPSocket.IServer CreateNetworkServer(string cfgKey, HPSocket.ServerReceiveEventHandler onServerReceiveEventHandler = null)
        {
            if (String.IsNullOrWhiteSpace(ConfigurationManager.AppSettings[cfgKey])) return default;

            String configInfo = ConfigurationManager.AppSettings[cfgKey];
            if (String.IsNullOrWhiteSpace(configInfo)) return default;

            String[] info = configInfo.IndexOf(':') != -1 ?
                configInfo.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries) :
                configInfo.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (info.Length != 3)
            {
                Logger.Error($"服务端配置错误：{cfgKey}:{configInfo} ");
                return default;
            }

            if (!ushort.TryParse(info[2], out ushort localPort)) return default;

            if (info[0].ToUpper().IndexOf("TCP") != -1)
            {
                return CreateNetworkServer<HPSocket.Tcp.TcpServer>("0.0.0.0", localPort, onServerReceiveEventHandler);
            }
            else if (info[0].ToUpper().IndexOf("UDP") != -1)
            {
                return CreateNetworkServer<HPSocket.Udp.UdpServer>("0.0.0.0", localPort, onServerReceiveEventHandler);
            }

            return default;
        }
        /// <summary>
        /// 快速停止并清理 Server 对象
        /// </summary>
        /// <param name="TServer"></param>
        public static void DisposeNetworkServer(ref HPSocket.IServer TServer)
        {
            if (TServer == null) return;
            RemoveInstanceEvents(TServer);

            try
            {
                List<IntPtr> clients = TServer.GetAllConnectionIds();
                foreach (IntPtr client in clients)
                {
                    TServer.Disconnect(client, true);
                }

                Type type = TServer.GetType();
                TServer.GetListenAddress(out string ip, out ushort port);
                Logger.Info($"服务端({type}) {ip}:{port} 已断开所有连接并销毁释放资源");
            }
            catch(Exception ex)
            {
                Logger.Error(ex);
            }
            finally
            {
                TServer.Dispose();
                TServer = null;
            }
        }


        /// <summary>
        /// 快速创建并连接远程服务端的 Client 对象
        /// </summary>
        /// <typeparam name="TClient"></typeparam>
        /// <param name="remoteAddress"></param>
        /// <param name="remotePort"></param>
        /// <param name="onClientReceiveEventHandler"></param>
        /// <returns></returns>
        public static TClient CreateNetworkClient<TClient>(string remoteAddress, ushort remotePort, HPSocket.ClientReceiveEventHandler onClientReceiveEventHandler = null) where TClient : class, HPSocket.IClient, new()
        {
            if (remotePort <= 0) return null;

            TClient client = new TClient();
            client.Async = true;
            client.Port = remotePort;
            client.Address = remoteAddress;
            client.FreeBufferPoolSize = 32;
            client.FreeBufferPoolHold = client.FreeBufferPoolSize * 3;

            String connType = client is HPSocket.Tcp.TcpClient ? "TCP" : "UDP";

            client.OnClose += (HPSocket.IClient sender, HPSocket.SocketOperation enOperation, int errorCode) =>
            {
                client.GetRemoteHost(out string rHost, out ushort rPort);
                client.GetListenAddress(out string lHost, out ushort lPort);
                Logger.Warn($"客户端({connType}) {lHost}:{lPort} 已断开与远程服务 {rHost}:{rPort} 的连接，等待重新连接 ... ");

                Task.Run(() =>
                {
                    System.Threading.Thread.Sleep(2000);
                    try
                    {
                        client?.Connect();
                    }
                    catch (Exception) { }
                });
                return HPSocket.HandleResult.Ok;
            };
            client.OnConnect += (HPSocket.IClient sender) =>
            {
                client.GetRemoteHost(out string rHost, out ushort rPort);
                client.GetListenAddress(out string lHost, out ushort lPort);
                Logger.Info($"客户端({connType}) {lHost}:{lPort} 已连接远程服务 {rHost}:{rPort} ");
                
                return HPSocket.HandleResult.Ok;
            };
            client.OnReceive += (HPSocket.IClient sender, byte[] data) =>
            {
                if (Logger.IsDebugEnabled)
                {
                    client.GetRemoteHost(out string rHost, out ushort rPort);
                    client.GetListenAddress(out string lHost, out ushort lPort);

                    Logger.Debug($"客户端({connType}) {lHost}:{lPort} 收到远程服务 {rHost}:{rPort} 的数据，长度：{data.Length}(Bytes)");
                }

                return HPSocket.HandleResult.Ok;
            };

            if (onClientReceiveEventHandler != null) client.OnReceive += onClientReceiveEventHandler;

            try
            {
                client.Connect();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return default;
            }

            return client;
        }
        /// <summary>
        /// 快速创建并连接远程服务端的 Client 对象
        /// <para>配置的键值格式：ip,port 或 ip:port </para>
        /// </summary>
        /// <typeparam name="TClient"></typeparam>
        /// <param name="cfgKey">format(remote address : remote port)，示例：127.0.0.1:5331</param>
        /// <param name="onClientReceiveEventHandler"></param>
        /// <returns></returns>
        public static TClient CreateNetworkClient<TClient>(string cfgKey, HPSocket.ClientReceiveEventHandler onClientReceiveEventHandler = null) where TClient : class, HPSocket.IClient, new()
        {
            if (String.IsNullOrWhiteSpace(ConfigurationManager.AppSettings[cfgKey])) return default;

            String configInfo = ConfigurationManager.AppSettings[cfgKey];
            if (String.IsNullOrWhiteSpace(configInfo)) return default;

            String[] info = configInfo.IndexOf(':') != -1 ?
                configInfo.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries) :
                configInfo.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (info.Length <= 0 || info.Length > 2) return default;

            if (info.Length == 1)
            {
                if (!ushort.TryParse(info[0], out ushort removePort)) return default;
                return CreateNetworkClient<TClient>("127.0.0.1", removePort, onClientReceiveEventHandler);
            }
            else if (info.Length == 2)
            {
                if (!ushort.TryParse(info[1], out ushort removePort)) return default;
                return CreateNetworkClient<TClient>(info[0], removePort, onClientReceiveEventHandler);
            }

            return default;
        }
        /// <summary>
        /// 快速创建并连接远程服务端的 Client 对象
        /// <para>配置的键值格式：type,ip,port 或 type:ip:port </para>
        /// </summary>
        /// <param name="cfgKey"></param>
        /// <param name="onClientReceiveEventHandler"></param>
        /// <returns></returns>
        public static HPSocket.IClient CreateNetworkClient(string cfgKey, HPSocket.ClientReceiveEventHandler onClientReceiveEventHandler = null)
        {
            if (String.IsNullOrWhiteSpace(ConfigurationManager.AppSettings[cfgKey])) return default;

            String configInfo = ConfigurationManager.AppSettings[cfgKey];
            if (String.IsNullOrWhiteSpace(configInfo)) return default;

            String[] info = configInfo.IndexOf(':') != -1 ?
                configInfo.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries) :
                configInfo.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (info.Length != 3)
            {
                Logger.Error($"客户端配置错误：{cfgKey}:{configInfo} ");
                return default;
            }

            if (!ushort.TryParse(info[2], out ushort removePort)) return default;

            if (info[0].ToUpper().IndexOf("TCP") != -1)
            {
                return CreateNetworkClient<HPSocket.Tcp.TcpClient>(info[1], removePort, onClientReceiveEventHandler);
            }
            else if (info[0].ToUpper().IndexOf("UDP") != -1)
            {
                return CreateNetworkClient<HPSocket.Udp.UdpClient>(info[1], removePort, onClientReceiveEventHandler);
            }

            return default;
        }
        /// <summary>
        /// 快速关闭并清理 Client 对象
        /// </summary>
        /// <param name="TClient"></param>
        public static void DisposeNetworkClient(ref HPSocket.IClient TClient)
        {
            if (TClient == null) return;
            RemoveInstanceEvents(TClient);

            Type type = TClient.GetType();
            TClient.GetListenAddress(out string lHost, out ushort lPort);
            Logger.Info($"客户端({type}) {lHost}:{lPort} 断开连接并销毁释放资源");

            TClient.Dispose();
            TClient = null;
        }
    }
#endif
}
