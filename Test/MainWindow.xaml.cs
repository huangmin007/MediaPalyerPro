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

namespace Test
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        IModbusMaster master;

        public MainWindow()
        {
            InitializeComponent();
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //tcpClient = new TcpClient();
            //tcpClient.ConnectAsync("127.0.0.1", 8899);
            //tcpClientAdapter = new TcpClientAdapter(tcpClient);
            //master = ModbusSerialMaster.CreateRtu(new NModbus4TcpClientAdapter("127.0.0.1", 8899));
            //ModbusIpMaster.CreateIp(new NModbus4TcpClientAdapter("127.0.0.1", 8899));
            //master = ModbusSerialMaster.CreateRtu(tcpClient);
            
            
            master = ModbusSerialMaster.CreateRtu(new NModbus4SerialPortAdapter("com3", 115200));

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if(button == Button_Connect)
            {
                //master.WriteMultipleCoils(4, 0, new bool[] { true, false, true });
                master.WriteMultipleCoilsAsync(2, 0, new bool[] { true, false });
            }
            else if(button == Button_Close)
            {
                master.WriteMultipleCoilsAsync(2, 0, new bool[] { false, true });
            }
            else if(button == Button_Write)
            {
                master.WriteMultipleCoils(2, 0, new bool[] { true, false });
            }
        }
    }
}
