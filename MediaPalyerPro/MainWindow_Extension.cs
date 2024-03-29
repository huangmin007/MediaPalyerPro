﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

namespace MediaPalyerPro
{
    public partial class MainWindow : Window
    {

        /// <summary>
        /// 是否允许 Touch 功能
        /// </summary>
        /// <param name="enabled"></param>
        public void EnabledTouch(bool enabled)
        {
            RootContainer.IsEnabled = enabled;

            Task.Run(() =>
            {
                byte[] bytes = new byte[] { (byte)(enabled ? 0x01 : 0x00) };
                for (int i = 0; i < 3; i++)
                {
                    udpCast?.Send(bytes, bytes.Length);
                    Thread.Sleep(1000);
                }
            });
        }

        /// <summary>
        /// 设置播放器音量
        /// </summary>
        /// <param name="volume"></param>
        public void SetVolume(float volume)
        {
#if false
            MiddlePlayer.SetVolume(volume);
            BackgroundPlayer.SetVolume(volume);
            ForegroundPlayer.SetVolume(volume);
#else
            MiddlePlayer.Volume = volume;
            BackgroundPlayer.Volume = volume;
            ForegroundPlayer.Volume = volume;
#endif
        }

        /// <summary>
        /// 音量增加 10%
        /// </summary>
        public void VolumeUp()
        {
#if false
            MiddlePlayer.VolumeUp();
            BackgroundPlayer.VolumeUp();
            ForegroundPlayer.VolumeUp();
#else
            float volume = BackgroundPlayer.Volume;
            volume = (float)(volume + 0.1f >= 1.0f ? 1.0f : volume + 0.1f);
            
            MiddlePlayer.Volume = volume;
            BackgroundPlayer.Volume = volume;
            ForegroundPlayer.Volume = volume;
#endif
        }
        /// <summary>
        /// 音量减小 10%
        /// </summary>
        public void VolumeDown()
        {
#if false
            MiddlePlayer.VolumeDown();
            BackgroundPlayer.VolumeDown();
            ForegroundPlayer.VolumeDown();
#else
            float volume = BackgroundPlayer.Volume;
            volume = (float)(volume - 0.1f <= 0.0f ? 0.0f : volume - 0.1f);
            
            MiddlePlayer.Volume = volume;
            BackgroundPlayer.Volume = volume;
            ForegroundPlayer.Volume = volume;
#endif
        }

        /// <summary>
        /// 临近的 ID 项，下一个项
        /// </summary>
        public void NextItem()
        {
            if (CurrentItem == null) return;
            if (int.TryParse(CurrentItem.Attribute("ID")?.Value, out int id))
            {
                LoadItem(id + 1);
            }
        }
        /// <summary>
        /// 临近的 ID 项，上一个项
        /// </summary>
        public void PrevItem()
        {
            if (CurrentItem == null) return;
            if (int.TryParse(CurrentItem.Attribute("ID")?.Value, out int id))
            {
                LoadItem(id - 1);
            }
        }
        /// <summary>
        /// 下一个节点
        /// </summary>
        public void NextNode()
        {
            if (CurrentItem == null) return;

            if (CurrentItem.NextNode != null)
                LoadItem((XElement)CurrentItem.NextNode);
            else
                LoadItem((XElement)(CurrentItem.Parent.FirstNode));
        }
        /// <summary>
        /// 上一个节点
        /// </summary>
        public void PrevNode()
        {
            if (CurrentItem == null) return;
            if (CurrentItem.PreviousNode != null)
                LoadItem((XElement)CurrentItem.PreviousNode);
            else
                LoadItem((XElement)(CurrentItem.Parent.LastNode));
        }
        /// <summary>
        /// 播放暂停
        /// </summary>
        public void PlayPause()
        {
            if (CurrentPlayer == null) return;
            if (CurrentPlayer.IsPaused) CurrentPlayer.Play();
            else CurrentPlayer.Pause();
        }

        /// <summary>
        /// 播放视频
        /// </summary>
        public void Play()
        {
            if (CurrentPlayer == null) return;
            if (CurrentPlayer.IsPaused) CurrentPlayer.Play();
        }

        /// <summary>
        /// 暂停视频
        /// </summary>
        public void Pause()
        {
            if (CurrentPlayer == null) return;
            CurrentPlayer.Pause();
        }

        public void Sleep(int ms)
        {
            System.Threading.Thread.Sleep(ms);
        }

        public void TestEcho(string message = null)
        {
            if (String.IsNullOrWhiteSpace(message))
                Log.Info("This is test message, Hello World ...");
            else
                Log.Info(message);
        }

