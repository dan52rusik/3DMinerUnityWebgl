#if YandexGamesPlatform_yg
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;

namespace YG
{
    public partial class PlatformYG2 : IPlatformsYG2
    {
        [DllImport("__Internal")]
        private static extern void Init_js(string configJson);

        [DllImport("__Internal")]
        private static extern void Commit_js(string payloadJson);

        [DllImport("__Internal")]
        private static extern void Push_js(string metaJson);

        public void Init(string configJson)
        {
            Init_js(configJson);
        }

        public void Commit(string payloadJson)
        {
            Commit_js(payloadJson);
        }

        public void Push(string metaJson)
        {
            Push_js(metaJson);
        }
    }
}
#endif