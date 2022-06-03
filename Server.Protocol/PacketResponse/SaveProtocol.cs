using System;
using System.Collections.Generic;
using Game.Save.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    public class SaveProtocol : IPacketResponse
    {
        public const string Tag = "va:save";
        
        private readonly ISaveRepository _saveRepository;
        public SaveProtocol(ServiceProvider serviceProvider)
        {
            _saveRepository = serviceProvider.GetService<ISaveRepository>();
        }
        public List<List<byte>> GetResponse(List<byte> payload)
        {
            Console.WriteLine("セーブ開始");
            _saveRepository.Save();
            Console.WriteLine("セーブ完了");
            return new List<List<byte>>();
        }
    }
}