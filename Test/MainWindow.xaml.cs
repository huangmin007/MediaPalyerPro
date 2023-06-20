using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml.Linq;
using System.Xml;

namespace Test
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private XElement RootConfiguration = null;
        private IEnumerable<XElement> ListItems;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string path = @"E:\2020\005_Projects\2023_MediaPalyerPro\MediaPalyerPro\MediaPalyerPro\";

            LoadConfig($"{path}\\MediaContents.Page.Config");
        }

        public void LoadConfig(string fileName)
        {
            Console.WriteLine(fileName);
            if (!File.Exists(fileName)) 
                throw new ArgumentNullException(nameof(fileName), "文件不存在");

            try
            {
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.IgnoreComments = true;
                settings.IgnoreWhitespace = true;
                XmlReader reader = XmlReader.Create(fileName, settings);

                //RootElement = XElement.Load(fileName);
                RootConfiguration = XElement.Load(reader, LoadOptions.None);
                ListItems = RootConfiguration.Elements("Item");

                //Console.WriteLine(RootElement);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取 {fileName} 文件错误：{ex}");
                return;
            }

            //Console.WriteLine("-------");
            //var items = RootElement.Elements("Item");
            //var templates = RootElement.Elements("Template");
            //ReplaceTemplateElements(templates, items, true);

            ReplaceTemplateElements(RootConfiguration, "Template", "RefTemplate", true);

            Console.WriteLine("\r\n\r\n");
            Console.WriteLine(RootConfiguration);
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
                for (int i = 0; i < refTemplates?.Count(); i ++)
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

            //获取引用模板元素集合
            var refTemplates = rootElement.Descendants(refTemplateXName);
            if (refTemplates?.Count() <= 0) return;
            
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
