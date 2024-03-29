﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using SpaceCG.Extensions;
using SpaceCG.Generic;
using Sttplay.MediaPlayer;

namespace MediaPlayerPro
{
    public static class MainWindowExtensions
    {
        static readonly LoggerTrace Log = new LoggerTrace(nameof(MainWindowExtensions));

        #region Static Functions
        /// <summary>
        /// 是否是音频或视频文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static bool IsVideoFile(String fileName)
        {
            if (String.IsNullOrWhiteSpace(fileName)) return false;

            FileInfo fileInfo = new FileInfo(fileName);
            if (!fileInfo.Exists) return false;
            String extension = fileInfo.Extension.ToUpper();

            switch (extension)
            {
                case ".MP3":    //音频格式
                case ".WAV":    //音频格式
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

        /// <summary>
        /// 是否图片类型文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 打开媒体文件
        /// </summary>
        /// <param name="player"></param>
        /// <param name="filename"></param>
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

        /// <summary>
        /// 跟据 配置文件 中读取对应实例的 key 属性值, 动态的设置实例对象的属性值
        /// <para>nameSpace 格式指：[Object.]Property , Object 可为空</para>
        /// </summary>
        /// <param name="instanceObj"></param>
        /// <param name="nameSpace"></param>
        public static void SetInstancePropertyValues(object instanceObj, String nameSpace)
        {
            if (instanceObj == null)
                throw new ArgumentNullException(nameof(instanceObj), "参数不能为空");

            if (String.IsNullOrWhiteSpace(nameSpace)) nameSpace = "";
            PropertyInfo[] properties = instanceObj.GetType().GetProperties();

            foreach (PropertyInfo property in properties)
            {
                if (!property.CanWrite || !property.CanRead) continue;

                String value = ConfigurationManager.AppSettings[$"{nameSpace}{property.Name}"];
                if (String.IsNullOrWhiteSpace(value)) continue;

                InstanceExtensions.SetInstancePropertyValue(instanceObj, property.Name, value);
            }
        }

        /// <summary>
        /// 启动配置文件中的应用程序或相关文档
        /// <para name="fileNameCfgKey">配置的键值格式：(命名空间.Process属性)，属性 FileName 不可为空。</para>
        /// <para>例如：Process.FileName, 多个进程操作，可以在命名空间上处理：Process1.FileName ... </para>
        /// </summary>
        /// <param name="fileNameCfgKey">配置的键值格式：(命名空间.Process属性)，例如：Process.FileName, 多个进程操作，可以在命名空间上处理：Process1.FileName ... </param>
        /// <returns></returns>
        public static Process CreateProcessModule(string fileNameCfgKey)
        {
            if (String.IsNullOrWhiteSpace(ConfigurationManager.AppSettings[fileNameCfgKey])) return null;

            String fileName = ConfigurationManager.AppSettings[fileNameCfgKey].Trim();
            FileInfo fileInfo = new FileInfo(fileName);
            if (!fileInfo.Exists)
            {
                Log.Warn($"应用程序或文档 \"{fileName}\" 不存在");
                return null;
            }

            String nameSpace = fileNameCfgKey.Replace("FileName", "");
            ProcessStartInfo startInfo = new ProcessStartInfo(fileInfo.FullName);
            startInfo.WorkingDirectory = fileInfo.DirectoryName;

            SetInstancePropertyValues(startInfo, nameSpace);

            //重复一次，转为使用绝对路径
            startInfo.FileName = fileInfo.FullName;

            Process process = new Process();
            process.StartInfo = startInfo;
            process.EnableRaisingEvents = true;
            process.Exited += (s, e) => Log.Warn($"应用程序或文档 \"{fileName}\" 发生退出事件(ExitCode:{process.ExitCode})");

            Task.Run(() =>
            {
                try
                {
                    if (process.Start())
                        Log.Info($"已启动的应用程序或文档 \"{fileName}\"");
                    else
                        Log.Warn($"应用程序或文档 \"{fileName}\" 启动失败");
                }
                catch (Exception ex)
                {
                    Log.Error($"应用程序或文档 \"{fileName}\" 启动时发生错误：{ex}");
                }
            });

            return process;
        }

        /// <summary>
        /// 退出并释放进程对象资源
        /// </summary>
        /// <param name="process"></param>
        public static void DisposeProcessModule(ref Process process)
        {
            if (process == null) return;

            int code = 0;
            String name = "";

            try
            {
                if (!process.HasExited)
                {
                    name = process.ProcessName;

                    process.Kill();
                    code = process.ExitCode;
                }

                process.Dispose();
                Log.Info($"退出并释放进程模块 {name}(ExitCode:{code}) 完成");
            }
            catch (Exception ex)
            {
                Log.Error($"退出并释放 进程模块资源 对象错误: {ex}");
            }
            finally
            {
                process = null;
            }
        }

        /// <summary>
        /// 检查更新元素，移除或是修改的属性的值，以保持格式的正确性
        /// </summary>
        /// <param name="rootElements"></param>
        public static void CheckAndUpdateElements(this XElement rootElements)
        {
            foreach (XElement element in rootElements.Descendants())
            {
                string localName = element.Name.LocalName;

                //0.替换 ImageBrush 节点 ImageSource 路径改为绝对路径
                if (localName == nameof(ImageBrush))
                {
                    XAttribute imageSource = element.Attribute("ImageSource");
                    imageSource.Value = Path.Combine(Environment.CurrentDirectory, imageSource?.Value);
                }
                //1.移除显示对象禁止访问的属性
                else if (MainWindow.FrameworkElements.IndexOf(localName) != -1)
                {
                    foreach (string xname in MainWindow.DisableAttributes)
                    {
                        var attribute = element.Attribute(xname);
                        if (attribute != null) attribute.Remove();
                    }
                }
            }
        }
#if false
        /// <summary>
        /// 兼容性处理函数
        /// </summary>
        /// <param name="rootElements"></param>
        [Obsolete("兼容性处理函数", true)]
        public static void CompatibleProcess(XElement rootElements)
        {
            //0.新旧元素节点名称的替换
            //key:oldElementNodeName, value:newElementNodeName
            Dictionary<string, string> OldElementName = new Dictionary<string, string>();
            OldElementName.Add("MiddleGroup", $"{MainWindow.MIDDLE}{MainWindow.CONTAINER}");
            OldElementName.Add("ForegroundGroup", $"{MainWindow.FOREGROUND}{MainWindow.CONTAINER}");
            OldElementName.Add("BackgroundGroup", $"{MainWindow.BACKGROUND}{MainWindow.CONTAINER}");
            foreach (var element in rootElements.Descendants())
            {
                string localName = element.Name.LocalName;

                //0.显示节点名称替换
                if (OldElementName.ContainsKey(localName))
                {
                    element.Name = OldElementName[localName];
                }
                //1.控制接口属性 Target
                if (localName == "Action")
                {
                    if (element.Attribute("Target") == null)
                    {
                        XAttribute xTarget = element.Attribute("TargetObj") != null ? element.Attribute("TargetObj") : element.Attribute("TargetKey");
                        if (xTarget == null) continue;
                        //替换旧的显示对象名称
                        string value = OldElementName.ContainsKey(xTarget.Value) ? OldElementName[xTarget.Value] : xTarget.Value;
                        element.Add(new XAttribute("Target", value));
                    }
                }
                else if (localName == "Events" && element.HasAttributes)
                {
                    element.Name = MainWindow.XEvent;
                    if (element.Attribute(MainWindow.XType) == null && element.Attribute("Name") != null)
                    {
                        element.Add(new XAttribute(MainWindow.XType, element.Attribute("Name").Value));
                    }
                }
            }
        }
#endif
        /// <summary>
        /// 重启计时器
        /// </summary>
        /// <param name="timer"></param>
        public static void Restart(this Timer timer)
        {
            if (timer == null) return;

            MainWindow.CurrentTickCount = 0;
            if (!timer.Enabled) timer.Start();
        }
        /// <summary>
        /// 扩展 <see cref="WPFSCPlayerPro.Open(MediaType, string)"/> 方法
        /// </summary>
        /// <param name="player"></param>
        public static void Open(this WPFSCPlayerPro player)
        {
            if (player == null) return;
            player.Open(player.OpenMode, null);
        }
        /// <summary>
        /// 扩展 <see cref="WPFSCPlayerPro.Open(MediaType, string)"/> 方法
        /// </summary>
        /// <param name="player"></param>
        public static void Open(this WPFSCPlayerPro player, string url)
        {
            if (player == null) return;
            player.Open(player.OpenMode, url);
        }
    }
}