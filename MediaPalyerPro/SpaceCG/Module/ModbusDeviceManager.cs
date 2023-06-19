using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using SpaceCG.Generic;

namespace SpaceCG.Module
{
    public abstract class ModbusDevice
    {
        /// <summary>
        /// 设备地址
        /// </summary>
        public byte Address { get; protected set; } = 0x00;

        /// <summary>
        /// 设备输入状态存储器
        /// </summary>
        public Dictionary<ushort, ushort> Inputs { get; protected set; } = new Dictionary<ushort, ushort>();

        /// <summary>
        /// 设备输出状态存储器
        /// </summary>
        public Dictionary<ushort, ushort> Outputs { get; protected set; } = new Dictionary<ushort, ushort>();
    }

    /// <summary>
    /// Modbus IO 设备
    /// </summary>
    public class ModbusIODevice
    {
        /// <summary>
        /// 设备地址
        /// </summary>
        public byte Address { get; private set; } = 0x00;

        /// <summary>
        /// 数字输入信号状态集合
        /// </summary>
        public bool[] DigitalOutputStatus { get; internal set; }
        /// <summary>
        /// 数字输出信号状态集合
        /// </summary>
        public bool[] DigitalInputStatus { get; internal set; }

        /// <summary>
        /// 模拟信号输入状态集合
        /// </summary>
        public ushort[] AnalogInputStatus { get; internal set; }
        /// <summary>
        /// 模拟信号输出状态集合
        /// </summary>
        public ushort[] AnalogOutputStatus { get; internal set; }

        /// <summary>
        /// 开启输入实时读取
        /// </summary>
        public bool EnabledInputRead = true;

        /// <summary>
        /// 开启输出实时读取
        /// </summary>
        public bool EnabledOutputRead = false;

        /// <summary>
        /// IO 设备对象
        /// </summary>
        /// <param name="address"></param>
        /// <param name="DI"></param>
        /// <param name="DO"></param>
        /// <param name="AI"></param>
        /// <param name="AO"></param>
        public ModbusIODevice(byte address = 0x01, byte DI = 8, byte DO = 8, byte AI = 0, byte AO = 0)
        {
            this.Address = address;

            this.DigitalInputStatus = new bool[DI];
            this.DigitalOutputStatus = new bool[DO];

            this.AnalogInputStatus = new ushort[AI];
            this.AnalogOutputStatus = new ushort[AO];
        }

        public static ModbusIODevice CreateDevice(byte address = 0x01, byte DI = 8, byte DO = 8, byte AI = 0, byte AO = 0)
        {
            return new ModbusIODevice(address, DI, DO, AI, AO);
        }
    }

    public class ModbusMethodInfo
    {
        public string MethodName;
        public byte FuncCode;
        public byte SlaveAddress;
        public ushort StartAddress;
        public object Value;
    }

    /// <summary>
    /// Modbus 传输设备
    /// </summary>
    public class ModbusTransportDevice : IDisposable
    {
        protected static readonly log4net.ILog Log = log4net.LogManager.GetLogger("ModbusTransportDevice");

        public String Key { get; private set; }

        public event Action<ModbusTransportDevice, byte, ushort, ushort> InputChangeEvent;
        public event Action<ModbusTransportDevice, byte, ushort, ushort> OutputChangeEvent;

        private bool IsRunning = false;
        internal Modbus.Device.IModbusMaster master;

        protected List<ModbusIODevice> Devices { get; private set; } = new List<ModbusIODevice>(8);

        public int ReadTimeout
        {
            get
            {
                return master.Transport.ReadTimeout;
            }
            set
            {
                master.Transport.ReadTimeout = value;
            }
        }
        public int WriteTimeout
        {
            get
            {
                return master.Transport.WriteTimeout;
            }
            set
            {
                master.Transport.WriteTimeout = value;
            }
        }

        /// <summary>
        /// Modbus写参数队列
        /// </summary>
        protected ConcurrentQueue<ModbusMethodInfo> Queues = new ConcurrentQueue<ModbusMethodInfo>();

        /// <summary>
        /// Modbus Transport
        /// </summary>
        /// <param name="key"></param>
        /// <param name="master"></param>
        public ModbusTransportDevice(String key, Modbus.Device.IModbusMaster master)
        {
            this.Key = key;
            this.master = master;
        }

