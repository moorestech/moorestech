using System.Collections.Generic;
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
            _worldSaveRequest.RequestSave();
            return null;
        }
        
        
        [MessagePackObject]
        public class SaveProtocolMessagePack : ProtocolMessagePackBase
        {
            public SaveProtocolMessagePack()
            {
                Tag = ProtocolTag;
            }
        }
    }
}
