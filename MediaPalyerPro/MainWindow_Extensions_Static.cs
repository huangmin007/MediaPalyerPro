using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using SpaceCG.Extensions;
using Sttplay.MediaPlayer;

namespace MediaPalyerPro
{
    public partial class MainWindow : Window
    {
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
    }
}
