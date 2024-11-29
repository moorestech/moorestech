using System.Collections.Generic;
using Game.SaveLoad.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class SaveProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:save";
        
        private readonly IWorldSaveDataSaver _worldSaveDataSaver;
        
        public SaveProtocol(ServiceProvider serviceProvider)
        {
            _worldSaveDataSaver = serviceProvider.GetService<IWorldSaveDataSaver>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            Debug.Log("セーブ開始");
            _worldSaveDataSaver.Save();
            Debug.Log("セーブ完了");
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