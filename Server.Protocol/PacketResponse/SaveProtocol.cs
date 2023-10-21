using System;
using System.Collections.Generic;
using Game.Save.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

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
            Console.WriteLine("");
            _worldSaveDataSaver.Save();
            Console.WriteLine("");
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