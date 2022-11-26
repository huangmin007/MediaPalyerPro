using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SpaceCG.Generic
{
    public static partial class StringExtension
    {
        /// <summary>
        /// log4net.Logger 对象
        /// </summary>
        public static readonly log4net.ILog Logger = log4net.LogManager.GetLogger("StringExtension");

        /// <summary>
        /// 将字符解析为 <see cref="System.Int32"/> 类型数组
        /// </summary>
        /// <param name="value"></param>
        /// <param name="array">如果为空，则按字符串分割的数组长度返回转换结果，如果不为空，则按 array 的长度返回转换结果；</param>
        /// <param name="separator">分隔字符串中子字符串的字符数组、不包含分隔符的空数组或 null。</param>
        /// <param name="style">枚举值的按位组合，用于指示可出现在 string 中的样式元素。要指定的一个典型值为 System.Globalization.NumberStyles.Integer。</param>
        /// <param name="provider">一个对象，提供有关 string 的区域性特定格式设置信息。</param>
        /// <returns></returns>
        public static void ToInt32Array(this String value, ref int[] array, char separator = ',', NumberStyles style = NumberStyles.None, IFormatProvider provider = null)
        {
            string[] stringArray = value.Trim().Split(separator);

            if (array == null || array.Length <= 0)
                array = new int[stringArray.Length];

            int length = Math.Min(stringArray.Length, array.Length);

            for (int i = 0; i < length; i++)
            {
                if (String.IsNullOrWhiteSpace(stringArray[i])) continue;

                int temp = array[i];
                if (!Int32.TryParse(stringArray[i], style, provider, out array[i]))
                    array[i] = temp;
            }
        }

        /// <summary>
        /// 将字符解析为 <see cref="System.UInt32"/> 类型数组
        /// </summary>
        /// <param name="value"></param>
        /// <param name="array">如果为空，则字符分割的数组长度返回转换结果，如果不为空，则按 array 的长度返回转换结果</param>
        /// <param name="separator">分隔字符串中子字符串的字符数组、不包含分隔符的空数组或 null。</param>
        /// <param name="style">枚举值的按位组合，用于指示可出现在 string 中的样式元素。要指定的一个典型值为 System.Globalization.NumberStyles.Integer。</param>
        /// <param name="provider">一个对象，提供有关 string 的区域性特定格式设置信息。</param>
        /// <returns></returns>
        public static void ToUInt32Array(this String value, ref uint[] array, char separator = ',', NumberStyles style = NumberStyles.None, IFormatProvider provider = null)
        {
            string[] stringArray = value.Trim().Split(separator);

            if (array == null || array.Length <= 0)
                array = new uint[stringArray.Length];

            int length = Math.Min(stringArray.Length, array.Length);

            for (int i = 0; i < length; i++)
            {
                if (String.IsNullOrWhiteSpace(stringArray[i])) continue;

                uint temp = array[i];
                if (!UInt32.TryParse(stringArray[i], style, provider, out array[i]))
                    array[i] = temp;
            }
        }

        /// <summary>
        /// 将字符解析为 <see cref="System.Byte"/> 类型数组
        /// </summary>
        /// <param name="value"></param>
        /// <param name="array">如果为空，则字符分割的数组长度返回转换结果，如果不为空，则按 array 的长度返回转换结果</param>
        /// <param name="separator">分隔字符串中子字符串的字符数组、不包含分隔符的空数组或 null。</param>
        /// <param name="style">枚举值的按位组合，用于指示可出现在 string 中的样式元素。要指定的一个典型值为 System.Globalization.NumberStyles.Integer。</param>
        /// <param name="provider">一个对象，提供有关 string 的区域性特定格式设置信息。</param>
        /// <returns></returns>
        public static void ToByteArray(this String value, ref byte[] array, char separator = ',', NumberStyles style = NumberStyles.HexNumber, IFormatProvider provider = null)
        {
            string[] stringArray = value.Trim().Split(separator);

            if (array == null || array.Length <= 0)
                array = new byte[stringArray.Length];

            int length = Math.Min(stringArray.Length, array.Length);

            for (int i = 0; i < length; i++)
            {
                if (String.IsNullOrWhiteSpace(stringArray[i])) continue;

                byte temp = array[i];
                if (!Byte.TryParse(stringArray[i], style, provider, out array[i]))
                    array[i] = temp;
            }
        }

        /// <summary>
        /// 将字符解析为 <see cref="System.UInt16"/> 类型数组
        /// </summary>
        /// <param name="value"></param>
        /// <param name="array">如果为空，则字符分割的数组长度返回转换结果，如果不为空，则按 array 的长度返回转换结果</param>
        /// <param name="separator">分隔字符串中子字符串的字符数组、不包含分隔符的空数组或 null。</param>
        /// <param name="style">枚举值的按位组合，用于指示可出现在 string 中的样式元素。要指定的一个典型值为 System.Globalization.NumberStyles.Integer。</param>
        /// <param name="provider">一个对象，提供有关 string 的区域性特定格式设置信息。</param>
        /// <returns></returns>
        public static void ToUInt16Array(this String value, ref ushort[] array, char separator = ',', NumberStyles style = NumberStyles.HexNumber, IFormatProvider provider = null)
        {
            string[] stringArray = value.Trim().Split(separator);

            if (array == null || array.Length <= 0)
                array = new ushort[stringArray.Length];

            int length = Math.Min(stringArray.Length, array.Length);

            for (int i = 0; i < length; i++)
            {
                if (String.IsNullOrWhiteSpace(stringArray[i])) continue;

                ushort temp = array[i];
                if (!ushort.TryParse(stringArray[i], style, provider, out array[i]))
                    array[i] = temp;
            }
        }
        
        /// <summary>
        /// 字符参数转换为函数参数列表
        /// <para>a1,a2,[c1,c2],c3 OR [a1,a2]</para>
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static object[] ConvertParameters(String parameters)
        {
            if (String.IsNullOrWhiteSpace(parameters))
                throw new ArgumentNullException(nameof(parameters), "参数不能为空");

            try
            {
                int ci = parameters.IndexOf(',');
                int fi = parameters.IndexOf('[');
                int si = parameters.IndexOf(']');

                if (ci != -1 && (fi == -1 || si == -1)) return parameters.Split(',');
                if (ci == -1 && (fi == -1 || si == -1)) return new object[] { parameters };

                List<object> args = new List<object>();
                if (fi == 0 && si == parameters.Length - 1)
                {
                    String temp = parameters.Replace('[', ' ').Replace(']', ' ').Trim();
                    args.Add(temp.Split(','));
                    return args.ToArray();
                }

                if (fi != -1 && si != -1)
                {
                    string temp;
                    int prevIndex = -1, nextIndex = 0;
                    while (nextIndex < parameters.Length - 1)
                    {
                        nextIndex = parameters.IndexOf(',', prevIndex + 1);
                        if (nextIndex < 0) nextIndex = parameters.Length;

                        //Console.Write($"{prevIndex}/{nextIndex}/{parameters.Length}  ");
                        temp = parameters.Substring(prevIndex + 1, nextIndex - prevIndex - 1);

                        if (temp.IndexOf('[') != -1)
                        {
                            nextIndex = parameters.IndexOf(']', prevIndex + 1);
                            temp = parameters.Substring(prevIndex + 1, nextIndex - prevIndex);
                            temp = temp.Replace('[', ' ').Replace(']', ' ').Trim();
                            args.Add(temp.Split(','));
                            nextIndex++;
                        }
                        else
                        {
                            args.Add(temp);
                        }

                        //Console.WriteLine("arg{0}:{1}",args.Count(), temp);

                        prevIndex = nextIndex;
                    }
                }

                return args.ToArray();
            }
            catch(Exception ex)
            {
                Logger.Error(ex);
            }

            return new object[]{ parameters};
        }

        /// <summary>
        /// 转换数组或集合参数
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="targetParamInfo"></param>
        /// <returns></returns>
        public static Array ConvertArrayParameters(String[] parameters, Type arrayType)
        {
            if (parameters?.Length <= 0)
                throw new ArgumentNullException(nameof(parameters), "参数不能为空");
            if (!arrayType.IsArray)
                throw new ArgumentException(nameof(arrayType), "目标参数应为数组或集合类型数据");

            if (arrayType == typeof(String[])) return parameters;

            Type elementType = arrayType.GetElementType();
            Array arguments = Array.CreateInstance(elementType, parameters.Length);
            //Console.WriteLine("ArrayType::{0} {1}", arrayType, elementType);

            for (int i = 0; i < parameters.Length; i ++)
                arguments.SetValue(ConvertValueTypeParameters(parameters[i], elementType), i);

            return arguments;
        }

        /// <summary>
        /// 转换值类型参数
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="paramType"></param>
        /// <returns></returns>
        public static object ConvertValueTypeParameters(object parameter, Type paramType)
        {
            if (parameter == null)
                throw new ArgumentNullException(nameof(parameter), "参数不能为空");
            if (!paramType.IsValueType)
                throw new ArgumentException(nameof(paramType), "目标参数应为值类型参数");

            object argument = null;
            string param = parameter.ToString();
            //Console.WriteLine("ValueType::{0}", paramType);

            if (paramType == typeof(bool))
            {
                argument = bool.TryParse(param, out bool value) ? value : false;
            }
            else if (paramType == typeof(byte))
            {
                param = param.ToLower().Trim();
                if (param.IndexOf("0x") != -1)
                    argument = byte.TryParse(param.Replace("0x", ""), NumberStyles.HexNumber, null, out byte value) ? value : (byte)0x00;
                else
                    argument = byte.TryParse(param, out byte value) ? value : (byte)0x00;
            }
            else if (paramType == typeof(ushort))
            {
                param = param.ToLower().Trim();
                if (param.IndexOf("0x") != -1)
                    argument = ushort.TryParse(param.Replace("0x", ""), NumberStyles.HexNumber, null, out ushort value) ? value : (ushort)0x00;
                else
                    argument = ushort.TryParse(param, out ushort value) ? value : (ushort)0x00;
            }
            else if (paramType == typeof(UInt32[]))
            {
                param = param.ToLower().Trim();
                if (param.IndexOf("0x") != -1)
                    argument = UInt32.TryParse(param.Replace("0x", ""), NumberStyles.HexNumber, null, out UInt32 value) ? value : (UInt32)0x00;
                else
                    argument = UInt32.TryParse(param, out UInt32 value) ? value : (UInt32)0x00;
            }
            else if (paramType == typeof(UInt64[]))
            {
                param = param.ToLower().Trim();
                if (param.IndexOf("0x") != -1)
                    argument = UInt64.TryParse(param.Replace("0x", ""), NumberStyles.HexNumber, null, out UInt64 value) ? value : (UInt64)0x00;
                else
                    argument = UInt64.TryParse(param, out UInt64 value) ? value : (UInt64)0x00;
            }
            else
            {
                argument = Convert.ChangeType(param, paramType);
            }

            return argument;
        }
    }
}
