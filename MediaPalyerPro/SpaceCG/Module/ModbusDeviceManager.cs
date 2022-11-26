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
    /// <summary>
    /// Modbus IO 设备
    /// </summary>
    class ModbusIODevice
    {
        /// <summary>
        /// 设备地址
        /// </summary>
        public byte Address { get; private set; } = 0x00;

        /// <summary>
        /// 数字输入信号状态集合
        /// </summary>
        public bool[] DigitalOutputStatus { get; private set; }
        /// <summary>
        /// 数字输出信号状态集合
        /// </summary>
        public bool[] DigitalInputStatus { get; private set; }

        /// <summary>
        /// 模拟信号输入状态集合
        /// </summary>
        public ushort[] AnalogInputStatus { get; private set; }
        /// <summary>
        /// 模拟信号输出状态集合
        /// </summary>
        public ushort[] AnalogOutputStatus { get; private set; }

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

    /// <summary>
    /// Modbus 传输设备
    /// </summary>
    class ModbusTransportDevice : IDisposable
    {
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger("ModbusTransportDevice");

        public String Key { get; private set; }

        public event Action<ModbusTransportDevice, byte, ushort, ushort> InputChangeEvent;
        public event Action<ModbusTransportDevice, byte, ushort, ushort> OutputChangeEvent;

        //internal NModbus.IModbusMaster master;
        internal Modbus.Device.IModbusMaster master;
        private bool IsRunning = false;

        public List<ModbusIODevice> Devices { get; private set; } = new List<ModbusIODevice>(8);

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

        class methodInfo
        {
            public string methodName;
            //public byte funcCode;
            public byte slaveAddress;
            public ushort startAddress;
            public object data;
        }

        private ConcurrentQueue<methodInfo> queues = new ConcurrentQueue<methodInfo>();

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
            InstanceExtension.DisposeNModbus4Master(ref master);
            StopListener();
        }

        /// <summary>
        /// 添加设备
        /// </summary>
        /// <param name="device"></param>
        public void AddDevice(ModbusIODevice device)
        {
            Devices.Add(device);
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
        private bool ReadDigitalInputStatus(ModbusIODevice device)
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
        private bool ReadDigitalOutputStatus(ModbusIODevice device)
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
        private bool ReadAnalogInputStatus(ModbusIODevice device)
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
        private bool ReadAnalogOutputStatus(ModbusIODevice device)
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
        /// 写单个线圈状态
        /// </summary>
        /// <param name="address"></param>
        /// <param name="startAddress"></param>
        /// <param name="value"></param>
        public void WriteSingleCoil(byte address, ushort startAddress, bool value)
        {
            methodInfo info = new methodInfo
            {
                methodName = "WriteSingleCoil",
                slaveAddress = address,
                startAddress = startAddress,
                data = value,
            };

            queues.Enqueue(info);
        }
        /// <summary>
        /// 写多个线圈状态
        /// </summary>
        /// <param name="address"></param>
        /// <param name="startAddress"></param>
        /// <param name="value"></param>
        public void WriteMultipleCoils(byte address, ushort startAddress, params bool[] value)
        {
            methodInfo info = new methodInfo
            {
                methodName = "WriteMultipleCoils",
                slaveAddress = address,
                startAddress = startAddress,
                data = value,
            };

            queues.Enqueue(info);
        }
        /// <summary>
        /// 写单个保持寄存器
        /// </summary>
        /// <param name="address"></param>
        /// <param name="startAddress"></param>
        /// <param name="value"></param>
        public void WriteSingleRegister(byte address, ushort startAddress, ushort value)
        {
            methodInfo info = new methodInfo
            {
                methodName = "WriteSingleRegister",
                slaveAddress = address,
                startAddress = startAddress,
                data = value,
            };

            queues.Enqueue(info);
        }
        /// <summary>
        /// 写多个保持寄存器
        /// </summary>
        /// <param name="address"></param>
        /// <param name="startAddress"></param>
        /// <param name="value"></param>
        public void WriteMultipleRegisters(byte address, ushort startAddress, ushort[] value)
        {
            methodInfo info = new methodInfo
            {
                methodName = "WriteMultipleRegisters",
                slaveAddress = address,
                startAddress = startAddress,
                data = value,
            };

            queues.Enqueue(info);
        }

        private void HandlerWriteQueues()
        {
            if (queues.Count() <= 0 || !IsRunning) return;

            methodInfo info;
            if (!queues.TryDequeue(out info)) return;

            try
            {
                switch (info.methodName)
                {
                    case "WriteSingleCoil":
                        master.WriteSingleCoil(info.slaveAddress, info.startAddress, (bool)info.data);
                        break;

                    case "WriteMultipleCoils":
                        master.WriteMultipleCoils(info.slaveAddress, info.startAddress, (bool[])info.data);
                        break;

                    case "WriteSingleRegister":
                        master.WriteSingleRegister(info.slaveAddress, info.startAddress, (ushort)info.data);
                        break;

                    case "WriteMultipleRegisters":
                        master.WriteMultipleRegisters(info.slaveAddress, info.startAddress, (ushort[])info.data);
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
    public class ModbusDeviceManager : IDisposable
    {
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger("ModbusDeviceManager");

        private XElement DevicesList = null;        
        private List<ModbusTransportDevice> transportDevices = new List<ModbusTransportDevice>(8);
        private ConcurrentDictionary<String, IDisposable> AccessObjects = new ConcurrentDictionary<string, IDisposable>();

        public static readonly byte[] ReturnOK = new byte[4] { 0x4F, 0x4B, 0x0D, 0x0A }; //OK\r\n
        public static readonly byte[] ReturnError = new byte[7] { 0x45, 0x52, 0x52, 0x4F, 0x52, 0x0D, 0x0A }; //ERROR\r\n

        /// <summary>
        /// Modbus 设备管理对象
        /// <para>包含配置键：Modbus.Master.DIO, Network.Server.Interface</para>
        /// <para>包含可选配置键：Modbus.Master.LMS, Network.Client.Demo, ...</para>
        /// </summary>
        public ModbusDeviceManager()
        {
            Modbus.Device.IModbusMaster ModbusLMS = InstanceExtension.CreateNModbus4Master("Modbus.Master.LMS");
            Modbus.Device.IModbusMaster ModbusDIO = InstanceExtension.CreateNModbus4Master("Modbus.Master.DIO");
            if (ModbusLMS != null) AccessObjects.TryAdd("Modbus.Master.LMS", ModbusLMS);
            if (ModbusDIO != null) AccessObjects.TryAdd("Modbus.Master.DIO", ModbusDIO);

            HPSocket.IServer NetworkServer = InstanceExtension.CreateNetworkServer("Network.Server.Interface", OnServerReceiveEventHandler);
            HPSocket.IClient NetworkClient = InstanceExtension.CreateNetworkClient("Network.Client.Demo", OnClientReceiveEventHandler);
            if (NetworkServer != null) AccessObjects.TryAdd("Network.Server.Interface", NetworkServer);
            if (NetworkClient != null) AccessObjects.TryAdd("Network.Client.Demo", NetworkClient);

            SerialPort SerialPort = InstanceExtension.CreateSerialPort("Serial.PortName", null);
            if (SerialPort != null) AccessObjects.TryAdd("Serial.PortName", SerialPort);
        }

        private void KeyboardHookEx_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            Log.Debug($"key down::{ e.Key}");
        }

        private void KeyboardHookEx_KeyPress(object sender, System.Windows.Input.KeyEventArgs e)
        {
            Log.Debug($"key press::{ e.Key}");
        }

        private HPSocket.HandleResult OnServerReceiveEventHandler(HPSocket.IServer sender, IntPtr connId, byte[] data)
        {
            ReceiveNetworkMessageHandler(Encoding.UTF8.GetString(data));
            return HPSocket.HandleResult.Ok;
        }
        private HPSocket.HandleResult OnClientReceiveEventHandler(HPSocket.IClient sender, byte[] data)
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
            InstanceExtension.DisposeAccessObjects(AccessObjects);
            AccessObjects.Clear();
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
                Log.Error($"指定的配置文件不存在 {configFile}");
                return;
            }

            //Reset Clear Handler
            foreach (var modbus in transportDevices)
            {
                if (AccessObjects.ContainsKey(modbus.Key))
                    AccessObjects[modbus.Key] = modbus.master;
                else
                    AccessObjects.TryAdd(modbus.Key, modbus.master);

                modbus.StopListener();
            }
            transportDevices.Clear();

            DevicesList = XElement.Load(configFile);
            IEnumerable<XElement> BusElements = DevicesList.Descendants("Bus");
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
                IEnumerable<XElement> devices = busElement.Descendants("Device");
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

                transportDevices.Add(modbus);
                modbus.StartListener();
            }
        }

        private void Modbus_InputChangeEvent(ModbusTransportDevice modbus, byte address, ushort index, ushort value)
        {
            InputOutputChange(modbus.Key, "InputChange", address, index, value);
        }
        private void Modbus_OutputChangeEvent(ModbusTransportDevice modbus, byte address, ushort index, ushort value)
        {
            InputOutputChange(modbus.Key, "OutputChange", address, index, value);
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
            Task.Run(() =>
            {
                if (Log.IsDebugEnabled)
                    Log.Debug($"{busKey}, {eventType}, {slaveAddress}, {startAddress}, {value}");

                var elements = from bus in DevicesList.Descendants()
                               where bus.Attribute("Key")?.Value == busKey
                               from ev in bus.Descendants("Events")
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
                            InstanceExtension.CallInstanceMethod(targetObj, methodName, StringExtension.ConvertParameters(action.Attribute("Params").Value));
                        else
                            InstanceExtension.CallInstanceMethod(targetObj, methodName);
                    }
                }
            });
        }

        public void Dispose()
        {
            foreach (var modbus in transportDevices)
            {
                modbus?.Dispose();
            }
            transportDevices.Clear();
            DisposeAccessObjects();
        }
    }
}
