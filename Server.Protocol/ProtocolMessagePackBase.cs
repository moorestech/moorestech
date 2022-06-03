using System;
using MessagePack;

namespace Server.Protocol
{
    [MessagePackObject(keyAsPropertyName:true)]
    [Serializable]
    public class ProtocolMessagePackBase
    {
        public string Tag { get; set; }
    }
}