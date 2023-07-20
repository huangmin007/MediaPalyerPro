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

        internal const string PLAYER = "Player";
        internal const string BUTTON = "Button";

        /*
        internal const string FOREGROUND = "Foreground";
        internal const string BACKGROUND = "Background";
        internal const string MIDDLE = "Middle";
        
        internal const string BUTTONS = "Buttons";
        internal const string CONTAINER = "Container";
        */

        /// <summary> 可访问的元素对象的名称 </summary>
        internal static List<string> FrameworkElements = new List<string>();
        /// <summary> 元素禁用属性 </summary>
        internal static List<string> DisableAttributes = new List<string>() { "Name", "Content" };

        private XElement RootConfiguration = null;
        protected IEnumerable<XElement> ItemElements;
        protected Boolean ListAutoLoop { get; set; } = false;
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
            if(string.IsNullOrWhiteSpace(this.Title)) this.Title = "Meida Player Pro";
            ProcessModule processModule = Process.GetCurrentProcess().MainModule;
            if (processModule != null && !string.IsNullOrWhiteSpace(processModule.FileVersionInfo.FileVersion))
            {
                Title = $"{Title} (v{processModule.FileVersionInfo.FileVersion})";
            }
            
            ControlInterface = new ReflectionController(localPort);
            ControlInterface.AccessObjects.Add("Window", this.Window);
            ControlInterface.MethodFilters.Add("*.ReleaseCore");
            ControlInterface.PropertyFilters.Add("*.Content");
            TypeExtensions.CustomConvertFromExtension = ConvertFromExtension;
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

                this.RootContainer.Width = this.Width;
                this.RootContainer.Height = this.Height;
                FrameworkElements.Add(this.RootContainer.Name);
                foreach (FrameworkElement child in LogicalTreeHelper.GetChildren(RootContainer))
                {
                    child.Width = this.Width;
                    child.Height = this.Height;
#if DEBUG
                    this.Topmost = false;
                    child.SetValue(ToolTipService.IsEnabledProperty, true);
#else
                    child.SetValue(ToolTipService.IsEnabledProperty, false);
#endif
                    if (child.GetType() == typeof(WPFSCPlayerPro))
                    {
                        //Default Setting
                        WPFSCPlayerPro player = (WPFSCPlayerPro)child;
                        InitializePlayer(player);
                        ControlInterface.AccessObjects.Add(player.Name, player);
                    }

                    FrameworkElements.Add(child.Name);
                }
            }

            LoadConfig(MEDIA_CONFIG_FILE);
            ProcessModule = MainWindowExtensions.CreateProcessModule("Process.FileName");

            hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            hwndSource.DpiChanged += (s, ev) => { ev.Handled = true; };

#if false
            Console.WriteLine("-----aaa");
            XmlDocument element = new XmlDocument();// (System.Windows.Markup.XamlWriter.Save(this));
            element.LoadXml(System.Windows.Markup.XamlWriter.Save(this));

            //XmlReader reader = XmlReader.Create(System.Windows.Markup.XamlWriter.Save(this));
            XmlNamespaceManager xmlNamespaceManager2 = new XmlNamespaceManager(element.NameTable);
            Console.WriteLine(xmlNamespaceManager2.DefaultNamespace);