        /// <summary>
        /// 替换引用模板元素，引用模板元素名称为 $"Ref{templateElement.Name.LocalName}"
        /// </summary>
        /// <param name="templateElements">模板元素的集合</param>
        /// <param name="itemElements">需要替换的元素集合，其中应该包含引用模板元素</param>
        /// <param name="removeRefElement">是否移除引用模板元素，false 为保留</param>
        public static void ReplaceTemplateElements(IEnumerable<XElement> templateElements, IEnumerable<XElement> itemElements, bool removeRefElement = true)
        {
            if (templateElements?.Count() == 0) return;
            var refElementName = $"Ref{templateElements.First().Name.LocalName}";

            //模板中引用模板的处理
            var refTemplateCount = (from element in templateElements
                                    where element.Name.LocalName == refElementName
                                    select element)?.Count();
            if (refTemplateCount > 0) ReplaceTemplateElements(templateElements, null, true);
            var taretElements = itemElements?.Count() == 0 ? templateElements : itemElements;

            foreach (var element in taretElements)
            {
                var refTemplates = element.Descendants(refElementName);
                for (int i = 0; i < refTemplates?.Count(); i++)
                {
                    var refTemplate = refTemplates.ElementAt(i);
                    var refTemplateName = refTemplate.Attribute("Name")?.Value;
                    if (String.IsNullOrWhiteSpace(refTemplateName)) continue;

                    //在模板集合中查找指定名称的模板
                    var targetTemplates = from tmp in templateElements
                                          where refTemplate != tmp && refTemplateName == tmp.Attribute("Name")?.Value
                                          select tmp;
                    if (targetTemplates?.Count() != 1) continue;

                    var template = targetTemplates.First();
                    string templateString = template.ToString();

                    IEnumerable<XAttribute> attributes = refTemplate.Attributes();
                    foreach (var attribute in attributes)
                    {
                        if (attribute.Name == "Name") continue;
                        templateString = templateString.Replace($"{{{attribute.Name}}}", attribute.Value);
                    }

                    refTemplate.AddAfterSelf(XElement.Parse(templateString).Elements());
                    if (removeRefElement)
                    {
                        refTemplate.Remove();
                        i--;
                    }
                }
            }
        }

        /// <summary>
        /// 分析 XML 文档，处理模板与引用模板元素
        /// </summary>
        /// <param name="rootElement"> XML 元素或根元素</param>
        /// <param name="templateXName">模板的元素的名称</param>
        /// <param name="refTemplateXName">引用模板的元素名称</param>
        /// <param name="removeRefElements">是否移除引用模板元素，如果保留，则会重命名元素名称</param>
        public static void ReplaceTemplateElements(XElement rootElement, XName templateXName, XName refTemplateXName, bool removeRefElements = true)
        {
            if (rootElement == null || templateXName == null || refTemplateXName == null) return;

            //获取有效的模板元素集合
            var templates = from template in rootElement.Elements(templateXName)
                            let templateName = template.Attribute("Name")?.Value
                            where !String.IsNullOrWhiteSpace(templateName)
                            where !(from refTemplate in template.Descendants(refTemplateXName)
                                    where refTemplate.Attribute("Name")?.Value == templateName
                                    select true).Any()
                            select template;
            if (templates?.Count() <= 0) return;
            //Console.WriteLine($"Template Count: {templates.Count()}");

            //获取引用模板元素集合
            var refTemplates = rootElement.Descendants(refTemplateXName);
            if (refTemplates?.Count() <= 0) return;
            //Console.WriteLine($"Ref Template Count: {templates.Count()}");

            //Analyse Replace
            for (int i = 0; i < refTemplates?.Count(); i++)
            {
                var refTemplate = refTemplates.ElementAt(i);
                var refTemplateName = refTemplate.Attribute("Name")?.Value;
                if (String.IsNullOrWhiteSpace(refTemplateName)) continue;

                //在模板集合中查找指定名称的模板
                var temps = from template in templates
                            where refTemplateName == template.Attribute("Name")?.Value
                            select template;
                if (temps?.Count() <= 0) continue;

                //拷贝模板并更新属性值
                string templateString = temps.First().ToString();
                IEnumerable<XAttribute> attributes = refTemplate.Attributes();
                foreach (var attribute in attributes)
                {
                    if (attribute.Name != "Name")
                        templateString = templateString.Replace($"{{{attribute.Name}}}", attribute.Value);
                }

                refTemplate.AddAfterSelf(XElement.Parse(templateString).Elements());
                if (removeRefElements)
                    refTemplate.Remove();
                else
                    refTemplate.Name = $"{refTemplate.Name.LocalName}.Handle";
                i--;
            }
        }

    }
}
