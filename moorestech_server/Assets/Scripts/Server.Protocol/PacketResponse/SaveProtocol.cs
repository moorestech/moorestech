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

        public ProtocolMessagePackBase GetResponse(byte[] payload)
        {
            // セーブ完了をクライアントに ACK で返す
            // Return ACK to the client once save completes
            Debug.Log("セーブ開始");
            _worldSaveDataSaver.Save();
            Debug.Log("セーブ完了");
            return new SaveResponseMessagePack();
        }

        [MessagePackObject]
        public class SaveRequestMessagePack : ProtocolMessagePackBase
        {
            public SaveRequestMessagePack()
            {
                Tag = ProtocolTag;
            }
        }

        [MessagePackObject]
        public class SaveResponseMessagePack : ProtocolMessagePackBase
        {
            public SaveResponseMessagePack()
            {
                Tag = ProtocolTag;
            }
        }
    }
}
