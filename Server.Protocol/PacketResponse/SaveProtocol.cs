using System;
using System.Collections.Generic;
using Game.Save.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.Base;

namespace Server.Protocol.PacketResponse
{
    public class SaveProtocol : IPacketResponse
    {
        public const string Tag = "va:save";
        
        private readonly IWorldSaveDataSaver _worldSaveDataSaver;
        public SaveProtocol(ServiceProvider serviceProvider)
        {
            _worldSaveDataSaver = serviceProvider.GetService<IWorldSaveDataSaver>();
        }
        public List<ToClientProtocolMessagePackBase> GetResponse(List<byte> payload)
        {
            Console.WriteLine("セーブ開始");
            _worldSaveDataSaver.Save();
            Console.WriteLine("セーブ完了");
            return new List<ToClientProtocolMessagePackBase>();
        }
    }
    
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class SaveProtocolMessagePack : ToServerProtocolMessagePackBase
    {
        public SaveProtocolMessagePack()
        {
            ToServerTag = SaveProtocol.Tag;
        }
    }
}