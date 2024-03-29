﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SpaceCG.Generic
{
    /// <summary>
    /// 实例功能扩展库
    /// </summary>
    public static partial class InstanceExtensions
    {
        /// <summary>
        /// log4net.Logger 对象
        /// </summary>
        public static readonly log4net.ILog Logger = log4net.LogManager.GetLogger("InstanceExtension");

        /// <summary>
        /// 动态的设置实例对象的属性值，跟据 XML 配置文件节点名称(实例字段)及节点属性(字段属性)来个修改实例属性
        /// <para>例如：&lt;Window Left="100" Width="100"/&gt; </para>
        /// <para> Window 是 instanceObj 中的一个实例对象(或变量对象，非静态对象)，其 Left、Width 为 Window 实例对象的原有属性 </para>
        /// </summary>
        /// <param name="instanceObj"></param>
        /// <param name="element"></param>
        public static void ChangeInstancePropertyValue(object instanceObj, XElement element)
        {
            if (instanceObj == null || element == null)
                throw new ArgumentNullException("参数不能为空");

            Object instanceFieldObj = GetInstanceFieldObject(instanceObj, element.Name.LocalName);
            if (instanceFieldObj == null) return;
            
            IEnumerable<XAttribute> attributes = element.Attributes();
            if (attributes.Count() == 0) return;

            foreach (XAttribute attribute in attributes)
            {
                ChangeInstancePropertyValue(instanceFieldObj, attribute.Name.LocalName, attribute.Value);
            }
        }

        /// <summary>
        /// 动态的设置实例对象的属性值，跟据 配置文件 中读取对应实例的 key 属性值
        /// <para>nameSpace 格式指：[Object.]Property , Object 可为空</para>
        /// </summary>
        /// <param name="instanceObj"></param>
        /// <param name="nameSpace"></param>
        public static void ChangeInstancePropertyValue(object instanceObj, String nameSpace)
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

                ChangeInstancePropertyValue(instanceObj, property.Name, value);
            }
        }

        /// <summary>
        /// 动态的设置实例对象的属性值
        /// </summary>
        /// <param name="instanceObj"></param>
        /// <param name="propertyName"></param>
        /// <param name="newValue"></param>
        public static void ChangeInstancePropertyValue(Object instanceObj, String propertyName, Object newValue)
        {
            if (instanceObj == null || String.IsNullOrWhiteSpace(propertyName)) throw new ArgumentNullException("参数不能为空");

            Type type = instanceObj.GetType();
            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !property.CanWrite || !property.CanRead) return;

            object convertValue = null;
            try
            {
                if (newValue == null || newValue.ToString().ToLower().Trim() == "null")
                {
                    property.SetValue(instanceObj, convertValue);
                    return;
                }

                Type valueType = property.GetValue(instanceObj)?.GetType();

                //值不空 && 值类型为枚举类型
                if (valueType?.IsEnum == true)
                    convertValue = Enum.Parse(property.PropertyType, newValue.ToString());
                else if(valueType?.IsValueType == true)
                    convertValue = StringExtension.ConvertValueTypeParameters(newValue, valueType);
                else
                    convertValue = Convert.ChangeType(newValue, property.PropertyType);
                
                property.SetValue(instanceObj, convertValue);
            }
            catch (Exception ex)
            {
                Logger.ErrorFormat("设置实例对象 {0} 的属性 {1} 值 {2} 失败：{3}", type, property, newValue == null ? "null" : newValue, ex);                
            }
        }

        /// <summary>
        /// 动态移除实例对象所有委托事件
        /// </summary>
        /// <param name="instanceObj">对象实例</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void RemoveInstanceEvents(object instanceObj)
        {
            if (instanceObj == null) throw new ArgumentNullException("参数不能为空");

            EventInfo[] events = instanceObj.GetType().GetEvents();
            foreach (EventInfo info in events)
                RemoveInstanceEvents(instanceObj, info.Name);
#if false
            EventInfo[] baseEvents = instanceObj.GetType().BaseType.GetEvents();
            foreach (EventInfo info in baseEvents)
                RemoveInstanceEvents(instanceObj, info.Name);
#endif
        }
        /// <summary>
        /// 动态移除实例对象指定的委托事件
        /// </summary>
        /// <param name="instanceObj">对象实例</param>
        /// <param name="eventName">对象事件名称</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void RemoveInstanceEvents(object instanceObj, string eventName)
        {
            if (instanceObj == null || string.IsNullOrWhiteSpace(eventName))
                throw new ArgumentNullException("参数不能为空");

            //BindingFlags bindingAttr = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            BindingFlags bindingAttr = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static;

            try
            {
                FieldInfo fields = instanceObj.GetType().GetField(eventName, bindingAttr);      //当前类类型中查找
                if (fields == null)
                {
                    fields = instanceObj.GetType().BaseType.GetField(eventName, bindingAttr);   //基类类型中查找
                    if (fields == null) return;
                }

                object value = fields.GetValue(instanceObj);
                if (value == null || !(value is Delegate)) return;

                Delegate anonymity = (Delegate)value;
                foreach (Delegate handler in anonymity.GetInvocationList())
                {
                    if (instanceObj.GetType().GetEvent(eventName) == null) continue;

                    instanceObj.GetType().GetEvent(eventName).RemoveEventHandler(instanceObj, handler);
                    if (Logger.IsDebugEnabled)
                        Logger.Debug($"Object({instanceObj.GetType()}) Remove Event: {eventName}({handler.Method.Name})");
                }
            }
            catch (Exception ex)
            {
                Logger.ErrorFormat("Remove Anonymous Events Error:{0}", ex);
            }
        }

        /// <summary>
        /// 动态调用 实例 对象的方法
        /// </summary>
        /// <param name="instanceObj"></param>
        /// <param name="methodInfo"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static object CallInstanceMethod(object instanceObj, MethodInfo methodInfo, params object[] parameters)
        {
            ParameterInfo[] parameterInfos = methodInfo.GetParameters();

            String paramInfo = "";
            if (parameterInfos.Length > 0)
            {
                foreach (ParameterInfo info in parameterInfos)
                    paramInfo += info.ToString() + ", ";
                paramInfo = paramInfo.Substring(0, paramInfo.Length - 2);
            }

            object[] _parameters;
            if (methodInfo.IsStatic && methodInfo.IsDefined(typeof(ExtensionAttribute), false) && parameters.Length != parameterInfos.Length)
            {
                _parameters = new object[parameters.Length + 1];
                _parameters[0] = instanceObj;
                for (int i = 0; i < parameters.Length; i++)
                    _parameters[i + 1] = parameters[i];

                Logger.Info($"实例对象 {instanceObj}, 准备执行匹配的扩展函数 {instanceObj.GetType()} {methodInfo.Name}({paramInfo}), 参数 {parameterInfos.Length}/{_parameters.Length} 个");
            }
            else if(instanceObj == null && methodInfo.IsStatic)
            {
                _parameters = parameters;
                Logger.Info($"准备执行匹配的静态函数 {methodInfo.Name}({paramInfo}) 参数 {parameterInfos.Length}/{_parameters.Length} 个");
            }
            else
            {
                _parameters = parameters;
                Logger.Info($"实例对象 {instanceObj}, 准备执行匹配的函数 {instanceObj.GetType()} {methodInfo.Name}({paramInfo}), 参数 {parameterInfos.Length}/{_parameters.Length} 个");
            }

            object[] arguments = new object[_parameters.Length];
            try
            {
                //参数转换
                for (int i = 0; i < _parameters.Length; i++)
                {
                    ParameterInfo pInfo = parameterInfos[i];
                    if (_parameters[i] == null || _parameters[i].ToString().ToLower().Trim() == "null")
                    {
                        arguments[i] = null;
                        continue;
                    }

                    //Console.WriteLine($"Convert Type:: {_parameters[i].GetType()} / {pInfo.ParameterType}  IsValueType:{pInfo.ParameterType.IsValueType}  IsArray:{pInfo.ParameterType.IsArray}  IsEnum:{pInfo.ParameterType.IsEnum}");

                    if (pInfo.ParameterType.IsEnum)
                    {
                        arguments[i] = Enum.Parse(pInfo.ParameterType, _parameters[i].ToString(), true);
                    }
                    else if (pInfo.ParameterType.IsValueType)
                    {
                        arguments[i] = StringExtension.ConvertValueTypeParameters(_parameters[i], pInfo.ParameterType);
                    }
                    else if (pInfo.ParameterType.IsArray)
                    {
                        arguments[i] = StringExtension.ConvertArrayParameters((String[])_parameters[i], pInfo.ParameterType);
                    }
                    else
                    {
                        arguments[i] = Convert.ChangeType(_parameters[i], pInfo.ParameterType);
                    }
                }

                return methodInfo.Invoke(instanceObj, arguments);
            }
            catch (Exception ex)
            {
                Logger.Debug($"函数执行失败: {ex}");
            }

            return null;
        }

        /// <summary>
        /// 动态调用 实例 对象的方法
        /// </summary>
        /// <param name="instanceObj"></param>
        /// <param name="methodName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static object CallInstanceMethod(object instanceObj, String methodName, params object[] parameters)
        {
            if (instanceObj == null || String.IsNullOrWhiteSpace(methodName))
                throw new ArgumentNullException("参数不能为空");

            Type type = instanceObj.GetType();
            IEnumerable<MethodInfo> methods = from method in type.GetMethods()
                                              where method.Name == methodName
                                              where method.GetParameters().Length == parameters.Length
                                              select method;

            if (methods.Count() != 1)
            {
                Logger.Warn($"在实例对象 {instanceObj} 中，找到匹配的函数 {methodName} 有 {methods.Count()} 个，取消执行 或 查找扩展函数");

                return CallExtensionMethod(instanceObj, methodName, parameters);
            }

            return CallInstanceMethod(instanceObj, methods.ElementAt(0), parameters);
        }

        /// <summary>
        /// 动态调用 对象扩展 的方法
        /// </summary>
        /// <param name="instanceObj"></param>
        /// <param name="methodName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static object CallExtensionMethod(object instanceObj, String methodName, params object[] parameters)
        {
            if (instanceObj == null || String.IsNullOrWhiteSpace(methodName))
                throw new ArgumentNullException("参数不能为空");

            IEnumerable<MethodInfo> methods = from type in typeof(InstanceExtensions).Assembly.GetTypes()
                                              where type.IsSealed && !type.IsGenericType && !type.IsNested
                                              from method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                                              where method.Name == methodName && method.IsDefined(typeof(ExtensionAttribute), false) && method.GetParameters()[0].ParameterType == instanceObj.GetType()
                                              select method;

            if (methods.Count() != 1)
            {
                Logger.Warn($"在实例对象 {instanceObj} 中，找到匹配的扩展函数 {methodName} 有 {methods.Count()} 个，取消执行");
                return null;
            }

            return CallInstanceMethod(instanceObj, methods.ElementAt(0), parameters);
        }

        /// <summary>
        /// 动态调用 静态方法
        /// </summary>
        /// <param name="classFullName"></param>
        /// <param name="methodName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static object CallClassStaticMethod(String classFullName, String methodName, params object[] parameters)
        {
            IEnumerable<MethodInfo> methods = from assembly in AppDomain.CurrentDomain.GetAssemblies()
                                              let type = assembly.GetType(classFullName)
                                              where type != null
                                              from method in type.GetMethods(BindingFlags.Static | BindingFlags.Public)
                                              where method.Name == methodName && method.GetParameters().Length == parameters.Length
                                              from paramInfo in method.GetParameters()
                                              where paramInfo.ParameterType.IsValueType || paramInfo.ParameterType.IsArray || paramInfo.ParameterType.IsEnum 
                                              select method;

#if false
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(classFullName);
                if (type == null) continue;

                foreach(MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public))
                {
                    if (!(method.Name == methodName && method.GetParameters().Length == parameters.Length)) continue;
                    
                    foreach(ParameterInfo parameter in method.GetParameters())
                    {
                        Console.WriteLine($"{classFullName}, {methodName},,,{parameter.Name}:{parameter.ParameterType},{parameter.ParameterType.IsClass},{parameter.ParameterType.IsValueType}");
                       //if (parameter.ParameterType.IsAbstract || parameter.ParameterType.IsClass) continue;
                    }

                }
            }