        public void Dispose()
        {
            InstanceExtensions.DisposeNModbus4Master(ref master);
            StopListener();
        }

        /// <summary>
        /// 添加 Modbus 设备
        /// </summary>
        /// <param name="device"></param>
        public bool AddDevice(ModbusIODevice device)
        {
            foreach (ModbusIODevice dev in Devices)
                if (dev.Address == device.Address) return false;

            Devices.Add(device);
            return true;
        }

        /// <summary>
        /// 跟据设备地址获取设备
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public ModbusIODevice GetDevice(byte address)
        {
            foreach (ModbusIODevice device in Devices)
                if (device.Address == address) return device;

            return null;
        }

        /// <summary>
        /// 启动监听
        /// </summary>
        public void StartListener()
        {
            IsRunning = true;
            var result = ThreadPool.QueueUserWorkItem(new WaitCallback(SyncDeviceStatus), this);
            Log.InfoFormat($"设备总线 ({Key}) 数据处理线程入池 {result}");
        }

        /// <summary>
        /// 停止监听
        /// </summary>
        public void StopListener()
        {
            IsRunning = false;
        }

        private static void SyncDeviceStatus(object obj)
        {
            ModbusTransportDevice modbus = (ModbusTransportDevice)obj;

            List<ModbusIODevice> devices = modbus.Devices;
            byte deviceCount = (byte)devices.Count();
            Stopwatch stopwatch = new Stopwatch();

            while (modbus.IsRunning)
            {
                //stopwatch.Restart();
                for (ushort i = 0; i < deviceCount; i++)
                {
                    modbus.ReadDigitalInputStatus(devices[i]);
                    modbus.ReadAnalogInputStatus(devices[i]);

                    modbus.ReadDigitalOutputStatus(devices[i]);
                    modbus.ReadAnalogOutputStatus(devices[i]);
                }
                //stopwatch.Stop();
                //Log.Debug($"use {stopwatch.ElapsedMilliseconds} ms");
            }

            Log.InfoFormat($"已退出设备总线 ({modbus.Key}) 数据处理线程({Thread.CurrentThread.ManagedThreadId})");
        }

