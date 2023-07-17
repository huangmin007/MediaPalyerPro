﻿using System;
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
        private Timer timer;

        public MainWindow()
        {
            InitializeComponent();
        }

        Queue<int> queue = new Queue<int>(5);
        HwndSource hwndSource;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            BitmapImage bitmapImage = new BitmapImage(new Uri(@"D:\Desktop\big\IMG_ (8).jpg"));
            Image_Test.Source = bitmapImage;

            timer = new Timer(UpdateDisplay, this, 100, 30);
            Image_Test.RenderTransform = new TranslateTransform(0, 0);
            Image_Test.Clip = new RectangleGeometry(new Rect(0, 0, 300, 600), 0, 0);

            hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            hwndSource.DpiChanged += HwndSource_DpiChanged;
            //this.DpiChanged += MainWindow_DpiChanged;

            Color c = (Color)TypeDescriptor.GetConverter(typeof(Color)).ConvertFromString("#FF00FF00");
            this.Background = new SolidColorBrush(c);
            bool b = TypeDescriptor.GetConverter(typeof(WindowStyle)).IsValid("singleborderwindow");
            Console.WriteLine(b);

            byte i = (byte)TypeDescriptor.GetConverter(typeof(byte)).ConvertFromString("0x10");
            Console.WriteLine(i);

            //bool boo = (bool)TypeDescriptor.GetConverter(typeof(bool)).ConvertFrom(1);
            //Console.WriteLine(boo);

            queue.Enqueue(0);
            queue.Enqueue(1);
            Console.WriteLine(queue.Count);
            queue.Enqueue(2);
            queue.Enqueue(3);
            Console.WriteLine(queue.Count);
            queue.Enqueue(4);
            queue.Enqueue(5);
            Console.WriteLine(queue.Count);
        }

        private void MainWindow_DpiChanged(object sender, DpiChangedEventArgs e)
        {
            e.Handled = true;
            Console.WriteLine("win dpi chanage....");
        }

        private void HwndSource_DpiChanged(object sender, HwndDpiChangedEventArgs e)
        {
            Console.WriteLine("hwnd dpi chanage....");
            e.Handled = true;
        }

        int offset = 1;
        private void UpdateDisplay(object obj)
        {
            this.Dispatcher.Invoke(() =>
            {
                RectangleGeometry rg = Image_Test.Clip as RectangleGeometry;
                Rect rect = rg.Rect;

                TranslateTransform tt = Image_Test.RenderTransform as TranslateTransform;
                //Console.WriteLine($"TT.X: {tt.X}  Rect.X:{rect.X}  {Image_Test.ActualWidth}");
                if(tt.X >= rect.X) offset = -1;

                tt.X += offset;

                //(rg.Transform as TranslateTransform).X -= offset;

                rect.X -= offset;
                rg.Rect = rect;

                
            });
            
        }

        

        protected override void OnContentChanged(object oldContent, object newContent)
        {
            base.OnContentChanged(oldContent, newContent);
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {

        }
        

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if(button == Button_Connect)
            {
                Image_Test.RenderSize = new Size(500, 600);
                //EllipseGeometry ellipse = new EllipseGeometry(new Point(0, 0), 0, 0);                
                //Image_Test.Clip = new EllipseGeometry(new Point(0, 0), 0, 0, new TranslateTransform());
                Image_Test.Clip = new RectangleGeometry(new Rect(100, 100, 200, 400));
            }
            else if(button == Button_Close)
            {
                (Image_Test.Clip as RectangleGeometry).Rect = new Rect(200, 100, 200, 400);
            }
            else if(button == Button_Write)
            {
                Image_Test.RenderTransform = new TranslateTransform(200, 100);
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            Rect rect;
            switch(e.Key)
            {

                case Key.Q:
                    Image_Test.RenderTransform = new TranslateTransform(0, 0);
                    Image_Test.Clip = new RectangleGeometry(new Rect(100, 0, 300, 600));
                    break;

                case Key.A:
                    rect = (Image_Test.Clip as RectangleGeometry).Rect;
                    rect.X -= 1;
                    (Image_Test.Clip as RectangleGeometry).Rect = rect;
                    break;
                case Key.D:
                    rect = (Image_Test.Clip as RectangleGeometry).Rect;
                    rect.X += 1;
                    (Image_Test.Clip as RectangleGeometry).Rect = rect;
                    break;

                case Key.Z:
                    (Image_Test.RenderTransform as TranslateTransform).X -= 1;
                    rect = (Image_Test.Clip as RectangleGeometry).Rect;
                    rect.X += 1;
                    (Image_Test.Clip as RectangleGeometry).Rect = rect;
                    break;
                case Key.X:
                    (Image_Test.RenderTransform as TranslateTransform).X += 1;
                    //Console.WriteLine((Image_Test.RenderTransform as TranslateTransform).X);
                    rect = (Image_Test.Clip as RectangleGeometry).Rect;
                    rect.X -= 1;
                    //Console.WriteLine($"Rect::{rect.X}");
                    (Image_Test.Clip as RectangleGeometry).Rect = rect;
                    break;
            }
        }
    }
}
