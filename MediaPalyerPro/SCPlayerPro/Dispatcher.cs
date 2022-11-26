using System;
using System.Collections.Generic;

namespace Sttplay.MediaPlayer
{
    /// <summary>
    /// Any thread can call this class
    /// If the user uses UnitySCPlayerPro, then an SCMGR instance will be created automatically,
    /// and SCMGR will automatically call WakeAll
    /// </summary>
    public class Dispatcher
    {
        private static List<Action> funcList = new List<Action>();

        /// <summary>
        /// Call this function for cross-threading
        /// </summary>
        /// <param name="func">Action</param>
        public static void Invoke(Action func)
        {
            lock (funcList)
                funcList.Add(func);
        }

        /// <summary>
        /// Call this function through the Unity lifecycle function
        /// </summary>
        public static void WakeAll()
        {
            lock (funcList)
            {
                while (funcList.Count > 0)
                {
                    funcList[0]();
                    funcList.RemoveAt(0);
                }
            }
        }
    }
}