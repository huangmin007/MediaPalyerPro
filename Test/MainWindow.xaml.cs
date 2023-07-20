using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;
using System.Xml;
using System.Net.Sockets;
using System.Threading;
using Modbus.Device;
using System.Net;
using SpaceCG.Generic;
using System.Windows.Interop;
using System.ComponentModel;

namespace Test
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        static readonly LoggerTrace Logger = new LoggerTrace();

        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch(e.Key)
            {
                case Key.A:
                    Console.WriteLine("----");
                    Console.WriteLine(Image_Test.RenderSize);
                    Console.WriteLine(Image_Test.DesiredSize);
                    break;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Console.WriteLine(Image_Test.RenderSize);
            Console.WriteLine(Image_Test.DesiredSize);
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
           
        }

    }
}
