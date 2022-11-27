﻿using System;
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
        private readonly string MEDIA_CONFIG_FILE = "MediaItems.Config";

        private XElement RootElement = null;
        private IEnumerable<XElement> ListItems;

        private XElement CurrentItem = null;
        private Boolean ListAutoLoop = false;

        private MainWindow Window;
        private Process ProcessModule;
        private LoggerWindow LoggerWindow;
        private ConcurrentDictionary<String, IDisposable> AccessObjects = new ConcurrentDictionary<string, IDisposable>();

        public MainWindow()
        {
            this.Window = this;
            InitializeComponent();
            LoggerWindow = new LoggerWindow();
            
            InstanceExtension.ChangeInstancePropertyValue(this, "Window.");
            ProcessModule = InstanceExtension.CreateProcessModule("Process.FileName");

#if DEBUG
            //System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Critical;
            //int tier = RenderCapability.Tier >> 16;
            //Console.WriteLine("Tier:{0}", tier);

            this.Topmost = false;
            ((log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository()).Root.Level = log4net.Core.Level.Debug;
#endif

        }

        #region Inherit Functions
        /// <inheritdoc/>
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            Player.Close();
            Background.Close();
            LoggerWindow.Close();

            InstanceExtension.DisposeAccessObjects(AccessObjects);

            InstanceExtension.DisposeNetworkClient(ref UDPClientSync);
            InstanceExtension.DisposeNetworkServer(ref UDPServerSync);

            InstanceExtension.RemoveInstanceEvents(Player);
            InstanceExtension.DisposeProcessModule(ref ProcessModule);
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
                        //Process.Start(Process.GetCurrentProcess().MainModule.FileName);
                        //Application.Current.Shutdown();
                        LoadConfig(MEDIA_CONFIG_FILE);
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
                    if (Player.Visibility == Visibility.Visible)
                    {
                        if (Player.IsPaused)
                            Player.Play();
                        else
                            Player.Pause();
                        return;
                    }
                    if(Background.Visibility == Visibility.Visible)
                    {
                        if (Background.IsPaused)
                            Background.Play();
                        else
                            Background.Pause();
                    }
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
            Modbus.Device.IModbusMaster ModbusDIO = InstanceExtension.CreateNModbus4Master("Modbus.Master.DIO");
            if (ModbusDIO != null) AccessObjects.TryAdd("Modbus.Master.DIO", ModbusDIO);
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
            return HandleResult.Ok;
        }
        private HandleResult OnServerReceiveEventHandler(IServer sender, IntPtr connId, byte[] data)
        {
            String message = Encoding.UTF8.GetString(data);
            Log.Info($"Receive Data: {message}");

            TimerRestart();
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
                RootElement = XElement.Load(fileName);
                ListItems = RootElement.Elements("Item");
            }
            catch (Exception ex)
            {
                Log.Error($"读取 {fileName} 文件错误：{ex}");
                return;
            }

            XAttribute autoLoop = RootElement?.Attribute("AutoLoop");
            XAttribute defaultId = RootElement?.Attribute("DefaultID");

            bool.TryParse(autoLoop?.Value, out ListAutoLoop);
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
                                          where attribute?.Name == "ID" && attribute?.Value == id.ToString()
                                          select element;

            Log.Info($"Load Config Item ID: {id}, Count: {items.Count()}");
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
        private void LoadItem(XElement item)
        {
            if (item == null) return;

            TimerRestart();
            CurrentItem = item;

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

            foreach (FrameworkElement uiElement in PlayerButtons.Children)
                PlayerButtons.UnregisterName(uiElement.Name);
            foreach (FrameworkElement uiElement in BackgroundButtons.Children)
                BackgroundButtons.UnregisterName(uiElement.Name);

            Player.Close();
            Background.Close();
            PlayerButtons.Children.Clear();
            BackgroundButtons.Children.Clear();

            try
            {
                foreach (XElement element in item.Elements())
                {
                    if(element.Name.LocalName == "Action")
                    {
                        this.CallActionElement(element);
                        continue;
                    }

                    InstanceExtension.ChangeInstancePropertyValue(this, element);

                    //Buttons
                    object uiElement = InstanceExtension.GetInstanceFieldObject(this, element.Name.LocalName);
                    if (element.Elements("Button")?.Count() > 0 && uiElement?.GetType() == typeof(Canvas))
                    {
                        Canvas canvas = (Canvas)uiElement;
                        foreach (XElement btnElement in element.Elements("Button"))
                        {
                            StringReader stringReader = new StringReader(btnElement.ToString());
                            XmlReader xmlReader = XmlReader.Create(stringReader, settings, context);
                            XamlXmlReader xamlXmlReader = new XamlXmlReader(xmlReader, xamlXmlReaderSettings);
                            Button button = (Button)System.Windows.Markup.XamlReader.Load(xamlXmlReader);
                            canvas.RegisterName(button.Name, button);
                            canvas.Children.Add(button);
                        }
                    }

                    //Actions
                    foreach (XElement action in element?.Elements("Action"))
                    {
                        this.CallActionElement(action);
                    }
                }
            }
            catch(Exception ex)
            {
                Log.Error($"加载配置项错误：{ex}");
            }
        }

        /// <summary>
        /// 执行配置事件
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="currentTime"></param>
        /// <param name="lastTime"></param>
        private void CallPlayerEvent(String eventName, double currentTime = -1.0f, double lastTime = -1.0f)
        {
            IEnumerable<XElement> events = from evs in CurrentItem?.Element(nameof(Player))?.Elements("Events")
                                           where evs.Attribute("Name")?.Value == eventName
                                           select evs;

            if (events?.Count() == 0) return;

            foreach (XElement element in events.Elements())
            {
                if (element.Name.LocalName == "Action")
                {
                    if (currentTime >= 0 && lastTime >= 0)
                    {
                        if (String.IsNullOrWhiteSpace(element.Parent.Attribute("Position")?.Value)) continue;
                        if (!double.TryParse(element.Parent.Attribute("Position").Value, out double position)) continue;

                        if (!(position <= currentTime && position > lastTime)) continue;
                    }

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
        public void CallButtonEvent(Button button)
        {
            String name = button.Name;
            String parent = button.Parent.GetValue(NameProperty).ToString();

            IEnumerable<XElement> events = from evs in CurrentItem?.Element(parent)?.Elements("Events")
                                           where evs.Attribute("Name")?.Value == "Click" &&
                                           (String.IsNullOrWhiteSpace(evs.Attribute("Button")?.Value) || evs.Attribute("Button")?.Value == button.Name)
                                           select evs;

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
        /// Call Button Event
        /// </summary>
        /// <param name="btnName"></param>
        public void CallButtonEvent(String btnName)
        {
            object obj = this.FindName(btnName);
            if (obj == null || obj.GetType() != typeof(Button)) return;

            this.CallButtonEvent((Button)obj);
        }
        /// <summary>
        /// Call指定项的按扭事件
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="btnName"></param>
        public void CallButtonEvent(int itemId, String btnName)
        {
            IEnumerable<XElement> events = from item in ListItems
                                           where item.Attribute("ID")?.Value == itemId.ToString()
                                           from element in item.Elements()
                                           where element.Name.LocalName.IndexOf("Buttons") > 0
                                           from evElement in element.Elements("Events")
                                           where evElement.Attribute("Name")?.Value == "Click" && evElement.Attribute("Button")?.Value == btnName
                                           select evElement;

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
        /// Call Action XElement
        /// </summary>
        /// <param name="action"></param>
        private void CallActionElement(XElement action)
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
        private double LastTime = 0.0f;
        private IEnumerable<XElement> onRenderEvents;

        private void OnCaptureOpenCallbackEvent(CaptureOpenResult result, string message, OpenCallbackContext context)
        {
            if (result != CaptureOpenResult.SUCCESS) return;

            //PlayerVideoInfo();
            onRenderEvents = null;

            if (CurrentItem?.Element(nameof(Player)) != null)
            {
                IEnumerable<XElement> events = CurrentItem.Element(nameof(Player))?.Elements("Events");
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
            CheckNetworkSyncStatus();

            if (onRenderEvents != null)
            {
                double currentTime = Math.Round(Player.CurrentTime / 1000.0f, 2);
                if (LastTime == currentTime) return;

                CallPlayerEvent("OnRenderFrame", currentTime, LastTime);
                LastTime = currentTime;
            }
        }
        private void OnFirstFrameRenderEventHandler(Sttplay.MediaPlayer.SCFrame obj)
        {
            if (Log.IsDebugEnabled)
                Log.Debug($"First Frame Render Evnet. {Player.Url}");

            if (UDPClientSync != null)  //4字节心跳
                UDPClientSync.Send(SyncMessage, SyncMessage.Length - 4, 4);

            CallPlayerEvent("OnFirstFrame");
            LastTime = Math.Round(Player.CurrentTime / 1000.0f, 2);
        }
        private void OnStreamFinishedEventHandler()
        {
            if (Log.IsDebugEnabled)
                Log.Debug($"Stream Finish Event. {Player.Url}");

            if (UDPClientSync != null)  //4字节心跳
                UDPClientSync.Send(SyncMessage, SyncMessage.Length - 4, 4);

            CallPlayerEvent("OnLastFrame");

            if (ListAutoLoop)
            {
                if (CurrentItem.NextNode != null)
                    LoadItem((XElement)CurrentItem.NextNode);
                else
                    LoadItem((XElement)CurrentItem.Parent.FirstNode);
            }
        }

        private void WPFSCPlayerPro_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!this.IsLoaded) return;
            WPFSCPlayerPro player = (WPFSCPlayerPro)sender;
            Log.Info($"WPFSCPlayerPro({player.Name}) IsVisibleChanged: {player.Visibility}");

            if (player.Visibility != Visibility.Visible)
            {
                player.Pause();
            }
        }

        #endregion

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            Log.Info($"Click Button: {button.Name}");

            TimerRestart();
            CallButtonEvent(button.Name);
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            TimerRestart();
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

        
    }
}
