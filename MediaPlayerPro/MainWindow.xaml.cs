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
using System.Configuration;
using System.Windows.Media;
using SpaceCG.Extensions.Modbus;
using System.Windows.Interop;

namespace MediaPlayerPro
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public static readonly LoggerTrace Log = new LoggerTrace(nameof(MainWindow));
        private readonly string MEDIA_CONFIG_FILE = "MediaContents.Config";
        
        internal const string XEvent = "Event";
        internal const string XType = "Type";
        internal const string XAction = "Action";

        internal const string FOREGROUND = "Foreground";
        internal const string BACKGROUND = "Background";
        internal const string MIDDLE = "Middle";
        internal const string PLAYER = "Player";
        internal const string BUTTON = "Button";
        internal const string BUTTONS = "Buttons";
        internal const string CONTAINER = "Container";

        /// <summary> 元素名称 </summary>
        internal static List<String> FrameworkElements = new List<String>();
        /// <summary> 元素禁用属性 </summary>
        internal static List<String> DisableAttributes = new List<string>() { "Name", "Content" };

        private XElement RootConfiguration = null;
        protected IEnumerable<XElement> ItemElements;
        protected Boolean ListAutoLoop { get; set; } = false;
        protected XElement Settings { get; private set; } = null;
        protected XElement CurrentItem { get; private set; } = null;

        /// <summary>
        /// 当前 Item 的 ID
        /// </summary>
        public int CurrentItemID { get; private set; } = int.MinValue;

        private Process ProcessModule;
        private MainWindow Window;
        private LoggerWindow LoggerWindow;

        /// <summary> 当前播放器 </summary>
        private WPFSCPlayerPro CurrentPlayer;
        /// <summary> 控制接口 </summary>
        private ReflectionController ControlInterface;

        private XmlParserContext xmlParserContext;
        private XmlReaderSettings xmlReaderSettings;
        private XamlXmlReaderSettings xamlXmlReaderSettings;
        private Stopwatch stopwatch = new Stopwatch();
        private HwndSource hwndSource;

        public MainWindow()
        {
            InitializeComponent();
            MainWindowExtensions.SetInstancePropertyValues(this, "Window.");
            ushort localPort = ushort.TryParse(ConfigurationManager.AppSettings["Interface.LocalPort"], out ushort port) ? port : (ushort)2023;

            this.Window = this;
            this.Title = "Meida Player Pro v1.2.20230620"; 
            LoggerWindow = new LoggerWindow();
            ControlInterface = new ReflectionController(localPort);
            ControlInterface.AccessObjects.Add("Window", this.Window);
            ControlInterface.MethodFilters.Add("*.ReleaseCore");
            TypeExtensions.CustomConvertFromExtension = ConvertFromExtension;

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

                    if(subChild.GetType() == typeof(WPFSCPlayerPro))
                    {
                        ControlInterface.AccessObjects.Add(subChild.Name, subChild);
                    }
                }
            }

            FrameworkElements.Add(RootContainer.Name);
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

            LoadConfig(MEDIA_CONFIG_FILE);
            ProcessModule = MainWindowExtensions.CreateProcessModule("Process.FileName");

            hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            hwndSource.DpiChanged += (s, ev) => { ev.Handled = true; };
        }

        /// <summary>
        /// 加载配置文件
        /// </summary>
        /// <param name="fileName"></param>
        public void LoadConfig(String fileName)
        {
            if (!File.Exists(fileName))
            {
                Log.Error($"配置文件 {MEDIA_CONFIG_FILE} 不存在");
                if (MessageBox.Show($"配置文件 {MEDIA_CONFIG_FILE} 不存在!{Environment.NewLine}退出程序？", "文件错误", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK) == MessageBoxResult.OK)
                {
                    this.Close();
                    Application.Current.Shutdown(0);
                }
                return;
            }

            try
            {
                using (XmlReader reader = XmlReader.Create(fileName, xmlReaderSettings))
                {
                    RootConfiguration = XElement.Load(reader, LoadOptions.None);
                }

                MainWindowExtensions.CompatibleProcess(RootConfiguration);
                MainWindowExtensions.CheckAndUpdateElements(RootConfiguration);
                XElementExtensions.ReplaceTemplateElements(RootConfiguration, "Template", "RefTemplate", true);
                //Console.WriteLine(RootConfiguration);
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

            Settings = RootConfiguration.Element("Settings");
            if (Settings != null)
            {
                XElement timerElement = Settings.Element("Timer");
                if (timerElement != null) InitializeTimer(timerElement);

                XElement syncElement = Settings.Element("Synchronize");
                if (syncElement != null) InitializeNetworkSync(syncElement);
            }

            ConnectionManagement.Dispose();
            XElement Connections = RootConfiguration.Element(ConnectionManagement.XConnections);
            if (Connections != null)
            {
                ConnectionManagement.Instance.Configuration(ControlInterface, Connections.Attribute(ReflectionController.XName)?.Value);
                ConnectionManagement.Instance.TryParseElements(Connections.Descendants(ConnectionManagement.XConnection));
            }            

            ModbusDeviceManagement.Dispose();
            IEnumerable<XElement> ModbusElements = RootConfiguration.Descendants(ModbusTransport.XModbus);
            if (ModbusElements?.Count() > 0)
            {
                ModbusDeviceManagement.Instance.Configuration(ControlInterface, RootConfiguration.Attribute(nameof(ModbusDeviceManagement))?.Value);
                ModbusDeviceManagement.Instance.TryParseElements(ModbusElements);
            }
            
            ItemElements = RootConfiguration.Descendants("Item");
            if (bool.TryParse(RootConfiguration.Attribute("AutoLoop")?.Value, out bool listAutoLoop)) ListAutoLoop = listAutoLoop;
            if (int.TryParse(RootConfiguration.Attribute("DefaultID")?.Value, out int id)) LoadItem(id);
        }

        /// <summary>
        /// 加载指定项内容
        /// <para>XAML 解析参考：https://www.codenong.com/54797577/ </para>
        /// </summary>
        /// <param name="item"></param>
        protected void LoadItem(XElement item)
        {
            if (item == null || !item.HasElements) return;

            stopwatch.Restart();
            CurrentItem = item;
            CurrentItemID = int.TryParse(CurrentItem.Attribute("ID")?.Value, out int value) ? value : int.MinValue;

            try
            {
                foreach (XElement element in item.Elements())
                {
                    if (element.Name.LocalName == XAction)
                    {
                        ControlInterface.TryParseControlMessage(element);
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

                            if (MainWindowExtensions.IsVideoFile(WPFPlayer.Url)) WPFPlayer.Close();
                            WPFPlayer.Source = null;
                        }

                        //ToolTip
                        uiElement.ToolTip = element;
                        //FrameworkElement Property
                        InstanceExtensions.SetInstancePropertyValues(this, element);

                        //Buttons
                        if (uiElement.GetType() == typeof(Canvas) && uiElement.Name.IndexOf(BUTTONS) != -1 && element.Elements(BUTTON)?.Count() > 0)
                        {
                            Panel buttonContainer = (Panel)uiElement;
                            //Clear
                            buttonContainer.Children.Clear();
                            //Add
                            foreach (XElement btnElement in element.Elements(BUTTON))
                            {
                                using (StringReader stringReader = new StringReader(btnElement.ToString()))
                                {
                                    using (XmlReader xmlReader = XmlReader.Create(stringReader, xmlReaderSettings, xmlParserContext))
                                    {
                                        using (XamlXmlReader xamlXmlReader = new XamlXmlReader(xmlReader, xamlXmlReaderSettings))
                                        {
                                            Button button = System.Windows.Markup.XamlReader.Load(xamlXmlReader) as Button;
                                            button.ToolTip = String.Format($"{button.Name}.{buttonContainer.Name}.{CurrentItemID}");
                                            buttonContainer.Children.Add(button);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    //Sub Element Actions                    
                    foreach (XElement action in element?.Elements(XAction))
                    {
                        ControlInterface.TryParseControlMessage(action);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"配置解析或是执行异常：{ex}");
                MessageBox.Show($"配置解析或是执行异常：\r\n{ex}", "Error", MessageBoxButton.OK);
            }

            if (CurrentPlayer != null && MainWindowExtensions.IsVideoFile(CurrentPlayer.Url))
            {
                if ((CurrentPlayer.AutoOpen || CurrentPlayer.OpenAndPlay) && !CurrentPlayer.OpenSuccessed)
                    CurrentPlayer.Open(MediaType.Link, null);
                else
                    CurrentPlayer.Play();
            }

            stopwatch.Stop();
            Log.Info($"Load And Analyse Item XElement use {stopwatch.ElapsedMilliseconds} ms");

            if (CurrentPlayer != null)
                Log.Info($"CurrentPlayer Name: {CurrentPlayer.Name}  URL: {CurrentPlayer.Url}  OpenSuccessed: {CurrentPlayer.OpenSuccessed}");
            else
                Log.Warn($"CurrentPlayer is NULL.");

            GC.Collect();
        }

        /// <summary>
        /// Call Button Event
        /// </summary>
        /// <param name="button"></param>
        protected bool CallButtonEvent(Button button)
        {
            XElement buttonElements = (button.Parent as FrameworkElement)?.ToolTip as XElement;
            if (buttonElements == null) return false;

            IEnumerable<XElement> events = from evs in buttonElements.Elements(XEvent)
                                           where evs.Attribute(XType)?.Value == "Click" && evs.Attribute("Element")?.Value == button.Name
                                           select evs;
            if (events?.Count() <= 0) return false;

            CallEventElements(events);
            return true;
        }
        
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            Button button = (Button)sender;
            Log.Info($"Click Button: {button.Name}  ToolTip: {button.ToolTip}");

            if (!CallButtonEvent(button) && button.ToolTip != null)
            {
                String[] tips = button.ToolTip.ToString().Split('.');
                CallButtonEvent(tips[0], tips[1], tips[2]);
            }            
        }
        private void UIElement_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Console.WriteLine(e.Source);
            Console.WriteLine(e.OriginalSource);

            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
            {
                this.PlayPause();
                e.Handled = true;
            }
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
                            MiddleContainer.Visibility == Visibility.Visible && MiddlePlayer.Visibility == Visibility.Visible ? MiddlePlayer :
                            BackgroundContainer.Visibility == Visibility.Visible && BackgroundPlayer.Visibility == Visibility.Visible ? BackgroundPlayer : null;
        }

        /// <summary>
        /// 扩展类型转换
        /// </summary>
        /// <param name="value"></param>
        /// <param name="destinationType"></param>
        /// <param name="conversionValue"></param>
        /// <returns></returns>
        public static bool ConvertFromExtension(object value, Type destinationType, out object conversionValue)
        {
            conversionValue = null;
            
            return false;
        }

    }
}