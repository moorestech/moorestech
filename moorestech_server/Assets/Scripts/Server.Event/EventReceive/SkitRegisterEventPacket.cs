using System;
using System.Collections.Generic;
using MessagePack;

namespace Server.Event.EventReceive
{
    public class SkitRegisterEventPacket
    {
        public const string EventTag = "va:event:skitRegister";
        
        [MessagePackObject]
        public class SkitRegisterEventMessagePack
        {
            [Key(0)] public List<string> PlayedSkitIds { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public SkitRegisterEventMessagePack() { }
            
            public SkitRegisterEventMessagePack(List<string> playedSkitIds)
            {
                PlayedSkitIds = playedSkitIds;
            }
        }
    }
}