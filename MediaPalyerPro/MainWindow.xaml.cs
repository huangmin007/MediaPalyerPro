using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Sttplay.MediaPlayer;
using SpaceCG.Generic;
using System.Threading.Tasks;
using HPSocket;
using System.Collections.Concurrent;
using System.Windows.Markup;
using System.Windows.Controls;
using System.Xml;
using System.Xaml;

namespace MediaPalyerPro
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public static readonly log4net.ILog Log = log4net.LogManager.GetLogger(nameof(MainWindow));

        private XElement RootElement = null;
        private IEnumerable<XElement> ListItems;

        private int CurrentItemID = -1;

        private XElement CurrentItem = null;

        private Boolean PlayListAutoLoop = false;

        private static Process ProcessModule;
        //private ModbusServices ModbusServices;

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
        private ushort SyncWaitCount = 120;
        private byte[] SyncMessage = new byte[16];

        private MainWindow Window;
        private ConcurrentDictionary<String, IDisposable> AccessObjects = new ConcurrentDictionary<string, IDisposable>();

        public MainWindow()
        {
            this.Window = this;
            InitializeComponent();

            InstanceExtension.ChangeInstancePropertyValue(this, "Window.");
            ProcessModule = InstanceExtension.CreateProcessModule("Process.FileName");

#if DEBUG
            ((log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository()).Root.Level = log4net.Core.Level.Debug;
#endif

        }

        #region Inherit Functions
        /// <inheritdoc/>
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            InstanceExtension.DisposeAccessObjects(AccessObjects);

            InstanceExtension.DisposeNetworkClient(ref UDPClientSync);
            InstanceExtension.DisposeNetworkServer(ref UDPServerSync);

            InstanceExtension.RemoveInstanceEvents(Player);
            InstanceExtension.DisposeProcessModule(ref ProcessModule);
        }
        /// <inheritdoc/>
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            DragMove();
            base.OnMouseLeftButtonDown(e);
        }
        /// <inheritdoc/>
        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);

            try
            {
                var fileName = ((Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();

                if (IsVideoFile(fileName))
                {
                    Player.Visibility = Visibility.Visible;
                    Player.Open(Sttplay.MediaPlayer.MediaType.Link, fileName);
                }
                else if (IsImageFile(fileName))
                {
                    Player.Visibility = Visibility.Visible;
                    Player.Source = new BitmapImage(new Uri(fileName, UriKind.RelativeOrAbsolute));
                }
                else
                {
                    MessageBox.Show($"不支持的文件类型 {fileName}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        /// <inheritdoc/>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.IsRepeat) return;
            Log.Info($"OnKeyDown: {e.KeyboardDevice.Modifiers} - {e.Key}");

            switch (e.Key)
            {
                case Key.D0:
                case Key.D1:
                case Key.D2:
                case Key.D3:
                case Key.D4:
                case Key.D5:
                case Key.D6:
                case Key.D7:
                case Key.D8:
                case Key.D9:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.None)
                        LoadItem((ushort)(e.Key - Key.D0));
                    else if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                        ExceConfigQKey(String.Format("QKEY.0x{0:X2}", e.Key - Key.D0));
                    break;
                case Key.D:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        log4net.Repository.Hierarchy.Logger root = ((log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository()).Root;
                        root.Level = (root.Level == log4net.Core.Level.Info) ? log4net.Core.Level.Debug : log4net.Core.Level.Info;
                        Log.Warn($"Root Logger Current Level: {root.Level}");
                    }
                    break;
                case Key.R:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        Process.Start(Process.GetCurrentProcess().MainModule.FileName);
                        Application.Current.Shutdown();
                    }
                    break;

                case Key.F:
                    this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                    break;
                case Key.S:
                    if (!this.AllowsTransparency)
                        this.WindowStyle = this.WindowStyle == WindowStyle.None ? WindowStyle.SingleBorderWindow : WindowStyle.None;
                    break;
                case Key.T:
                    this.Topmost = !this.Topmost;
                    break;

                case Key.Down:
                case Key.Right:
                    if (CurrentItem == null) return;
                    if (CurrentItem.NextNode != null)
                        LoadItem((XElement)CurrentItem.NextNode);
                    else
                        LoadItem((XElement)CurrentItem.Parent.FirstNode);
                    break;
                case Key.Up:
                case Key.Left:
                    if (CurrentItem == null) return;
                    if (CurrentItem.PreviousNode != null)
                        LoadItem((XElement)CurrentItem.PreviousNode);
                    else
                        LoadItem((XElement)CurrentItem.Parent.LastNode);
                    break;

                case Key.Space:
                case Key.Enter:
                    if (Player.IsPaused)
                        Player.Play();
                    else
                        Player.Pause();
                    break;

                case Key.Escape:
                    this.Close();
                    Application.Current.Shutdown(0);
                    break;
            }
        }
        #endregion

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //Create Instance
            Modbus.Device.IModbusMaster ModbusLMS = InstanceExtension.CreateNModbus4Master("Modbus.Master.LMS");
            Modbus.Device.IModbusMaster ModbusDIO = InstanceExtension.CreateNModbus4Master("Modbus.Master.DIO");
            if (ModbusLMS != null) AccessObjects.TryAdd("Modbus.Master.LMS", ModbusLMS);
            if (ModbusDIO != null) AccessObjects.TryAdd("Modbus.Master.DIO", ModbusDIO);
            //Create Instance
            HPSocket.IServer NetworkServer = InstanceExtension.CreateNetworkServer("Network.Server");
            HPSocket.IClient NetworkClient = InstanceExtension.CreateNetworkClient("Network.Client");
            if (NetworkServer != null) AccessObjects.TryAdd("Network.Server", NetworkServer);
            if (NetworkClient != null) AccessObjects.TryAdd("Network.Client", NetworkClient);

            //多端同步
            UDPClientSync = InstanceExtension.CreateNetworkClient("Synchronize.Slave", OnClientReceiveEventHandler);
            if (UDPClientSync == null)
                UDPServerSync = InstanceExtension.CreateNetworkServer("Synchronize.Master", onServerReceiveEventHandler);
            if (UDPServerSync != null && ushort.TryParse(ConfigurationManager.AppSettings["Synchronize.Calibr"], out ushort calibr)) SyncCalibr = calibr;
            if (UDPServerSync != null && ushort.TryParse(ConfigurationManager.AppSettings["Synchronize.WaitCount"], out ushort waitCount)) SyncWaitCount = waitCount;
            if (UDPClientSync != null) Player.Volume = 0.0f;

            //Background
            ConfigurationBackground("Background.Image");

            //VideoPlayer
            //InstanceExtension.ChangeInstancePropertyValue(Player, "Player.");
            if (!String.IsNullOrWhiteSpace(Player.Url) && File.Exists(Player.Url))
            {
                OpenMediaFile(Player, Player.Url);
            }
            else
            {
                //读取并播放列表文件
                String fileName = "MediaPlayList.Config";
                if (!File.Exists(fileName)) return;

                try
                {
                    RootElement = XElement.Load(fileName);
                    ListItems = RootElement.Descendants("Item");

                    XAttribute autoLoop = RootElement.Attribute("AutoLoop");
                    XAttribute autoPlayId = RootElement.Attribute("AutoPlayID");

                    if (bool.TryParse(autoLoop?.Value, out bool loop))
                        PlayListAutoLoop = loop;

                    if (int.TryParse(autoPlayId?.Value, out int id))
                        LoadItem((ushort)id);
                }
                catch (Exception ex)
                {
                    Log.Error($"读取 {fileName} 文件错误：{ex}");
                }
            }
        }

        /// <summary>
        /// 播放列表项
        /// </summary>
        /// <param name="id"></param>
        public void LoadItem(int id)
        {
            if (ListItems?.Count() <= 0) return;

            IEnumerable<XElement> items = from element in ListItems
                                          from attribute in element.Attributes()
                                          where attribute?.Name == "ID" && attribute?.Value == id.ToString()
                                          select element;

            Log.Info($"List Item ID: {id} ,Count: {items.Count()}");
            if (items.Count() != 1)
            {
                Log.Error($"配置列表中存在多个相同的 ID: {id} ");
                return;
            }

            LoadItem(items.ElementAt(0));
        }
        /// <summary>
        /// 播放列表项,,https://www.codenong.com/54797577/
        /// </summary>
        /// <param name="item"></param>
        private void LoadItem(XElement item)
        {
            if (item == null) return;

            CurrentItem = item;
            if (int.TryParse(item.Attribute("ID")?.Value, out int id))
                CurrentItemID = id;

            foreach (XElement element in item.Elements())
                InstanceExtension.ChangeInstancePropertyValue(this, element);

            //StringReader stringReader = new StringReader("");
            XmlReaderSettings settings = new XmlReaderSettings { NameTable = new NameTable() };            
            XmlNamespaceManager xmlns = new XmlNamespaceManager(settings.NameTable);
            xmlns.AddNamespace("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            xmlns.AddNamespace("x", "http://schemas.microsoft.com/winfx/2006/xaml");
            XmlParserContext context = new XmlParserContext(null, xmlns, "", XmlSpace.Default);
            //XmlReader xmlReader = XmlReader.Create(stringReader, settings, context);
            XamlXmlReaderSettings xamlXmlReaderSettings = new XamlXmlReaderSettings();
            xamlXmlReaderSettings.LocalAssembly = System.Reflection.Assembly.GetExecutingAssembly();
            //XamlXmlReader xamlXmlReader = new XamlXmlReader(xmlReader, xamlXmlReaderSettings);
            //UIElement element = (UIElement)System.Windows.Markup.XamlReader.Load(xamlXmlReader);

            try
            {
                BackgroundCanvas.Children.Clear();
                foreach (XElement button in item.Element(BackgroundCanvas.Name)?.Elements("Button"))
                {
                    StringReader stringReader = new StringReader(button.ToString());
                    XmlReader xmlReader = XmlReader.Create(stringReader, settings, context);
                    XamlXmlReader xamlXmlReader = new XamlXmlReader(xmlReader, xamlXmlReaderSettings);
                    Button element = (Button)System.Windows.Markup.XamlReader.Load(xamlXmlReader);
                    BackgroundCanvas.Children.Add(element);
                }

                PlayerCanvas.Children.Clear();
                foreach (XElement button in item.Element(PlayerCanvas.Name)?.Elements("Button"))
                {
                    StringReader stringReader = new StringReader(button.ToString());
                    XmlReader xmlReader = XmlReader.Create(stringReader, settings, context);
                    XamlXmlReader xamlXmlReader = new XamlXmlReader(xmlReader, xamlXmlReaderSettings);
                    Button element = (Button)System.Windows.Markup.XamlReader.Load(xamlXmlReader);
                    PlayerCanvas.Children.Add(element);
                }
            }
            catch(Exception ex)
            {
                Log.Error($"动态添加显示对象异常：{ex}");
            }
            //if (item.Element("Player") == null) return;

            
            OpenMediaFile(Player, Player.Url);
        }

        /// <summary>
        /// 执行配置事件
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="currentTime"></param>
        /// <param name="lastTime"></param>
        private void ConfigEventHandler(String eventName, double currentTime = -1.0f, double lastTime = -1.0f)
        {
            IEnumerable<XElement> events = from evs in CurrentItem?.Element(nameof(Player))?.Elements("Events")
                                           where evs.Attribute("Name")?.Value == eventName
                                           select evs;
            if (events?.Count() == 0) return;

            foreach (XElement action in events.Elements("Action"))
            {
                if (currentTime >= 0 && lastTime >= 0)
                {
                    if (String.IsNullOrWhiteSpace(action.Parent.Attribute("Position")?.Value)) continue;
                    if (!double.TryParse(action.Parent.Attribute("Position").Value, out double position)) continue;

                    if (!(position <= currentTime && position > lastTime)) continue;
                }

                Object target = null;
                if (action.Attribute("TargetObj") != null)
                    target = InstanceExtension.GetInstanceFieldObject(this, action.Attribute("TargetObj").Value);
                else if (action.Attribute("TargetKey") != null)
                    target = AccessObjects.TryGetValue(action.Attribute("TargetKey").Value, out IDisposable obj) ? obj : null;
                if (target == null) continue;

                try
                {
                    Log.Info($"准备处理事件配置: {action} ");

                    //Method
                    if (!String.IsNullOrWhiteSpace(action.Attribute("Method")?.Value))
                    {
                        Task.Run(() =>
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                if (!String.IsNullOrWhiteSpace(action.Attribute("Params")?.Value))
                                    InstanceExtension.CallInstanceMethod(target, action.Attribute("Method").Value, StringExtension.ConvertParameters(action.Attribute("Params").Value));
                                else
                                    InstanceExtension.CallInstanceMethod(target, action.Attribute("Method").Value);
                            });
                        });
                    }
                    //Property
                    else if (!String.IsNullOrWhiteSpace(action.Attribute("Property")?.Value) && !String.IsNullOrWhiteSpace(action.Attribute("Value")?.Value))
                    {
                        Task.Run(() =>
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                InstanceExtension.ChangeInstancePropertyValue(target, action.Attribute("Property").Value, action.Attribute("Value").Value);
                            });
                        });
                    }
                    //Other
                    else
                    {
                        Log.Warn($"配置格式错误：{action}");
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }
        }

        public void TestEcho()
        {
            Log.Info("TestEcho, Hello World ...");
            Log.Info("TestEcho, Hello World ...");
            Log.Info("TestEcho, Hello World ...");
        }

        #region Player Events Handler
        private double LastTime = 0.0f;
        private IEnumerable<XElement> onRenderEvents;

        private void Player_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!IsVideoFile(Background.Url) || Background.Visibility != Visibility.Visible) return;

            if (Player.Visibility == Visibility.Visible)
                Background.Pause();
            else
                Background.Play();
        }

        private void OnCaptureOpenCallbackEvent(CaptureOpenResult result, string message, OpenCallbackContext context)
        {
            if (result != CaptureOpenResult.SUCCESS) return;

            PlayerVideoInfo();
            onRenderEvents = null;

            if (CurrentItem?.Element(nameof(Player)) != null)
            {
                IEnumerable<XElement> events = CurrentItem.Element(nameof(Player)).Elements("Events");
                if (events.Count() > 0)
                {
                    onRenderEvents = from ev in events
                                     where ev.Attribute("Name")?.Value == "OnRenderFrame"
                                     select ev;
                    if (onRenderEvents.Count() == 0) onRenderEvents = null;
                }
            }
        }
        private void OnRenderFrameEventHandler(Sttplay.MediaPlayer.SCFrame obj)
        {
            if (UDPServerSync != null)
            {
                List<IntPtr> clients = UDPServerSync.GetAllConnectionIds();
                if (clients.Count > 0)
                {
                    byte[] ct = BitConverter.GetBytes((int)Player.CurrentTime);  //4 Bytes
                    Array.Copy(ct, 0, SyncMessage, 0, ct.Length);

                    byte[] sc = BitConverter.GetBytes(SyncCalibr);      //2 Bytes
                    Array.Copy(sc, 0, SyncMessage, 4, sc.Length);

                    byte[] sw = BitConverter.GetBytes(SyncWaitCount);      //2 Bytes
                    Array.Copy(sc, 0, SyncMessage, 6, sw.Length);

                    // ... 

                    byte[] dt = BitConverter.GetBytes((int)Player.Duration);        //4 Bytes
                    Array.Copy(dt, 0, SyncMessage, SyncMessage.Length - dt.Length, dt.Length);

                    foreach (IntPtr client in clients)
                        UDPServerSync.Send(client, SyncMessage, SyncMessage.Length);
                }
            }

            if (onRenderEvents != null)
            {
                double currentTime = Math.Round(Player.CurrentTime / 1000.0f, 2);
                if (LastTime == currentTime) return;

                ConfigEventHandler("OnRenderFrame", currentTime, LastTime);
                LastTime = currentTime;
            }
        }
        private void OnFirstFrameRenderEventHandler(Sttplay.MediaPlayer.SCFrame obj)
        {
            if (Log.IsDebugEnabled)
                Log.Debug($"First Frame Render Evnet. {Player.Url}");

            if (UDPClientSync != null)  //4字节心跳
                UDPClientSync.Send(SyncMessage, SyncMessage.Length - 4, 4);

            ConfigEventHandler("OnFirstFrame");
            LastTime = Math.Round(Player.CurrentTime / 1000.0f, 2);
        }
        private void OnStreamFinishedEventHandler()
        {
            if (Log.IsDebugEnabled)
                Log.Debug($"Stream Finish Event. {Player.Url}");

            if (UDPClientSync != null)  //4字节心跳
                UDPClientSync.Send(SyncMessage, SyncMessage.Length - 4, 4);

            ConfigEventHandler("OnLastFrame");

            if (PlayListAutoLoop)
            {
                if (CurrentItem.NextNode != null)
                    LoadItem((XElement)CurrentItem.NextNode);
                else
                    LoadItem((XElement)CurrentItem.Parent.FirstNode);
            }
        }

        private HandleResult onServerReceiveEventHandler(IServer sender, IntPtr connId, byte[] data)
        {
            if (UDPClientSync != null) return HandleResult.Ok;
            if (data.Length != SyncMessage.Length) return HandleResult.Ok;

            UDPServerSync.GetRemoteAddress(connId, out String ip, out ushort port);
            if (data[0] == 0x01)
            {
                int diff = BitConverter.ToInt32(data, 2);
                int ct = BitConverter.ToInt32(data, 6);
                int dt = BitConverter.ToInt32(data, data.Length - 4);
                Log.Info($"远程主机(Slave) {ip}:{port} 校准时间，时间差：{diff}");
                Log.Info($"Current(Master)Video: {Player.CurrentTime}/{Player.Duration}    SlaveVideo: {ct}/{dt}    Diff: {diff}");
            }

            return HandleResult.Ok;
        }
        private HandleResult OnClientReceiveEventHandler(IClient sender, byte[] data)
        {
            if (UDPServerSync != null) return HandleResult.Ok;
            if (data.Length != SyncMessage.Length) return HandleResult.Ok;

            if (SyncWaitCount != 0)
            {
                SyncWaitCount--;
                return HandleResult.Ok;
            }

            int ct = BitConverter.ToInt32(data, 0);
            ushort sc = BitConverter.ToUInt16(data, 4);
            ushort sw = BitConverter.ToUInt16(data, 6);
            int dt = BitConverter.ToInt32(data, data.Length - 4);

            int DT = (int)Player.Duration;
            int CT = (int)Player.CurrentTime;
            int Diff = (int)Math.Abs(CT - ct);

            if (Diff > sc && ct != 0 && CT != 0)
            {
                SyncWaitCount = sw;
                this.Dispatcher.Invoke(() => Player.SeekFastMilliSecond(ct + 4));

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
        #endregion

        /// <summary>
        /// 获取配置键所操作的寄存器数据
        /// </summary>
        /// <param name="cfgKey"></param>
        /// <returns></returns>
        public bool ExceConfigQKey(String cfgKey)
        {
            if (String.IsNullOrWhiteSpace(ConfigurationManager.AppSettings[cfgKey]))
            {
                Log.WarnFormat("不存在的配置快捷指令键：{0} ", cfgKey);
                return false;
            }

            return false;
            //return WriteHoldingRegisters(ConfigurationManager.AppSettings[cfgKey]);
        }
        
        /// <summary>
        /// 配置背景属性
        /// </summary>
        /// <param name="cfgKey"></param>
        public void ConfigurationBackground(String cfgKey)
        {
            Background.Visibility = Visibility.Collapsed;
            if (String.IsNullOrWhiteSpace(ConfigurationManager.AppSettings[cfgKey])) return;

            String mediaFile = ConfigurationManager.AppSettings[cfgKey].Trim();
            if (!File.Exists(mediaFile))
            {
                Log.Warn($"Background 不存在的媒体文件 {mediaFile} ");
                return;
            }

            Background.Loop = true;
            Background.Volume = 0.8f;
            Background.OpenAndPlay = true;
            Background.Visibility = Visibility.Visible;
            InstanceExtension.ChangeInstancePropertyValue(Background, cfgKey.Substring(0, cfgKey.IndexOf('.') + 1));

            OpenMediaFile(Background, mediaFile);
        }
        /// <summary>
        /// 打印 Player 属性信息
        /// </summary>
        public void PlayerVideoInfo()
        {
            Type type = Player.GetType();
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (PropertyInfo property in properties)
            {
                Log.InfoFormat("{0}: {1}", property.Name, property.GetValue(Player));
            }
        }

        #region Static Functions
        public static bool IsVideoFile(String fileName)
        {
            if (String.IsNullOrWhiteSpace(fileName)) return false;

            FileInfo fileInfo = new FileInfo(fileName);
            if (!fileInfo.Exists) return false;
            String extension = fileInfo.Extension.ToUpper();

            switch (extension)
            {
                case ".MP4":
                case ".MOV":
                case ".M4V":
                case ".MKV":
                case ".MPG":
                case ".WEBM":
                case ".FLV":
                case ".F4V":
                case ".OGV":
                case ".TS":
                case ".MTS":
                case ".M2T":
                case ".M2TS":
                case ".3GP":
                case ".AVI":
                case ".WMV":
                case ".WTV":
                case ".MPEG":
                case ".RM":
                case ".RAM":
                    return true;
            }

            return false;
        }
        public static bool IsImageFile(String fileName)
        {
            if (String.IsNullOrWhiteSpace(fileName)) return false;

            FileInfo fileInfo = new FileInfo(fileName);
            if (!fileInfo.Exists) return false;
            String extension = fileInfo.Extension.ToUpper();

            switch (extension.ToUpper())
            {
                case ".JPG":
                case ".PNG":
                case ".BMP":
                case ".JPEG":
                    return true;
            }

            return false;
        }
        private static void OpenMediaFile(WPFSCPlayerPro player, String filename)
        {
            if (IsVideoFile(filename))
                player.Open(MediaType.Link);
            else if (IsImageFile(filename))
                player.Source = new BitmapImage(new Uri(filename, UriKind.RelativeOrAbsolute));
            else
                Log.Error($"打开文件 {filename} 失败，不支持的文件类型");
        }
        #endregion


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            Console.WriteLine(button.Name);
        }
    }
}