        /// <summary>
        /// 读取数字输入状态信号
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        protected bool ReadDigitalInputStatus(ModbusIODevice device)
        {
            if (!device.EnabledInputRead || !IsRunning) return false;
            if (device.DigitalInputStatus?.Length <= 0) return false;

            HandlerWriteQueues();
            bool[] result = null;

            try
            {
                result = master?.ReadInputs(device.Address, 0x0000, (ushort)device.DigitalInputStatus.Length);
            }
            catch (Exception ex)
            {
                Log.Error("ReadDigitalInputStatus:ReadInputs::", ex);
                return false;
            }

            if (result == null) return false;
            if (result.SequenceEqual(device.DigitalInputStatus)) return true;
            if (InputChangeEvent == null)
            {
                Array.Copy(result, device.DigitalInputStatus, result.Length);
                return true;
            }

            for (ushort i = 0; i < result.Length; i++)
            {
                if (result[i] != device.DigitalInputStatus[i])
                {
                    InputChangeEvent?.Invoke(this, device.Address, i, (ushort)(result[i] ? 1 : 0));
                    device.DigitalInputStatus[i] = result[i];
                }
            }

            return true;
        }
        /// <summary>
        /// 读取数字输出状态
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        protected bool ReadDigitalOutputStatus(ModbusIODevice device)
        {
            if (!device.EnabledOutputRead || !IsRunning) return false;
            if (device.DigitalOutputStatus?.Length <= 0) return false;

            HandlerWriteQueues();
            bool[] result = null;
            try
            {
                result = master?.ReadCoils(device.Address, 0x0000, (ushort)device.DigitalOutputStatus.Length);
            }
            catch (Exception ex)
            {
                Log.Error("ReadDigitalOutputStatus:ReadCoils::", ex);
                return false;
            }

            if (result == null) return false;
            if (result.SequenceEqual(device.DigitalOutputStatus)) return true;
            if (OutputChangeEvent == null)
            {
                Array.Copy(result, device.DigitalOutputStatus, result.Length);
                return true;
            }

            for (ushort j = 0; j < result.Length; j++)
            {
                if (result[j] != device.DigitalOutputStatus[j])
                {
                    OutputChangeEvent?.Invoke(this, device.Address, j, (ushort)(result[j] ? 1 : 0));
                    device.DigitalOutputStatus[j] = result[j];
                }
            }

            return true;
        }
        /// <summary>
        /// 读取模拟输入状态数据
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        protected bool ReadAnalogInputStatus(ModbusIODevice device)
        {
            if (!device.EnabledInputRead || !IsRunning) return false;
            if (device.AnalogInputStatus?.Length <= 0) return false;

            HandlerWriteQueues();
            ushort[] result = null;
            try
            {
                result = master?.ReadInputRegisters(device.Address, 0x0000, (ushort)device.DigitalInputStatus.Length);
            }
            catch (Exception ex)
            {
                Log.Error("ReadAnalogInputStatus:ReadInputRegisters::", ex);
                return false;
            }

            if (result == null) return false;
            if (result.SequenceEqual(device.AnalogInputStatus)) return true;
            if (InputChangeEvent == null)
            {
                Array.Copy(result, device.AnalogInputStatus, result.Length);
                return true;
            }

            for (ushort j = 0; j < result.Length; j++)
            {
                if (result[j] != device.AnalogInputStatus[j])
                {
                    InputChangeEvent?.Invoke(this, device.Address, j, result[j]);
                    device.AnalogInputStatus[j] = result[j];
                }
            }

            return true;
        }
        /// <summary>
        /// 读取模拟输出状态
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        protected bool ReadAnalogOutputStatus(ModbusIODevice device)
        {
            if (!device.EnabledOutputRead || !IsRunning) return false;
            if (device.AnalogOutputStatus?.Length <= 0) return false;

            HandlerWriteQueues();
            ushort[] result = null;

            try
            {
                result = master?.ReadHoldingRegisters(device.Address, 0x0000, (ushort)device.AnalogOutputStatus.Length);
            }
            catch (Exception ex)
            {
                Log.Error("AnalogOutputStatus:ReadHoldingRegisters::", ex);
                return false;
            }

            if (result == null) return false;
            if (result.SequenceEqual(device.AnalogOutputStatus)) return true;
            if (OutputChangeEvent == null)
            {
                Array.Copy(result, device.AnalogOutputStatus, result.Length);
                return true;
            }

            for (ushort j = 0; j < result.Length; j++)
            {
                if (result[j] != device.AnalogOutputStatus[j])
                {
                    OutputChangeEvent?.Invoke(this, device.Address, j, result[j]);
                    device.AnalogOutputStatus[j] = result[j];
                }
            }

            return true;
        }

        /// <summary>
        /// 等待间隔时间
        /// </summary>
        /// <param name="millisecondsTimeout"></param>
        public void Sleep(int millisecondsTimeout)
        {
            ModbusMethodInfo info = new ModbusMethodInfo
            {
                MethodName = "Sleep",
                SlaveAddress = 0,
                StartAddress = 0,
                Value = millisecondsTimeout,
            };
        }

        /// <summary>
        /// 翻转单线圈
        /// </summary>
        /// <param name="address"></param>
        /// <param name="startAddress"></param>
        public void TurnSingleCoil(byte address, ushort startAddress)
        {
            TurnMultipleCoilis(address, startAddress, 1);
        }

        /// <summary>
        /// 翻转多线圈
        /// </summary>
        /// <param name="address"></param>
        /// <param name="startAddress"></param>
        /// <param name="length"></param>
        public void TurnMultipleCoilis(byte address, ushort startAddress, byte length)
        {
            ModbusIODevice device = this.GetDevice(address);
            if (device == null) return;

            bool[] value = new bool[length];
            for (int i = startAddress; i < startAddress + length; i++)
            {
                value[i - startAddress] = !device.DigitalOutputStatus[startAddress];
            }

            WriteMultipleCoils(address, startAddress, value);
        }

