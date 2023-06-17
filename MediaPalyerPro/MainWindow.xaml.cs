using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Sttplay.MediaPlayer;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Windows.Controls;
using System.Xml;
using System.Xaml;
using System.Text;
using HPSocket;
using SpaceCG.Generic;
using SpaceCG.Log4Net.Controls;

namespace MediaPalyerPro
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public static readonly log4net.ILog Log = log4net.LogManager.GetLogger(nameof(MainWindow));
        private readonly string MEDIA_CONFIG_FILE = "MediaContents.Config";

        private XElement RootElement = null;
        private IEnumerable<XElement> ListItems;

        private XElement CurrentItem = null;
        public Boolean ListAutoLoop { get; set; } = false;

        private MainWindow Window;
        private Process ProcessModule;
        private LoggerWindow LoggerWindow;
        private ConcurrentDictionary<String, IDisposable> AccessObjects = new ConcurrentDictionary<string, IDisposable>();

        public MainWindow()
        {
            this.Window = this;
            InitializeComponent();

            InstanceExtension.ChangeInstancePropertyValue(this, "Window.");
            this.Title = "Meida Player Pro " + (!String.IsNullOrWhiteSpace(this.Title) ? $"({this.Title})" : "");

            LoggerWindow = new LoggerWindow();
            ProcessModule = InstanceExtension.CreateProcessModule("Process.FileName");

#if DEBUG
            //System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Critical;
            //int tier = RenderCapability.Tier >> 16;
            //Console.WriteLine("Tier:{0}", tier);

            this.Topmost = false;
            ((log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository()).Root.Level = log4net.Core.Level.Debug;
#endif

            RootGroup.Width = this.Width;
            RootGroup.Height = this.Height;
        }

        #region Override Functions
        /// <inheritdoc/>
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            ForegroundPlayer.Pause();
            MiddlePlayer.Pause();
            BackgroundPlayer.Pause();

            InstanceExtension.DisposeAccessObjects(AccessObjects);

            InstanceExtension.DisposeNetworkClient(ref NetworkSlave);
            InstanceExtension.DisposeNetworkServer(ref NetworkMaster);
            InstanceExtension.DisposeProcessModule(ref ProcessModule);

            InstanceExtension.RemoveInstanceEvents(BackgroundPlayer);
            InstanceExtension.RemoveInstanceEvents(MiddlePlayer);
            InstanceExtension.RemoveInstanceEvents(ForegroundPlayer);

            Application.Current.Shutdown(0);
            LoggerWindow.Close(true);

            ForegroundPlayer.ReleaseCore();
            MiddlePlayer.ReleaseCore();
            BackgroundPlayer.ReleaseCore();
        }

        /// <inheritdoc/>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.IsRepeat) return;

            TimerReset();
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
                    break;

                case Key.D:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        log4net.Repository.Hierarchy.Logger root = ((log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository()).Root;
                        root.Level = (root.Level == log4net.Core.Level.Info) ? log4net.Core.Level.Debug : log4net.Core.Level.Info;
                        Log.Warn($"Root Logger Current Level: {root.Level}");

                        this.Topmost = false;
                        this.WindowState = WindowState.Normal;
                        this.WindowStyle = WindowStyle.SingleBorderWindow;
                    }
                    break;
                case Key.R:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        LoadConfig(MEDIA_CONFIG_FILE);
                        //Process.Start(Process.GetCurrentProcess().MainModule.FileName);
                        //Application.Current.Shutdown();
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
                    NextNode();
                    break;
                case Key.Up:
                case Key.Left:
                    PrevNode();
                    break;

                case Key.Space:
                case Key.Enter:
                    PlayPause();
                    break;

                case Key.Escape:
                    this.Close();
                    Application.Current.Shutdown(0);
                    //this.WindowState = WindowState.Normal;
                    //this.WindowStyle = WindowStyle.SingleBorderWindow;
                    break;
            }
        }
        #endregion

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //Create Instance
            Modbus.Device.IModbusMaster ModbusDIO = InstanceExtension.CreateNModbus4Master("Modbus.Master");
            if (ModbusDIO != null) AccessObjects.TryAdd("Modbus.Master", ModbusDIO);
            System.IO.Ports.SerialPort SerialPort = InstanceExtension.CreateSerialPort("SerialPort.PortName", null);
            if (SerialPort != null) AccessObjects.TryAdd("SerialPort", SerialPort);

            //Create Instance
            HPSocket.IServer NetworkServer = InstanceExtension.CreateNetworkServer("Network.Server", OnServerReceiveEventHandler);
            HPSocket.IClient NetworkClient = InstanceExtension.CreateNetworkClient("Network.Client", OnClientReceiveEventHandler);
            if (NetworkServer != null) AccessObjects.TryAdd("Network.Server", NetworkServer);
            if (NetworkClient != null) AccessObjects.TryAdd("Network.Client", NetworkClient);

            InitializeTimer();
            CreateNetworkSyncObject();

            //读取并播放列表文件
            LoadConfig(MEDIA_CONFIG_FILE);
        }

        private HandleResult OnClientReceiveEventHandler(IClient sender, byte[] data)
        {
            String message = Encoding.UTF8.GetString(data);
            Log.Info($"Client Receive Data: {message}");

            TimerReset();

            return HandleResult.Ok;
        }
        private HandleResult OnServerReceiveEventHandler(IServer sender, IntPtr connId, byte[] data)
        {
            String message = Encoding.UTF8.GetString(data);
            Log.Info($"Server Receive Data: {message}");

            TimerReset();
            XElement element = null;

            try
            {
                element = XElement.Parse(message);
            }
            catch (Exception ex)
            {
                Log.Error($"数据解析错误：{ex}");
                return HandleResult.Ok;
            }

            try
            {
                this.Dispatcher.Invoke(() =>
                {
                    if (element.Name.LocalName == "Action")
                        this.CallActionElement(element);
                    else
                        InstanceExtension.ChangeInstancePropertyValue(this, element);
                });
            }
            catch (Exception ex)
            {
                Log.Error($"数据执行错误：{ex}");
            }

            return HandleResult.Ok;
        }

        /// <summary>
        /// 加载配置文件
        /// </summary>
        /// <param name="fileName"></param>
        public void LoadConfig(String fileName)
        {
            if (!File.Exists(fileName)) return;

            try
            {
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.IgnoreComments = true;
                settings.IgnoreWhitespace = true;
                XmlReader reader = XmlReader.Create(fileName, settings);

                //RootElement = XElement.Load(fileName);
                RootElement = XElement.Load(reader, LoadOptions.None);
                ReplaceTemplateElements(RootElement.Elements("Template"), RootElement.Elements("Item"), true);

                ListItems = RootElement.Elements("Item");
            }
            catch (Exception ex)
            {
                Log.Error($"读取 {fileName} 文件错误：{ex}");
                return;
            }

            XAttribute autoLoop = RootElement?.Attribute("AutoLoop");
            XAttribute defaultId = RootElement?.Attribute("DefaultID");

            if (bool.TryParse(autoLoop?.Value, out bool listAutoLoop)) ListAutoLoop = listAutoLoop;
            if (int.TryParse(defaultId?.Value, out int id)) LoadItem(id);
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
                                          where attribute?.Name == "ID" && attribute?.Value.Trim() == id.ToString()
                                          select element;

            if (items?.Count() == 0) return;

            Log.Info($"Ready Load Config Item ID: {id}, Count: {items.Count()}");
            if (items.Count() != 1)
            {
                Log.Warn($"配置项列表中不存在指定的 ID 项, 或存在多个相同的 ID: {id} ");
                return;
            }

            LoadItem(items.ElementAt(0));
        }
        /// <summary>
        /// 播放列表项,,https://www.codenong.com/54797577/
        /// </summary>
        /// <param name="item"></param>
        protected void LoadItem(XElement item)
        {
            if (item == null) return;

            CurrentItem = item;

            //StringReader stringReader = new StringReader("");
            XmlReaderSettings settings = new XmlReaderSettings { NameTable = new NameTable() };
            XmlNamespaceManager xmlns = new XmlNamespaceManager(settings.NameTable);
            xmlns.AddNamespace("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            xmlns.AddNamespace("x", "http://schemas.microsoft.com/winfx/2006/xaml");
            xmlns.AddNamespace("d", "http://schemas.microsoft.com/expression/blend/2008");
            xmlns.AddNamespace("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            xmlns.AddNamespace("local", "clr-namespace:MediaPalyerPro");
            XmlParserContext context = new XmlParserContext(null, xmlns, "", XmlSpace.Preserve);
            //XmlReader xmlReader = XmlReader.Create(stringReader, settings, context);
            XamlXmlReaderSettings xamlXmlReaderSettings = new XamlXmlReaderSettings();
            xamlXmlReaderSettings.LocalAssembly = System.Reflection.Assembly.GetExecutingAssembly();
            //XamlXmlReader xamlXmlReader = new XamlXmlReader(xmlReader, xamlXmlReaderSettings);
            //UIElement element = (UIElement)System.Windows.Markup.XamlReader.Load(xamlXmlReader);

            MiddlePlayer.Pause();
            ForegroundPlayer.Pause();
            BackgroundPlayer.Pause();

            String id = CurrentItem.Attribute("ID")?.Value;

            try
            {
                foreach (XElement element in item.Elements())
                {
                    if(element.Name.LocalName == "Action")
                    {
                        this.CallActionElement(element);
                        continue;
                    }

                    FrameworkElement uiElement = (FrameworkElement)InstanceExtension.GetInstanceFieldObject(this, element.Name.LocalName);
                    //WPFSCPlayerPro.Close()
                    if (uiElement?.GetType() == typeof(WPFSCPlayerPro))
                    {
                        WPFSCPlayerPro WPFPlayer = (WPFSCPlayerPro)uiElement;
                        WPFPlayer.Source = null;
                        WPFPlayer.Close();
                    }

                    //FrameworkElement Property
                    InstanceExtension.ChangeInstancePropertyValue(this, element);

                    //CanvasButtons
                    //if (uiElement?.GetType() == typeof(Canvas) && element.Elements("Button")?.Count() > 0)
                    if (uiElement.Name.IndexOf("Buttons") != 0 && element.Elements("Button")?.Count() > 0)
                    {
                        Panel PanelButtons = (Panel)uiElement;
                        //Clear
                        PanelButtons.Children.Clear();
                        PanelButtons.ToolTip = id;
                        //Add
                        foreach (XElement btnElement in element.Elements("Button"))
                        {
                            XElement btnElementClone = XElement.Parse(btnElement.ToString());
#if true //改为相对路径
                            var imageBurshs = from sub in btnElementClone.Elements()
                                              where sub.Name.LocalName == "Button.Background" || sub.Name.LocalName == "Button.Foreground"
                                              select sub.Element("ImageBrush");

                            foreach (XElement imageBursh in imageBurshs)
                            {
                                if (imageBursh == null) continue;
                                XAttribute imageSource = imageBursh?.Attribute("ImageSource");
                                if (imageSource != null && imageSource.Value.Substring(1, 1) != ":")
                                    imageSource.Value = $"{Environment.CurrentDirectory }/{imageSource.Value}";
                            }
#endif
                            StringReader stringReader = new StringReader(btnElementClone.ToString());
                            XmlReader xmlReader = XmlReader.Create(stringReader, settings, context);
                            XamlXmlReader xamlXmlReader = new XamlXmlReader(xmlReader, xamlXmlReaderSettings);
                            Button button = (Button)System.Windows.Markup.XamlReader.Load(xamlXmlReader);
                            button.ToolTip = String.Format($"{id}.{PanelButtons.Name}.{button.Name}");
                            PanelButtons.Children.Add(button);

                            //if(imageBurshs.Count() > 0)
                            //{
                                //Console.WriteLine(item);
                            //}

                        }
                    }

                    //Sub Element Actions
                    foreach (XElement action in element?.Elements("Action"))
                    {
                        this.CallActionElement(action);
                    }

                    //WPFSCPlayerPro.Open()
                    if (uiElement?.GetType() == typeof(WPFSCPlayerPro))
                    {
                        WPFSCPlayerPro WPFPlayer = (WPFSCPlayerPro)uiElement;
                        if(WPFPlayer.AutoOpen) WPFPlayer.Open(MediaType.Link, null);
                    }
                }
            }
            catch(Exception ex)
            {
                Log.Error($"加载配置项错误：{ex}");
            }

            GC.Collect();
        }

        /// <summary>
        /// 执行配置事件
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="currentTime"></param>
        /// <param name="lastTime"></param>
        protected void CallPlayerEvent(WPFSCPlayerPro player, String eventName, double currentTime = -1.0f, double lastTime = -1.0f)
        {
            IEnumerable<XElement> events = from evs in CurrentItem?.Element(player.Name)?.Elements("Events")
                                           where evs.Attribute("Name")?.Value == eventName
                                           select evs;

            if (events?.Count() == 0) return;

            foreach (XElement element in events.Elements())
            {
                if (currentTime >= 0 && lastTime >= 0)
                {
                    if (String.IsNullOrWhiteSpace(element.Parent.Attribute("Position")?.Value)) continue;
                    if (!double.TryParse(element.Parent.Attribute("Position").Value, out double position)) continue;

                    if (!(position <= currentTime && position > lastTime)) continue;

                    if (Log.IsDebugEnabled)
                        Log.Debug($"Render Frame Evnet CurrentTimer: {currentTime}");
                }

                if (element.Name.LocalName == "Action")
                {
                    CallActionElement(element);
                }
                else
                {
                    InstanceExtension.ChangeInstancePropertyValue(this, element);
                }
            }
        }
        /// <summary>
        /// Call Button Event
        /// </summary>
        /// <param name="button"></param>
        protected void CallButtonEvent(Button button)
        {
            if (CurrentItem == null) return;

            String name = button.Name;
            String parent = button.Parent.GetValue(NameProperty).ToString();
            Log.Info($"CallButtonEvent: {parent}.{name}");
            if (CurrentItem.Element(parent) == null)
            {
                return;
            }
#if true
            IEnumerable<XElement> events = from evs in CurrentItem.Element(parent).Elements("Events")
                                           where evs.Attribute("Name")?.Value == "Click" &&
                                           (String.IsNullOrWhiteSpace(evs.Attribute("Button")?.Value) || evs.Attribute("Button")?.Value == name)
                                           select evs;
#else
            List<XElement> events = new List<XElement>();
            foreach (var evs in CurrentItem.Element(parent).Elements("Events"))
            {
                if(evs.Attribute("Name")?.Value == "Click" && 
                    (String.IsNullOrWhiteSpace(evs.Attribute("Button")?.Value) || evs.Attribute("Button")?.Value == name))
                {
                    events.Add(evs);
                }
            }
#endif
            foreach (XElement element in events?.Elements())
            {
                if (element.Name.LocalName == "Action")
                {
                    this.CallActionElement(element);
                }
                else
                {
                    InstanceExtension.ChangeInstancePropertyValue(this, element);
                }
            }
        }        
        /// <summary>
        /// Call指定项页面的按扭事件
        /// </summary>
        /// <param name="id">页面ID</param>
        /// <param name="layerName"></param>
        /// <param name="buttonName"></param>
        public void CallButtonEvent(int id, string layerName, string buttonName)
        {
            IEnumerable<XElement> events = from item in ListItems
                                           where item.Attribute("ID")?.Value.Trim() == id.ToString()
                                           from element in item.Elements()
                                           where element.Name.LocalName == layerName
                                           from evElement in element.Elements("Events")
                                           where evElement.Attribute("Name")?.Value?.Trim() == "Click" && evElement.Attribute("Button")?.Value?.Trim() == buttonName
                                           select evElement;

            Log.Info($"CallButtonEvent: ItemID: {id}  LayerName:{layerName}  ButtonName: {buttonName}  Count: {events?.Count()}");

            foreach (XElement element in events.Elements())
            {
                if (element.Name.LocalName == "Action")
                {
                    this.CallActionElement(element);
                }
                else
                {
                    InstanceExtension.ChangeInstancePropertyValue(this, element);
                }
            }
        }
        /// <summary>
        /// Call当前项或页面)的按扭事件
        /// </summary>
        /// <param name="layerName"></param>
        /// <param name="buttonName"></param>
        public void CallButtonEvent(string layerName, string buttonName) 
        {
            if (int.TryParse(CurrentItem.Attribute("ID")?.Value, out int id)) 
                CallButtonEvent(id, layerName, buttonName);
        }
        /// <summary>
        /// Call Action XElement
        /// </summary>
        /// <param name="action"></param>
        protected void CallActionElement(XElement action)
        {
            if (action?.Name?.LocalName != "Action") return;

            Object target = null;
            if (action.Attribute("TargetObj") != null)
                target = InstanceExtension.GetInstanceFieldObject(this, action?.Attribute("TargetObj")?.Value);
            else if (action.Attribute("TargetKey") != null)
                target = AccessObjects.TryGetValue(action?.Attribute("TargetKey")?.Value, out IDisposable obj) ? obj : null;
            if (target == null)
            {
                Log.Warn($"未找到配置的目标对象：{action}");
                return;
            }

            Log.Info($"准备执行目标对象配置: {action} ");

            try
            {
                //Method
                if (!String.IsNullOrWhiteSpace(action.Attribute("Method")?.Value))
                {
                    if(action.Attribute("Method").Value == "Sleep")
                    {
                        InstanceExtension.CallInstanceMethod(target, action.Attribute("Method").Value, StringExtension.ConvertParameters(action.Attribute("Params").Value));
                        return;
                    }

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
                    Log.Error($"配置格式错误：{action}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"执行目标对象配置错误：{ex}");
            }
        }


#region Player Events Handler
        //private double LastTime = 0.0f;
        //private IEnumerable<XElement> onRenderEvents;

        private Dictionary<String, double> playerLastTimer = new Dictionary<string, double>();
        private Dictionary<String, IEnumerable<XElement>> playerRenderEvents = new Dictionary<string, IEnumerable<XElement>>();

        private void OnCaptureOpenCallbackEvent(WPFSCPlayerPro player, CaptureOpenResult result, string message, OpenCallbackContext context)
        {
            if (Log.IsDebugEnabled)
                Log.Debug($"WPFSCPlayerPro({player.Name}) On Capture Open Callback Event, Result: {result}  Message: {message}");

            if (result != CaptureOpenResult.SUCCESS) return;

            if (!playerLastTimer.ContainsKey(player.Name))
                playerLastTimer.Add(player.Name, 0.0f);
            if(!playerRenderEvents.ContainsKey(player.Name))
                playerRenderEvents.Add(player.Name, null);

            playerRenderEvents[player.Name] = null;
            if (CurrentItem?.Element(player.Name) != null)
            {
                IEnumerable<XElement> events = CurrentItem.Element(player.Name)?.Elements("Events");
                if (events.Count() > 0)
                {
                    playerRenderEvents[player.Name] = from ev in events
                                                  where ev.Attribute("Name")?.Value == "OnRenderFrame"
                                                  select ev;
                    if (playerRenderEvents[player.Name].Count() == 0) playerRenderEvents[player.Name] = null;
                    //Console.WriteLine("COUNT>>>>>>>>");
                }
            }
        }
        private void OnRenderFrameEventHandler(WPFSCPlayerPro player, SCFrame frame)
        {
            CheckNetworkSyncStatus();

            if (playerRenderEvents[player.Name] != null)
            {
                double currentTime = Math.Round(player.CurrentTime / 1000.0f, 2);
                if (playerLastTimer[player.Name] == currentTime) return;

                CallPlayerEvent(player, "OnRenderFrame", currentTime, playerLastTimer[player.Name]);
                playerLastTimer[player.Name] = currentTime;
            }
        }
        private void OnFirstFrameRenderEventHandler(WPFSCPlayerPro player, SCFrame frame)
        {
            if (Log.IsDebugEnabled)
                Log.Debug($"WPFSCPlayerPro({player.Name}) First Frame Render Evnet. URL: {player.Url}");

            if (NetworkSlave != null)  //4字节心跳
                NetworkSlave.Send(SyncMessage, SyncMessage.Length - 4, 4);

            CallPlayerEvent(player, "OnFirstFrame");
            playerLastTimer[player.Name] = Math.Round(player.CurrentTime / 1000.0f, 2);
        }
        private void OnStreamFinishedEventHandler(WPFSCPlayerPro player)
        {
            if (Log.IsDebugEnabled)
                Log.Debug($"WPFSCPlayerPro({player.Name}) Stream Finish Event. URL: {player.Url}  ListAutoLoop: {ListAutoLoop}");

            if (NetworkSlave != null)  //4字节心跳
                NetworkSlave.Send(SyncMessage, SyncMessage.Length - 4, 4);

            CallPlayerEvent(player, "OnLastFrame");

            if (ListAutoLoop)
            {
                if (CurrentItem.NextNode != null)
                    LoadItem((XElement)CurrentItem.NextNode);
                else
                    LoadItem((XElement)CurrentItem.Parent.FirstNode);
            }
        }
        private void OnStatusChangeEvent(WPFSCPlayerPro player)
        {
            if (Log.IsDebugEnabled)
                Log.Debug($"WPFSCPlayerPro({player.Name}) Status Chagned Evnet. IsPaused: {player.IsPaused}");
            
            CheckNetworkSyncStatus();
        }
        #endregion
        private void GridGroup_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {

        }
        private void WPFSCPlayerPro_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!this.IsLoaded) return;

            WPFSCPlayerPro player = (WPFSCPlayerPro)sender;
            Log.Info($"WPFSCPlayerPro({player.Name})  IsVisibleChanged:{player.Visibility}  NewValue:{e.NewValue}");
            
            FrameworkElement parent = (FrameworkElement)player.Parent;
            if (player.Visibility != Visibility.Visible || parent.Visibility != Visibility.Visible)
            {
                player.Pause();
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            Log.Info($"Click Button: {button.Name}  ToolTip: {button.ToolTip}");

            TimerReset();
            if (button.ToolTip != null)
            {
                String[] tips = button.ToolTip.ToString().Split('.');
                CallButtonEvent(int.Parse(tips[0]), tips[1], tips[2]);
            }
            else
            {
                CallButtonEvent(button);
            }
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Log.IsDebugEnabled) Log.Debug($"Window Mouse Down.");
            TimerReset();
        }

        /// <summary>
        /// 打印 Player 属性信息
        /// </summary>
        public void PlayerVideoInfo()
        {
            Type type = ForegroundPlayer.GetType();
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (PropertyInfo property in properties)
            {
                Log.InfoFormat("{0}: {1}", property.Name, property.GetValue(ForegroundPlayer));
            }
        }

        private void WPFSCPlayerPro_MouseDown(object sender, MouseButtonEventArgs e)
        {
            WPFSCPlayerPro player = (WPFSCPlayerPro)sender;

            if (!IsVideoFile(player.Url)) return;

            if (player.IsPaused)
                player.Play();
            else
                player.Pause();
        }

        private void OnRenderAudioEvent(WPFSCPlayerPro player, IntPtr arg2, int arg3)
        {
            this.Dispatcher.Invoke(() =>
            {
                if (playerRenderEvents[player.Name] != null)
                {
                    double currentTime = Math.Round(player.CurrentTime / 1000.0f, 2);
                    if (playerLastTimer[player.Name] == currentTime) return;

                    CallPlayerEvent(player, "OnRenderFrame", currentTime, playerLastTimer[player.Name]);
                    playerLastTimer[player.Name] = currentTime;
                }
            });
            
        }
    }
}
