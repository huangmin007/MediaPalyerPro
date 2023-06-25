using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Sttplay.MediaPlayer;
using System.Windows.Controls;
using System.Xml;
using System.Xaml;
using SpaceCG.Generic;
using SpaceCG.Extensions;

namespace MediaPlayerPro
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public static readonly LoggerTrace Log = new LoggerTrace(nameof(MainWindow));
        private readonly string MEDIA_CONFIG_FILE = "MediaContents.Config";

        internal const string FOREGROUND = "Foreground";
        internal const string BACKGROUND = "Background";
        internal const string CENTER = "Center";
        internal const string PLAYER = "Player";
        internal const string BUTTON = "Button";
        internal const string BUTTONS = "Buttons";
        internal const string CONTAINER = "Container";

        private XElement RootConfiguration = null;
        private IEnumerable<XElement> ItemElements;

        private XElement CurrentItem = null;
        private int CurrentItemID = int.MinValue;

        public Boolean ListAutoLoop { get; set; } = false;

        private Process ProcessModule;
        private MainWindow Window;
        private LoggerWindow LoggerWindow;

        /// <summary> 元素名称 </summary>
        private static List<String> FrameworkElements = new List<String>();
        /// <summary> 元素禁用属性 </summary>
        private static List<String> DisableAttributes = new List<string>() { "Name", "Content" };

        /// <summary> 当前播放器 </summary>
        private WPFSCPlayerPro CurrentPlayer;
        /// <summary> 控制接口 </summary>
        private ControlInterface ControlInterface;

        XmlParserContext xmlParserContext;
        XmlReaderSettings xmlReaderSettings;
        XamlXmlReaderSettings xamlXmlReaderSettings;
        private Stopwatch stopwatch = new Stopwatch();

        public MainWindow()
        {
            InitializeComponent();
            SetInstancePropertyValues(this, "Window.");
            this.Title = "Meida Player Pro v1.0.20230620";
            //this.Title = "Meida Player Pro " + (!String.IsNullOrWhiteSpace(this.Title) ? $"({this.Title})" : "");

            LoggerWindow = new LoggerWindow();
            ProcessModule = CreateProcessModule("Process.FileName");

            this.RootContainer.Width = this.Width;
            this.RootContainer.Height = this.Height;
            foreach (FrameworkElement child in LogicalTreeHelper.GetChildren(RootContainer))
            {
                FrameworkElements.Add(child.Name);
                child.Width = this.Width;
                child.Height = this.Height;
#if DEBUG
                this.Topmost = false;
                child.SetValue(ToolTipService.IsEnabledProperty, true);
#else
                child.SetValue(ToolTipService.IsEnabledProperty, false);
#endif
                Console.WriteLine($"FrameworkElement: {child.Name}({child})");
                foreach (FrameworkElement subChild in LogicalTreeHelper.GetChildren(child))
                {
                    FrameworkElements.Add(subChild.Name);
                    subChild.Width = this.Width;
                    subChild.Height = this.Height;
#if DEBUG
                    subChild.SetValue(ToolTipService.IsEnabledProperty, true);
#else
                    subChild.SetValue(ToolTipService.IsEnabledProperty, false);
#endif
                    Console.WriteLine($"\tFrameworkElement: {subChild.Name}({subChild})");
                }
            }

            this.Window = this;
            ControlInterface = new ControlInterface(2023);
            ControlInterface.AccessObjects.Add("Window", this);

            InstanceExtensions.ConvertChangeTypeExtension = ConvertChangeTypeExtension;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            {
                xmlReaderSettings = new XmlReaderSettings
                {
                    IgnoreComments = true,
                    IgnoreWhitespace = true,
                    NameTable = new NameTable(),
                };

                XmlNamespaceManager xmlns = new XmlNamespaceManager(xmlReaderSettings.NameTable);
                xmlns.AddNamespace("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
                xmlns.AddNamespace("x", "http://schemas.microsoft.com/winfx/2006/xaml");
                xmlns.AddNamespace("d", "http://schemas.microsoft.com/expression/blend/2008");
                xmlns.AddNamespace("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
                xmlns.AddNamespace("local", "clr-namespace:MediaPalyerPro");
                xmlns.AddNamespace("sttplay", "clr-namespace:Sttplay.MediaPlayer");
                xmlParserContext = new XmlParserContext(null, xmlns, "", XmlSpace.Preserve);
                xamlXmlReaderSettings = new XamlXmlReaderSettings();
                xamlXmlReaderSettings.LocalAssembly = Assembly.GetExecutingAssembly();
            }

            InitializeTimer();
            CreateNetworkSyncObject();

            //读取并播放列表文件
            LoadConfig(MEDIA_CONFIG_FILE);
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
                using (XmlReader reader = XmlReader.Create(fileName, xmlReaderSettings))
                {
                    RootConfiguration = XElement.Load(reader, LoadOptions.None);
                }

                CompatibleProcess(RootConfiguration);
                CheckAndUpdateElements(RootConfiguration);
                XElementExtensions.ReplaceTemplateElements(RootConfiguration, "Template", "RefTemplate", true);
                Console.WriteLine(RootConfiguration);
            }
            catch (Exception ex)
            {
                Log.Error($"读取 {fileName}错误, 文件格式错误：{ex}");
                if (MessageBox.Show($"退出程序？\r\n{ex.ToString()}", "配置文件格式错误", MessageBoxButton.OKCancel, MessageBoxImage.Error, MessageBoxResult.OK) == MessageBoxResult.OK)
                {
                    this.Close();
                    Application.Current.Shutdown(0);
                }
                return;
            }

            ItemElements = RootConfiguration.Elements("Item");
            if (bool.TryParse(RootConfiguration.Attribute("AutoLoop")?.Value, out bool listAutoLoop)) ListAutoLoop = listAutoLoop;
            if (int.TryParse(RootConfiguration.Attribute("DefaultID")?.Value, out int id)) LoadItem(id);
        }

        /// <summary>
        /// 加载指定项的内容
        /// </summary>
        /// <param name="id">指定 ID 属性值</param>
        public void LoadItem(int id)
        {
            if (ItemElements?.Count() <= 0) return;
            IEnumerable<XElement> items = from item in ItemElements
                                          where item.Attribute("ID")?.Value.Trim() == id.ToString()
                                          select item;
            if (items?.Count() != 1)
            {
                Log.Warn($"配置项列表中不存在指定的 ID: {id} 项");
                return;
            }

            Log.Info($"Ready Load Item ID: {id}");
            LoadItem(items.First());
        }
        /// <summary>
        /// 加载指定项的内容
        /// </summary>
        /// <param name="name">指定 Name 属性值</param>
        public void LoadItem(string name)
        {
            if (ItemElements?.Count() <= 0) return;
            IEnumerable<XElement> items = from item in ItemElements
                                          where item.Attribute("Name")?.Value.Trim() == name
                                          select item;
            if (items?.Count() != 1)
            {
                Log.Warn($"配置项列表中不存在指定的 Name: {name} 项");
                return;
            }

            Log.Info($"Ready Load Item Name: {name}");
            LoadItem(items.First());
        }
        /// <summary>
        /// 加载指定项内容
        /// <para>XAML 解析参考：https://www.codenong.com/54797577/ </para>
        /// </summary>
        /// <param name="item"></param>
        protected void LoadItem(XElement item)
        {
            if (item == null || !item.HasElements) return;

            CurrentItem = item;
            CurrentItemID = int.TryParse(CurrentItem.Attribute("ID")?.Value, out int value) ? value : int.MinValue;
            
            stopwatch.Restart();
            try
            {
                foreach (XElement element in item.Elements())
                {
                    if (element.Name.LocalName == "Action")
                    {
                        ControlInterface.TryParseControlMessage(element, out object returnResult);
                        continue;
                    }

                    if (FrameworkElements.IndexOf(element.Name.LocalName) == -1)
                    {
                        Log.Warn($"不支持的配置子项：{element}");
                        continue;
                    }

                    if (!InstanceExtensions.GetInstanceFieldValue(this, element.Name.LocalName, out object objectField) || objectField == null)
                    {
                        Log.Warn($"获取对象字段失败");
                        continue;
                    }

                    FrameworkElement uiElement = objectField as FrameworkElement;
                    if (uiElement == null)
                    {
                        Log.Warn($"实例窗体不存的字段对象 {objectField}");
                        continue;
                    }

                    if (uiElement.ToolTip != element)
                    {
                        //WPFSCPlayerPro.Close()
                        if (uiElement.GetType() == typeof(WPFSCPlayerPro) && uiElement.Name.IndexOf(PLAYER) != -1)
                        {
                            WPFSCPlayerPro WPFPlayer = (WPFSCPlayerPro)uiElement;
                            Console.WriteLine($"{WPFPlayer.Name} Source: {WPFPlayer.Source}  Url: {WPFPlayer.Url}");

                            if (IsVideoFile(WPFPlayer.Url)) WPFPlayer.Close();
                            WPFPlayer.Source = null;
                        }

                        //ToolTip
                        uiElement.ToolTip = element;
                        //FrameworkElement Property
                        InstanceExtensions.SetInstancePropertyValues(this, element);

                        //Buttons
                        if (uiElement.GetType() == typeof(Canvas) && uiElement.Name.IndexOf(BUTTONS) != -1 && element.Elements("Button")?.Count() > 0)
                        {
                            Panel PanelButtons = (Panel)uiElement;
                            //Clear
                            PanelButtons.Children.Clear();
                            //Add
                            foreach (XElement btnElement in element.Elements("Button"))
                            {
                                using (StringReader stringReader = new StringReader(btnElement.ToString()))
                                {
                                    using (XmlReader xmlReader = XmlReader.Create(stringReader, xmlReaderSettings, xmlParserContext))
                                    {
                                        using (XamlXmlReader xamlXmlReader = new XamlXmlReader(xmlReader, xamlXmlReaderSettings))
                                        {
                                            Button button = System.Windows.Markup.XamlReader.Load(xamlXmlReader) as Button;
                                            button.ToolTip = String.Format($"{CurrentItemID}.{PanelButtons.Name}.{button.Name}");
                                            PanelButtons.Children.Add(button);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    //Sub Element Actions
                    foreach (XElement action in element?.Elements("Action"))
                    {
                        ControlInterface.TryParseControlMessage(action, out object returnResult);
                    }
                }

                Console.WriteLine($"CurrentPlayer: {CurrentPlayer.Name} {CurrentPlayer.Url} {CurrentPlayer.OpenSuccessed}");
                //CurrentPlayer.Open() And Play()
                if (CurrentPlayer != null && IsVideoFile(CurrentPlayer.Url))
                {
                    if ((CurrentPlayer.AutoOpen || CurrentPlayer.OpenAndPlay) && !CurrentPlayer.OpenSuccessed)
                        CurrentPlayer.Open(MediaType.Link, null);
                    else
                        CurrentPlayer.Play();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"配置解析或是执行异常：{ex}");
                MessageBox.Show($"配置解析或是执行异常：\r\n{ex}", "Error", MessageBoxButton.OK);
            }

            stopwatch.Stop();
            Log.Info($"Load And Analyse Item XElement use {stopwatch.ElapsedMilliseconds} ms");
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
                    if (string.IsNullOrWhiteSpace(element.Parent.Attribute("Position")?.Value)) continue;
                    if (!double.TryParse(element.Parent.Attribute("Position").Value, out double position)) continue;

                    if (!(position <= currentTime && position > lastTime)) continue;

                    if (Log.IsDebugEnabled)
                        Log.Debug($"Render Frame Evnet CurrentTimer: {currentTime}");
                }

                if (element.Name.LocalName == "Action")
                {
                    ControlInterface.TryParseControlMessage(element, out object returnResult);
                }
                else
                {
                    InstanceExtensions.SetInstancePropertyValues(this, element);
                }
            }
        }
        
        /// <summary>
        /// 调用 指定项 => 指定按扭容器 => 指定的按扭 => 事件或属性
        /// </summary>
        /// <param name="itemID">页面ID</param>
        /// <param name="buttonContainer"></param>
        /// <param name="buttonName"></param>
        public void CallButtonEvent(int itemID, string buttonContainer, string buttonName)
        {
            stopwatch.Restart();
            IEnumerable<XElement> events = from item in ItemElements
                                           where item.Attribute("ID")?.Value.Trim() == itemID.ToString()
                                           from container in item.Elements(buttonContainer)
                                           from evt in container.Elements("Events")
                                           where evt.Attribute("Name")?.Value.Trim() == "Click" && evt.Attribute("Button")?.Value.Trim() == buttonName
                                           select evt;

            Log.Info($"CallButtonEvent: ItemID: {itemID}  LayerName:{buttonContainer}  ButtonName: {buttonName}  Count: {events?.Count()}");

            foreach (XElement element in events.Elements())
            {
                if (element.Name.LocalName == "Action")
                {
                    ControlInterface.TryParseControlMessage(element, out object returnResult);
                }
                else
                {
                    InstanceExtensions.SetInstancePropertyValues(this, element);
                }
            }

            stopwatch.Stop();
            Log.Info($"Call Button Events use {stopwatch.ElapsedMilliseconds} ms");
        }
        /// <summary>
        /// 调用 当前项(<see cref="CurrentItemID"/>) => 指定按扭容器 => 指定的按扭 => 事件或属性
        /// </summary>
        /// <param name="buttonContainer"></param>
        /// <param name="buttonName"></param>
        public void CallButtonEvent(string buttonContainer, string buttonName) => CallButtonEvent(CurrentItemID, buttonContainer, buttonName);
        /// <summary>
        /// 调用 当前项(<see cref="CurrentItemID"/>) => 当前最顶端的显示按扭容器 => 指定的按扭 => 事件或属性
        /// </summary>
        /// <param name="buttonName"></param>
        public void CallButtonEvent(string buttonName)
        {
            var btnContainer = ForegroundContainer.Visibility == Visibility.Visible && ForegroundButtons.Visibility == Visibility.Visible ? ForegroundButtons :
                               CenterContainer.Visibility == Visibility.Visible && CenterButtons.Visibility == Visibility.Visible ? CenterButtons :
                               BackgroundContainer.Visibility == Visibility.Visible && BackgroundButtons.Visibility == Visibility.Visible ? BackgroundButtons : null;

            CallButtonEvent(CurrentItemID, btnContainer.Name, buttonName);
        }
        /// <summary>
        /// Call Button Event
        /// </summary>
        /// <param name="button"></param>
        protected bool CallButtonEvent(Button button)
        {
            XElement buttonElements = (button.Parent as FrameworkElement)?.ToolTip as XElement;
            if (buttonElements == null) return false;

            IEnumerable<XElement> events = from evs in buttonElements.Elements("Events")
                                           where evs.Attribute("Name")?.Value == "Click" && evs.Attribute("Button")?.Value == button.Name
                                           select evs;
            if (events?.Count() <= 0) return false;

            foreach (XElement element in events?.Elements())
            {
                if (element.Name.LocalName == "Action")
                {
                    ControlInterface.TryParseControlMessage(element, out object returnResult);
                }
                else
                {
                    InstanceExtensions.SetInstancePropertyValues(this, element);
                }
            }
            return true;
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            Log.Info($"Click Button: {button.Name}  ToolTip: {button.ToolTip}");

            TimerReset();
            if (!CallButtonEvent(button) && button.ToolTip != null)
            {
                String[] tips = button.ToolTip.ToString().Split('.');
                CallButtonEvent(int.Parse(tips[0]), tips[1], tips[2]);
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
        private void UIElement_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Type type = sender.GetType();
            WPFSCPlayerPro player = null;
            FrameworkElement element = sender as FrameworkElement;
            Log.Info($"{type.Name} ({element.Name})  IsVisibleChanged: {element.IsVisible} ({element.Visibility})");

            if (type == typeof(WPFSCPlayerPro))
            {
                player = (WPFSCPlayerPro)sender;
            }
            else if (type == typeof(Grid))
            {
                Grid grid = (Grid)sender;
                string playerName = grid.Name.Replace(CONTAINER, PLAYER);
                player = this.FindName(playerName) as WPFSCPlayerPro;
            }

            if (player != null)
            {
                FrameworkElement parent = (FrameworkElement)player.Parent;
                if (parent.Visibility != Visibility.Visible || player.Visibility != Visibility.Visible)
                {
                    player.Pause();
                    player.SeekFastMilliSecond(0);
                }
            }

            CurrentPlayer = ForegroundContainer.Visibility == Visibility.Visible && ForegroundPlayer.Visibility == Visibility.Visible ? ForegroundPlayer :
                            CenterContainer.Visibility == Visibility.Visible && CenterPlayer.Visibility == Visibility.Visible ? CenterPlayer :
                            BackgroundContainer.Visibility == Visibility.Visible && BackgroundPlayer.Visibility == Visibility.Visible ? BackgroundPlayer : null;
        }

    }
}