        /// <summary>
        /// 写单个线圈状态
        /// </summary>
        /// <param name="address"></param>
        /// <param name="startAddress"></param>
        /// <param name="value"></param>
        public void WriteSingleCoil(byte address, ushort startAddress, bool value)
        {
            ModbusMethodInfo info = new ModbusMethodInfo
            {
                MethodName = "WriteSingleCoil",
                SlaveAddress = address,
                StartAddress = startAddress,
                Value = value,
            };

            Queues.Enqueue(info);
        }
        /// <summary>
        /// 写多个线圈状态
        /// </summary>
        /// <param name="address"></param>
        /// <param name="startAddress"></param>
        /// <param name="value"></param>
        public void WriteMultipleCoils(byte address, ushort startAddress, params bool[] value)
        {
            ModbusMethodInfo info = new ModbusMethodInfo
            {
                MethodName = "WriteMultipleCoils",
                SlaveAddress = address,
                StartAddress = startAddress,
                Value = value,
            };

            Queues.Enqueue(info);
        }
        /// <summary>
        /// 写单个保持寄存器
        /// </summary>
        /// <param name="address"></param>
        /// <param name="startAddress"></param>
        /// <param name="value"></param>
        public void WriteSingleRegister(byte address, ushort startAddress, ushort value)
        {
            ModbusMethodInfo info = new ModbusMethodInfo
            {
                MethodName = "WriteSingleRegister",
                SlaveAddress = address,
                StartAddress = startAddress,
                Value = value,
            };

            Queues.Enqueue(info);
        }
        /// <summary>
        /// 写多个保持寄存器
        /// </summary>
        /// <param name="address"></param>
        /// <param name="startAddress"></param>
        /// <param name="value"></param>
        public void WriteMultipleRegisters(byte address, ushort startAddress, ushort[] value)
        {
            ModbusMethodInfo info = new ModbusMethodInfo
            {
                MethodName = "WriteMultipleRegisters",
                SlaveAddress = address,
                StartAddress = startAddress,
                Value = value,
            };

            Queues.Enqueue(info);
        }

        protected void HandlerWriteQueues()
        {
            if (Queues.Count() <= 0 || !IsRunning) return;

            ModbusMethodInfo info;
            ModbusIODevice device;
            if (!Queues.TryDequeue(out info)) return;

            try
            {
                switch (info.MethodName)
                {
                    case "WriteSingleCoil":
                        master.WriteSingleCoil(info.SlaveAddress, info.StartAddress, (bool)info.Value);

                        device = GetDevice(info.SlaveAddress);
                        if (device == null) return;
                        device.DigitalOutputStatus[info.StartAddress] = (bool)info.Value;
                        break;

                    case "WriteMultipleCoils":
                        bool[] value = (bool[])info.Value;
                        master.WriteMultipleCoils(info.SlaveAddress, info.StartAddress, value);

                        device = GetDevice(info.SlaveAddress);
                        if (device == null) return;
                        Array.Copy(value, 0, device.DigitalOutputStatus, info.StartAddress, value.Length);
                        break;

                    case "WriteSingleRegister":
                        master.WriteSingleRegister(info.SlaveAddress, info.StartAddress, (ushort)info.Value);

                        device = GetDevice(info.SlaveAddress);
                        if (device == null) return;
                        device.AnalogOutputStatus[info.StartAddress] = (ushort)info.Value;
                        break;

                    case "WriteMultipleRegisters":
                        ushort[] aValue = (ushort[])info.Value;
                        master.WriteMultipleRegisters(info.SlaveAddress, info.StartAddress, aValue);

                        device = GetDevice(info.SlaveAddress);
                        if (device == null) return;
                        Array.Copy(aValue, 0, device.AnalogOutputStatus, info.StartAddress, aValue.Length);
                        break;

                    case "Sleep":
                        if ((int)info.Value > 0)
                            Thread.Sleep((int)info.Value);
                        break;
                }

                Thread.Sleep(4);
            }
            catch (Exception ex)
            {
                Log.Error($"Handler Queus Error:{ex}");
            }
        }

    }

    /// <summary>
    /// Modbus Device Manager 对象
    /// </summary>
    public partial class ModbusDeviceManager : IDisposable
    {
        protected static readonly log4net.ILog Log = log4net.LogManager.GetLogger("ModbusDeviceManager");

        /// <summary>
        /// Modbus Config Items 
        /// </summary>
        public XElement ModbusConfigItems { get; private set; } = null;

        /// <summary>
        /// Transport Devices 列表
        /// </summary>
        public List<ModbusTransportDevice> TransportDevices { get; private set; } = new List<ModbusTransportDevice>(8);

