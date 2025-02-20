using System;
using System.Collections.Generic;
using System.Linq;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    public class GetCraftRecipeUnlockStatusesProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getCraftRecipeUnlockStatuses";
        
        private readonly IGameUnlockStateDatastore _gameUnlockStateDatastore;
        
        public GetCraftRecipeUnlockStatusesProtocol(ServiceProvider serviceProvider)
        {
            _gameUnlockStateDatastore = serviceProvider.GetService<IGameUnlockStateDatastore>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var infos = _gameUnlockStateDatastore.CraftRecipeUnlockStateInfos;
            var unlocked = infos.Where(x => x.Value.IsUnlocked).Select(x => x.Key.ToString()).ToList();
            var locked = infos.Where(x => !x.Value.IsUnlocked).Select(x => x.Key.ToString()).ToList();
            
            return new ResponseCraftRecipeUnlockStatusesMessagePack(unlocked, locked);
        }
        
        
        [MessagePackObject]
        public class RequestCraftRecipeUnlockStatusesMessagePack : ProtocolMessagePackBase
        {
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestCraftRecipeUnlockStatusesMessagePack() { }
            public RequestCraftRecipeUnlockStatusesMessagePack(int playerId)
            {
                Tag = ProtocolTag;
            }
        }
        
        [MessagePackObject]
        public class ResponseCraftRecipeUnlockStatusesMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public List<string> UnlockCraftRecipeGuidsStr { get; set; }
            [Key(3)] public List<string> LockedCraftRecipeGuidsStr { get; set; }
            
            [IgnoreMember] public List<Guid> UnlockCraftRecipeGuids => UnlockCraftRecipeGuidsStr.Select(Guid.Parse).ToList();
            [IgnoreMember] public List<Guid> LockedCraftRecipeGuids => LockedCraftRecipeGuidsStr.Select(Guid.Parse).ToList();
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseCraftRecipeUnlockStatusesMessagePack() { }
            public ResponseCraftRecipeUnlockStatusesMessagePack(List<string> unlockCraftRecipeGuidsStr, List<string> lockedCraftRecipeGuidsStr)
            {
                Tag = ProtocolTag;
                UnlockCraftRecipeGuidsStr = unlockCraftRecipeGuidsStr;
                LockedCraftRecipeGuidsStr = lockedCraftRecipeGuidsStr;
            }
        }
    }
}