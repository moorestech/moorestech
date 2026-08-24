using System;
using Game.SaveLoad.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    public class SaveProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:save";
        
        private readonly IWorldSaveRequest _worldSaveRequest;
        
        public SaveProtocol(ServiceProvider serviceProvider)
        {
            _worldSaveRequest = serviceProvider.GetRequiredService<IWorldSaveRequest>();
        }
        
        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            // 要求番号を返し、クライアントが書き出し完了イベントと突き合わせられるようにする
            // Return the generation so the client can match it against the write-completed event
            var requestedSaveGeneration = _worldSaveRequest.RequestSave();
            return new SaveProtocolResponseMessagePack(requestedSaveGeneration);
        }
        
        
        [MessagePackObject]
        public class SaveProtocolMessagePack : ProtocolMessagePackBase
        {
            public SaveProtocolMessagePack()
            {
                Tag = ProtocolTag;
            }
        }
        
        [MessagePackObject]
        public class SaveProtocolResponseMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public long RequestedSaveGeneration { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public SaveProtocolResponseMessagePack() { }
            
            public SaveProtocolResponseMessagePack(long requestedSaveGeneration)
            {
                Tag = ProtocolTag;
                RequestedSaveGeneration = requestedSaveGeneration;
            }
        }
    }
}