        /// <summary>
        /// 可访问对象列表
        /// </summary>
        public ConcurrentDictionary<String, IDisposable> AccessObjects { get; private set; } = new ConcurrentDictionary<string, IDisposable>();

        public static readonly byte[] ReturnOK = new byte[4] { 0x4F, 0x4B, 0x0D, 0x0A }; //OK\r\n
        public static readonly byte[] ReturnError = new byte[7] { 0x45, 0x52, 0x52, 0x4F, 0x52, 0x0D, 0x0A }; //ERROR\r\n

        /// <summary>
        /// Modbus 设备管理对象
        /// <para>包含配置键：Modbus.Master.DIO, Network.Server.Interface</para>
        /// <para>包含可选配置键：Modbus.Master.LMS, Network.Client.Interface, ...</para>
        /// </summary>
        public ModbusDeviceManager(String accessKey = null)
        {
            Modbus.Device.IModbusMaster ModbusLMS = InstanceExtensions.CreateNModbus4Master("Modbus.Master.LCMS");
            Modbus.Device.IModbusMaster ModbusDIO = InstanceExtensions.CreateNModbus4Master("Modbus.Master.DIO");
            if (ModbusLMS != null) AccessObjects.TryAdd("Modbus.Master.LCMS", ModbusLMS);
            if (ModbusDIO != null) AccessObjects.TryAdd("Modbus.Master.DIO", ModbusDIO);

            HPSocket.IServer NetworkServer = InstanceExtensions.CreateNetworkServer("Network.Server.Interface", OnServerReceiveEventHandler);
            HPSocket.IClient NetworkClient = InstanceExtensions.CreateNetworkClient("Network.Client.Interface", OnClientReceiveEventHandler);
            if (NetworkServer != null) AccessObjects.TryAdd("Network.Server.Interface", NetworkServer);
            if (NetworkClient != null) AccessObjects.TryAdd("Network.Client.Interface", NetworkClient);

            SerialPort SerialPort = InstanceExtensions.CreateSerialPort("Serial.PortName", null);
            if (SerialPort != null) AccessObjects.TryAdd("Serial.PortName", SerialPort);

            if (!String.IsNullOrWhiteSpace(accessKey)) AccessObjects.TryAdd(accessKey, this);
        }

        /// <summary>
        /// 添加传输设备
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        public bool AddModbusTransportDevice(ModbusTransportDevice device)
        {
            foreach (ModbusTransportDevice td in TransportDevices)
                if (td.Key == device.Key) return false;

            TransportDevices.Add(device);
            return true;
        }

        /// <summary>
        /// 添加传输设备
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public ModbusTransportDevice GetModbusTransportDevice(string key)
        {
            foreach (ModbusTransportDevice td in TransportDevices)
                if (td.Key == key) return td;

            return null;
        }


        protected HPSocket.HandleResult OnServerReceiveEventHandler(HPSocket.IServer sender, IntPtr connId, byte[] data)
        {
            ReceiveNetworkMessageHandler(Encoding.UTF8.GetString(data));
            return HPSocket.HandleResult.Ok;
        }
        protected HPSocket.HandleResult OnClientReceiveEventHandler(HPSocket.IClient sender, byte[] data)
        {
            ReceiveNetworkMessageHandler(Encoding.UTF8.GetString(data));
            return HPSocket.HandleResult.Ok;
        }
        public void ReceiveNetworkMessageHandler(String message)
        {
            if (message.IndexOf("Modbus.Master.DIO") != 0) return;

            String[] args = message.Split(',');
            if (args.Length != 5) return;

            InputOutputChange(args[0], args[1], Convert.ToByte(args[2]), Convert.ToUInt16(args[3]), Convert.ToUInt16(args[4]));
        }

        /// <summary>
        /// 添加可访问对象
        /// </summary>
        /// <param name="key"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        public bool TryAddAccessObject(string key, IDisposable obj)
        {
            if (String.IsNullOrWhiteSpace(key) || obj == null) return false;

            if (AccessObjects.ContainsKey(key))
            {
                Log.Warn($"添加可访问对象 {key}:{obj} 失败");
                return false;
            }
            return AccessObjects.TryAdd(key, obj);
        }
        /// <summary>
        /// 清除所有外部可访问对象
        /// </summary>
        public void DisposeAccessObjects()
        {
            InstanceExtensions.DisposeAccessObjects(AccessObjects);
            AccessObjects?.Clear();
            AccessObjects = null;
        }

