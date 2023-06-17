using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

namespace MediaPalyerPro
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 设置播放器音量
        /// </summary>
        /// <param name="volume"></param>
        public void SetVolume(float volume)
        {
            MiddlePlayer.SetVolume(volume);
            BackgroundPlayer.SetVolume(volume);
            ForegroundPlayer.SetVolume(volume);
        }

        /// <summary>
        /// 音量增加 10%
        /// </summary>
        public void VolumeUp()
        {
            MiddlePlayer.VolumeUp();
            BackgroundPlayer.VolumeUp();
            ForegroundPlayer.VolumeUp();
        }
        /// <summary>
        /// 音量减小 10%
        /// </summary>
        public void VolumeDown()
        {
            MiddlePlayer.VolumeDown();
            BackgroundPlayer.VolumeDown();
            ForegroundPlayer.VolumeDown();
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
            if (ForegroundPlayer.Visibility == Visibility.Visible)
            {
                if (ForegroundPlayer.IsPaused)
                    ForegroundPlayer.Play();
                else
                    ForegroundPlayer.Pause();
                return;
            }
            if (MiddlePlayer.Visibility == Visibility.Visible)
            {
                if (MiddlePlayer.IsPaused)
                    MiddlePlayer.Play();
                else
                    MiddlePlayer.Pause();
                return;
            }
            if (BackgroundPlayer.Visibility == Visibility.Visible)
            {
                if (BackgroundPlayer.IsPaused)
                    BackgroundPlayer.Play();
                else
                    BackgroundPlayer.Pause();
                return;
            }
        }

        /// <summary>
        /// 播放视频
        /// </summary>
        public void Play()
        {
            if (ForegroundPlayer.Visibility == Visibility.Visible)
            {
                if (ForegroundPlayer.IsPaused) ForegroundPlayer.Play();
                return;
            }
            if (MiddlePlayer.Visibility == Visibility.Visible)
            {
                if (MiddlePlayer.IsPaused) MiddlePlayer.Play();
                return;
            }
            if (BackgroundPlayer.Visibility == Visibility.Visible)
            {
                if (BackgroundPlayer.IsPaused) BackgroundPlayer.Play();
                return;
            }
        }

        /// <summary>
        /// 暂停视频
        /// </summary>
        public void Pause()
        {
            if (ForegroundPlayer.Visibility == Visibility.Visible)
            {
                ForegroundPlayer.Pause();
                return;
            }
            if (MiddlePlayer.Visibility == Visibility.Visible)
            {
                MiddlePlayer.Pause();
                return;
            }
            if (BackgroundPlayer.Visibility == Visibility.Visible)
            {
                BackgroundPlayer.Pause();
                return;
            }
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

    }
}
