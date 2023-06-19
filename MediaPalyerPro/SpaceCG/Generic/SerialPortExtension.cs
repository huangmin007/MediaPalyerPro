using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceCG.Generic
{
    public static partial class InstanceExtensions
    {
        #region 扩展的配置动态调用函数
        public static void SendBytes(this SerialPort serialPort, byte[] buffer)
        {
            if (!serialPort.IsOpen) return;
            serialPort.Write(buffer, 0, buffer.Length);
        }

        public static void SendMessage(this SerialPort serialPort, string message)
        {
            SendBytes(serialPort, Encoding.UTF8.GetBytes(message));
        }
        #endregion

        /// <summary>
        /// 快捷创建 SerialPort 对象
        /// <para>配置的键值格式：(命名空间.Serial属性)，属性 PortName 不可为空</para>
        /// </summary>
        /// <param name="portNameCfgKey"></param>
        /// <param name="onSerialDataReceivedEventHandler"></param>
        /// <returns></returns>
        public static SerialPort CreateSerialPort(string portNameCfgKey, SerialDataReceivedEventHandler onSerialDataReceivedEventHandler)
        {
            if (String.IsNullOrWhiteSpace(ConfigurationManager.AppSettings[portNameCfgKey])) return null;

            String nameSpace = portNameCfgKey.Replace("PortName", "");
            SerialPort serialPort = new SerialPort();
            ChangeInstancePropertyValue(serialPort, nameSpace);

            serialPort.ErrorReceived += (s, e) => Logger.Warn($"串行端口 ({serialPort.PortName},{serialPort.BaudRate}) 发生了错误({e.EventType})");
            serialPort.PinChanged += (s, e) => Logger.Warn($"串行端口 ({serialPort.PortName},{serialPort.BaudRate}) 发生了非数据信号事件({e.EventType})");
            serialPort.DataReceived += (s, e) =>
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"串行端口 ({serialPort.PortName},{serialPort.BaudRate}) 接收了数据({e.EventType})，接收缓冲区中数据: {serialPort.BytesToRead}(Bytes)");
                }
            };
            if (onSerialDataReceivedEventHandler != null)
                serialPort.DataReceived += onSerialDataReceivedEventHandler;

            try
            {
                serialPort.Open();
                Logger.Info($"创建并打开新的串行端口 ({serialPort.PortName},{serialPort.BaudRate}) 完成");
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }

            return serialPort;
        }
        /// <summary>
        /// 关闭并释放 SerialPort 对象
        /// </summary>
        /// <param name="serialPort"></param>
        public static void DisposeSerialPort(ref SerialPort serialPort)
        {
            if (serialPort == null) return;
            RemoveInstanceEvents(serialPort);

            try
            {
                if (serialPort.IsOpen) serialPort.Close();
                serialPort.Dispose();
                Logger.Info($"关闭并释放串行端口 ({serialPort.PortName},{serialPort.BaudRate}) 完成");
            }
            catch (Exception ex)
            {
                Logger.Error($"关闭并释放串行端口资源 {serialPort} 对象错误：{ex}");
            }
            finally
            {
                serialPort = null;
            }
        }
    }
}