        /// <summary>
        /// 加载设备配置文件
        /// </summary>
        /// <param name="configFile"></param>
        public void LoadDeviceConfig(String configFile)
        {
            if (!File.Exists(configFile))
            {
                Log.Error($"指定的配置文件不存在 {configFile}, 禁用 Modbus Device Manager 模块.");
                return;
            }

            //Reset Clear Handler
            foreach (var modbus in TransportDevices)
            {
                if (AccessObjects.ContainsKey(modbus.Key))
                    AccessObjects[modbus.Key] = modbus.master;
                else
                    AccessObjects.TryAdd(modbus.Key, modbus.master);

                modbus.StopListener();
            }
            TransportDevices.Clear();

            ModbusConfigItems = XElement.Load(configFile);
            IEnumerable<XElement> BusElements = ModbusConfigItems.Elements("Modbus");
            for (int i = 0; i < BusElements.Count(); i++)
            {
                XElement busElement = BusElements.ElementAt(i);
                if (busElement == null) continue;

                String key = busElement.Attribute("Key")?.Value;
                if (String.IsNullOrWhiteSpace(key))
                {
                    Log.Warn($"总线名称 Key 不能为空");
                    continue;
                }

                if (!AccessObjects.TryGetValue(key, out IDisposable value))
                {
                    Log.Warn($"未找到总线对象 {key}");
                    continue;
                }

                Modbus.Device.IModbusMaster master = (Modbus.Device.IModbusMaster)value;
                ModbusTransportDevice modbus = new ModbusTransportDevice(key, master);
                Log.Info($"创建设备传输总线 {key}: {master}");

                if (int.TryParse(busElement.Attribute("ReadTimeout")?.Value, out int readTimeout))
                    modbus.ReadTimeout = readTimeout;
                if (int.TryParse(busElement.Attribute("WriteTimeout")?.Value, out int writeTimeout))
                    modbus.WriteTimeout = writeTimeout;

                //Device
                IEnumerable<XElement> devices = busElement.Elements("Device");
                for (int j = 0; j < devices.Count(); j++)
                {
                    XElement device = devices.ElementAt(j);
                    byte address = 0x00, diCount = 0x00, doCount = 0x00, aiCount = 0x00, aoCount = 0x00;
                    if (!byte.TryParse(device.Attribute("Address")?.Value, out address) || address == 0x00)
                    {
                        Log.Warn($"总线 {key} 设备地址错误 {address}");
                        continue;
                    }
                    byte.TryParse(device.Attribute("DICount")?.Value, out diCount);
                    byte.TryParse(device.Attribute("DOCount")?.Value, out doCount);
                    byte.TryParse(device.Attribute("AICount")?.Value, out aiCount);
                    byte.TryParse(device.Attribute("AOCount")?.Value, out aoCount);

                    ModbusIODevice iodevice = ModbusIODevice.CreateDevice(address, diCount, doCount, aiCount, aoCount);
                    if (bool.TryParse(device.Attribute("EnabledInputRead")?.Value, out bool inputRead))
                        iodevice.EnabledInputRead = inputRead;
                    if (bool.TryParse(device.Attribute("EnabledOutputRead")?.Value, out bool outputRead))
                        iodevice.EnabledOutputRead = outputRead;

                    modbus.AddDevice(iodevice);
                    Log.Info($"传输总线 {key} 添加设备:{address}");
                }

                //InputChange
                var inputchange = from events in busElement.Elements("Events")
                                  where events.Attribute("Name").Value == "InputChange"
                                  select events;
                if (inputchange.Count() > 0)
                {
                    Log.Info($"传输总线 {key} 监听 InputChangeEvent ");
                    modbus.InputChangeEvent += Modbus_InputChangeEvent;
                }

                //OutputChange
                var outputchange = from events in busElement.Elements("Events")
                                   where events.Attribute("Name").Value == "OutputChange"
                                   select events;
                if (outputchange.Count() > 0)
                {
                    Log.Info($"传输总线 {key} 监听 OutputChangeEvent ");
                    modbus.OutputChangeEvent += Modbus_OutputChangeEvent;
                }

                if (AccessObjects.ContainsKey(key))
                    AccessObjects[key] = modbus;
                else
                    AccessObjects.TryAdd(key, modbus);

                TransportDevices.Add(modbus);
                modbus.StartListener();
            }
        }

