using System;
using System.Collections.Generic;

namespace YG
{
    public partial interface IPlatformsYG2
    {
        public void Init(string configJson) { }

        public void Commit(string payloadJson) { }

        public void Push(string metaJson) { }
    }
}