#endif
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

            XElement Settings = RootConfiguration.Element("Settings");
            if (Settings != null)
            {
                XElement timerElement = Settings.Element("Timer");
                if (timerElement != null) InitializeTimer(timerElement);

                XElement syncElement = Settings.Element("Synchronize");
                if (syncElement != null) InitializeNetworkSync(syncElement);
            }

            ConnectionManagement.Instance.Disconnections();
            ConnectionManagement.Instance.ReflectionController = this.ControlInterface;
            XElement ConnectionsElement = RootConfiguration.Element(ConnectionManagement.XConnections);
            if (ConnectionsElement != null) ConnectionManagement.Instance.TryParseConnectionConfiguration(ConnectionsElement);

            XElement ItemsElement = RootConfiguration.Element("Items");
            ItemElements = ItemsElement?.Elements("Item");
            if (bool.TryParse(ItemsElement?.Attribute("ListAutoLoop")?.Value, out bool listAutoLoop)) ListAutoLoop = listAutoLoop;
            if (int.TryParse(ItemsElement?.Attribute("DefaultID")?.Value, out int id)) LoadItem(id);
        }

        /// <summary>
        /// 加载指定项内容
        /// <para>XAML 解析参考：https://www.codenong.com/54797577/ </para>
        /// </summary>
        /// <param name="item"></param>
        protected void LoadItem(XElement item)
        {
            if (ItemElements?.Count() <= 0) return;
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
                        Log.Warn($"获取对象字段失败, 不存在的对象 {element.Name.LocalName}");
                        continue;
                    }

                    FrameworkElement uiElement = objectField as FrameworkElement;
                    if (uiElement == null)
                    {
                        Log.Warn($"实例对象不存该字段对象 {objectField}");
                        continue;
                    }

                    if (uiElement.ToolTip != element)
                    {
                        //ToolTip
                        uiElement.ToolTip = element;
                        //FrameworkElement Property
                        InstanceExtensions.SetInstancePropertyValues(this, element);

                        //Buttons
                        if (uiElement.GetType() == typeof(Canvas) && uiElement.Name == nameof(ButtonContainer))
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

            stopwatch.Stop();
            Log.Info($"Load And Analyse Item XElement use {stopwatch.ElapsedMilliseconds} ms");

            if (CurrentPlayer != null)
                Log.Info($"CurrentPlayer Name: {CurrentPlayer.Name}  URL: {CurrentPlayer.Url}  OpenSuccessed: {CurrentPlayer.OpenSuccessed}");
            else
                Log.Warn($"CurrentPlayer is NULL.");

            GC.Collect();
        }

        protected void InitializePlayer(WPFSCPlayerPro player)
        {
            if (player == null) return;

            player.Volume = 0.8f;
            player.Loop = true;
            player.AutoOpen = true;
            player.OpenAndPlay = true;
            player.OpenMode = MediaType.Link;

            player.IsVisibleChanged += WPFSCPlayerPro_IsVisibleChanged;
            player.onCaptureOpenCallbackEvent += OnCaptureOpenCallbackEvent;
            player.onFirstFrameRenderEvent += OnFirstFrameRenderEventHandler;
            player.onRenderVideoFrameEvent += OnRenderVideoFrameEventHandler;
            player.onRenderAudioFrameEvent += OnRenderAudioFrameEventHandler;
            player.onStreamFinishedEvent += OnStreamFinishedEventHandler;
        }        
        private void WPFSCPlayerPro_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            WPFSCPlayerPro player = (WPFSCPlayerPro)sender;
            Log.Info($"{player.Name} IsVisibleChanged: {player.IsVisible} ({player.Visibility})");

            if (player.Visibility != Visibility.Visible)
            {
                player.Pause();
                player.SeekFastMilliSecond(0);
            }

            CurrentPlayer = ForegroundPlayer.Visibility == Visibility.Visible ? ForegroundPlayer :
                            MiddlePlayer.Visibility == Visibility.Visible ? MiddlePlayer :
                            BackgroundPlayer.Visibility == Visibility.Visible ? BackgroundPlayer : null;
        }
        private void UIElement_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Console.WriteLine($"UIElement_PreviewMouseDown：{e.Source}");

            if (e.Source is Button && e.ChangedButton == MouseButton.Left)
            {
                Button button = (Button)e.Source;
                Log.Info($"Click Button: {button.Name}  ToolTip: {button.ToolTip}");

                e.Handled = true;
                CallButtonEvent(button.Name);            
            }
            else if (e.Source is Canvas || e.Source is Grid)
            {
                if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
                {
                    this.PlayPause();
                    e.Handled = true;
                }
            }
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