        protected void Modbus_InputChangeEvent(ModbusTransportDevice modbus, byte address, ushort index, ushort value)
        {
            InputOutputChange(modbus.Key, "InputChange", address, index, value);
        }
        protected void Modbus_OutputChangeEvent(ModbusTransportDevice modbus, byte address, ushort index, ushort value)
        {
            InputOutputChange(modbus.Key, "OutputChange", address, index, value);
        }

        public void InputOutputChange(string hotKey)
        {
            if (ModbusConfigItems == null) return;

            Task.Run(() =>
            {
                var elements = from modbus in ModbusConfigItems.Elements()
                               from ev in modbus.Elements("Events")
                               where ev.Attribute("HotKey")?.Value == hotKey
                               select ev;

                if (elements?.Count() > 0)
                    Log.Info($"HotKey: {hotKey}");

                foreach (XElement events in elements)
                {
                    foreach (XElement action in events.Elements("Action"))
                    {
                        if (action.Attribute("TargetKey") == null) continue;

                        String objKey = action.Attribute("TargetKey")?.Value;
                        if (!AccessObjects.TryGetValue(objKey, out IDisposable targetObj))
                        {
                            Log.Warn($"未找到时目标对象实例 {objKey} ");
                            continue;
                        }

                        //Method
                        String methodName = action.Attribute("Method")?.Value;
                        if (String.IsNullOrWhiteSpace(methodName)) continue;

                        if (!String.IsNullOrWhiteSpace(action.Attribute("Params")?.Value))
                            InstanceExtensions.CallInstanceMethod(targetObj, methodName, StringExtension.ConvertParameters(action.Attribute("Params").Value));
                        else
                            InstanceExtensions.CallInstanceMethod(targetObj, methodName);
                    }
                }
            });
        }

        /// <summary>
        /// IO Change Handler
        /// </summary>
        /// <param name="busKey"></param>
        /// <param name="eventType"></param>
        /// <param name="slaveAddress"></param>
        /// <param name="startAddress"></param>
        /// <param name="value"></param>
        public void InputOutputChange(string busKey, string eventType, byte slaveAddress, ushort startAddress, ushort value)
        {
            if (ModbusConfigItems == null) return;
            Task.Run(() =>
            {
                if (Log.IsDebugEnabled)
                    Log.Debug($"{busKey}, {eventType}, {slaveAddress}, {startAddress}, {value}");

                var elements = from modbus in ModbusConfigItems.Elements()
                               where modbus.Attribute("Key")?.Value == busKey
                               from ev in modbus.Elements("Events")
                               where ev.Attribute("Name")?.Value == eventType &&
                               ev.Attribute("Address")?.Value == slaveAddress.ToString() &&
                               ev.Attribute("Index")?.Value == startAddress.ToString() &&
                               ev.Attribute("Value")?.Value == value.ToString()
                               select ev.Elements("Action");

                if (elements.Count() <= 0) return;

                foreach (var events in elements)
                {
                    foreach (XElement action in events)
                    {

                        if (action.Attribute("TargetKey") == null) continue;

                        String objKey = action.Attribute("TargetKey")?.Value;
                        if (!AccessObjects.TryGetValue(objKey, out IDisposable targetObj))
                        {
                            Log.Warn($"未找到时目标对象实例 {objKey} ");
                            continue;
                        }

                        //Method
                        String methodName = action.Attribute("Method")?.Value;
                        if (String.IsNullOrWhiteSpace(methodName)) continue;

                        if (!String.IsNullOrWhiteSpace(action.Attribute("Params")?.Value))
                            InstanceExtensions.CallInstanceMethod(targetObj, methodName, StringExtension.ConvertParameters(action.Attribute("Params").Value));
                        else
                            InstanceExtensions.CallInstanceMethod(targetObj, methodName);
                    }
                }
            });
        }

        public void Dispose()
        {
            foreach (var modbus in TransportDevices)
            {
                modbus?.Dispose();
            }
            TransportDevices.Clear();
            DisposeAccessObjects();
        }

    }
}
