//#define NModbus

using System;
using System.Configuration;
using System.IO.Ports;
using System.Net.Sockets;

namespace SpaceCG.Generic
{

    public static partial class InstanceExtensions
    {
#if NModbus
        /// <summary>
        /// 创建 NModbus 主机对象
        /// <para>配置的键值格式：Type,RemoteHostORcomm,RemotePortORbaudRate</para>
        /// <para>Type支持：TCP/TCP-RTU/UDP/UDP-RTU/SERIAL </para>
        /// </summary>
        /// <param name="cfgKey">Config Key Format:(type,hostORcom,portORbaudRate)</param>
        /// <returns></returns>
        public static NModbus.IModbusMaster CreateNModbusMaster(string cfgKey)
        {
            if (String.IsNullOrWhiteSpace(ConfigurationManager.AppSettings[cfgKey])) return null;

            String[] args = ConfigurationManager.AppSettings[cfgKey].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (args.Length != 3) throw new ArgumentException("参数格式不正确");

            if (!int.TryParse(args[2], out int portOrbaudRate)) return null;

            return CreateNModbusMaster(args[0], args[1], portOrbaudRate);
        }
        /// <summary>
        /// 创建 NModbus 主机对象
        /// </summary>
        /// <param name="type"></param>
        /// <param name="hostORcom"></param>
        /// <param name="portORbaudRate"></param>
        /// <returns></returns>
        public static NModbus.IModbusMaster CreateNModbusMaster(string type, string hostORcom, int portORbaudRate)
        {
            if (portORbaudRate <= 0) return null;
            if (String.IsNullOrWhiteSpace(type) || String.IsNullOrWhiteSpace(hostORcom))
                throw new ArgumentNullException("参数不能为空");

            type = type.ToUpper().Trim();
            NModbus.IModbusMaster master = null;
            NModbus.IModbusFactory factory = new NModbus.ModbusFactory();

            try
            {
                if (type.IndexOf("TCP") >= 0)
                {
                    TcpClient tcpClient = new TcpClient();
                    tcpClient.Connect(hostORcom, portORbaudRate);

                    if (type.IndexOf("RTU") == -1)
                        master = factory.CreateMaster(tcpClient);
                    else
                        master = factory.CreateMaster(factory.CreateRtuTransport(new NModbus.IO.TcpClientAdapter(tcpClient)));
                }
                else if (type.IndexOf("UDP") >= 0)
                {
                    UdpClient udpClient = new UdpClient();
                    udpClient.Connect(hostORcom, portORbaudRate);

                    if (type.IndexOf("RTU") == -1)
                        master = factory.CreateMaster(udpClient);
                    else
                        master = factory.CreateMaster(factory.CreateRtuTransport(new NModbus.IO.UdpClientAdapter(udpClient)));
                }
                else if (type.IndexOf("SERIAL") >= 0)
                {
                    SerialPort serialPort = new SerialPort(hostORcom, portORbaudRate);
                    serialPort.Open();
                    master = NModbus.Serial.ModbusFactoryExtensions.CreateRtuMaster(factory, serialPort);
                }
                else
                {
                    throw new ArgumentException($"不支持创建的类型：({type},{hostORcom},{portORbaudRate}) ", nameof(type));
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"创建 ModbusMaster 对象 ({type},{hostORcom},{portORbaudRate}) 错误：{ex}");
                return null;
            }

            Logger.Info($"创建 ModbusMaster 对象 ({type},{hostORcom},{portORbaudRate}) 完成");

            //Dispose.
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                Logger.Info($"exit modbus ...");
                DisposeNModbusMaster(ref master);
            };

            return master;
        }
        /// <summary>
        /// 关闭并清理 NModbusMaster 对象
        /// </summary>
        /// <param name="master"></param>
        public static void DisposeNModbusMaster(ref NModbus.IModbusMaster master)
        {
            if (master == null) return;

            try
            {
                Logger.DebugFormat("Dispose ModbusMaster: {0} {1}", master.Transport != null ? master.Transport.ToString() : "", master.ToString());
                master.Transport?.Dispose();
                master.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Error($"ModbusMaster Dispose Exception: {ex}");
            }
            finally
            {
                master = null;
            }
        }
#endif
    }
}
