using System;
using System.Collections.Generic;
using Game.Save.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

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

        public List<List<byte>> GetResponse(List<byte> payload)
        {
            Debug.Log("セーブ開始");
            _worldSaveDataSaver.Save();
            Debug.Log("セーブ完了");
            return new List<List<byte>>();
        }
    }


    [MessagePackObject(true)]
    public class SaveProtocolMessagePack : ProtocolMessagePackBase
    {
        public SaveProtocolMessagePack()
        {
            Tag = SaveProtocol.Tag;
        }
    }
}