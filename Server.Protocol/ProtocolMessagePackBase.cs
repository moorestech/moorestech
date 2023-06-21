using System;
using MessagePack;

namespace Server.Protocol
{
    [MessagePackObject(keyAsPropertyName:true)]
    public class ProtocolMessagePackBase
    {
        public string Tag { get; set; }
    }
}