#endif

            Console.WriteLine($"Count:{methods?.Count()}");
            if(methods?.Count() == 0)
            {
                Logger.Warn($"在类 {classFullName} 中，没找到匹配的静态函数 {methodName} ，取消执行");
                return null;
            }

            //if (methods?.Count() == 1)
            //{
                //Logger.Warn($"在类 {classFullName} 中，找到匹配的静态函数 {methodName} 有 {methods.Count()} 个");
                Logger.InfoFormat("在类 {0} 中，找到匹配的静态函数 {1} 有 {2} 个{3}", classFullName, methodName, methods.Count(), methods.Count() > 1 ? ", 存在执行歧异" : "");
                //return null;
            //}

            return CallInstanceMethod(null, methods.ElementAt(0), parameters); ;
        }

        /// <summary>
        /// 动态的获取实例的字段对象。注意：包括私有对象
        /// </summary>
        /// <param name="instanceObj"></param>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        public static object GetInstanceFieldObject(object instanceObj, String fieldName)
        {
            if (instanceObj == null || String.IsNullOrWhiteSpace(fieldName))
                throw new Exception("参数不能为空");

            try
            {
                Type type = instanceObj.GetType();
                FieldInfo fieldInfo = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (fieldInfo == null)
                {
                    Logger.Warn($"在实例对象 {instanceObj} 中，未找到指定字段 {fieldName} 对象");
                    return null;
                }

                return fieldInfo.GetValue(instanceObj);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            return null;
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
                Logger.Warn($"应用程序或文档 \"{fileName}\" 不存在");
                return null;
            }

            String nameSpace = fileNameCfgKey.Replace("FileName", "");
            ProcessStartInfo startInfo = new ProcessStartInfo(fileInfo.FullName);
            startInfo.WorkingDirectory = fileInfo.DirectoryName;

            ChangeInstancePropertyValue(startInfo, nameSpace);

            //重复一次，转为使用绝对路径
            startInfo.FileName = fileInfo.FullName;

            Process process = new Process();
            process.StartInfo = startInfo;
            process.EnableRaisingEvents = true;
            process.Exited += (s, e) => Logger.Warn($"应用程序或文档 \"{fileName}\" 发生退出事件(ExitCode:{process.ExitCode})");

            Task.Run(() =>
            {
                try
                {
                    if (process.Start())
                        Logger.Info($"已启动的应用程序或文档 \"{fileName}\"");
                    else
                        Logger.Warn($"应用程序或文档 \"{fileName}\" 启动失败");
                }
                catch (Exception ex)
                {
                    Logger.Error($"应用程序或文档 \"{fileName}\" 启动时发生错误：{ex}");
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
                Logger.Info($"退出并释放进程模块 {name}(ExitCode:{code}) 完成");
            }
            catch (Exception ex)
            {
                Logger.Error($"退出并释放 进程模块资源 对象错误: {ex}");
            }
            finally
            {
                process = null;
            }
        }

        /// <summary>
        /// 清除复杂的可访问对象集合
        /// </summary>
        /// <param name="accessObjects"></param>
        public static void DisposeAccessObjects(IReadOnlyDictionary<string, IDisposable> accessObjects)
        {
            if (accessObjects == null) return;

            foreach (var kv in accessObjects)
            {
                if (kv.Value == null) continue;

                Type type = kv.Value.GetType();
                Logger.Info($"Dispose Access Objects ...... {kv.Key} : {type}");

                if (type == typeof(System.IO.Ports.SerialPort))
                {
                    System.IO.Ports.SerialPort serialPort = (System.IO.Ports.SerialPort)kv.Value;
                    InstanceExtensions.DisposeSerialPort(ref serialPort);
                }
                else if (type == typeof(Process))
                {
                    Process process = (Process)kv.Value;
                    InstanceExtensions.DisposeProcessModule(ref process);
                }
                else if (typeof(Modbus.Device.IModbusMaster).IsAssignableFrom(type))
                {
                    Modbus.Device.IModbusMaster master = (Modbus.Device.IModbusMaster)kv.Value;
                    InstanceExtensions.DisposeNModbus4Master(ref master);
                }
#if NModbus
                else if (typeof(NModbus.IModbusMaster).IsAssignableFrom(type))
                {
                    NModbus.IModbusMaster master = (NModbus.IModbusMaster)kv.Value;
                    InstanceExtension.DisposeNModbusMaster(ref master);
                }
#endif
                else if (typeof(HPSocket.IServer).IsAssignableFrom(type))
                {
                    HPSocket.IServer server = (HPSocket.IServer)kv.Value;
                    InstanceExtensions.DisposeNetworkServer(ref server);
                }
                else if (typeof(HPSocket.IClient).IsAssignableFrom(type))
                {
                    HPSocket.IClient client = (HPSocket.IClient)kv.Value;
                    InstanceExtensions.DisposeNetworkClient(ref client);
                }
                else
                {
                    kv.Value.Dispose();
                }
            }


        }
    }
}
