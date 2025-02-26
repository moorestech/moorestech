using System;
using System.Collections.Generic;
using System.Linq;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    public class GetGameUnlockStateProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getGameUnlockState";
        
        private readonly IGameUnlockStateData gameUnlockStateData;
        
        public GetGameUnlockStateProtocol(ServiceProvider serviceProvider)
        {
            gameUnlockStateData = serviceProvider.GetService<IGameUnlockStateDataController>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var infos = gameUnlockStateData.CraftRecipeUnlockStateInfos;
            var unlocked = infos.Where(x => x.Value.IsUnlocked).Select(x => x.Key.ToString()).ToList();
            var locked = infos.Where(x => !x.Value.IsUnlocked).Select(x => x.Key.ToString()).ToList();
            
            return new ResponseGameUnlockStateProtocolMessagePack(unlocked, locked);
        }
        
        
        [MessagePackObject]
        public class RequestGameUnlockStateProtocolMessagePack : ProtocolMessagePackBase
        {
            public RequestGameUnlockStateProtocolMessagePack()
            {
                Tag = ProtocolTag;
            }
        }
        
        [MessagePackObject]
        public class ResponseGameUnlockStateProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public List<string> UnlockCraftRecipeGuidsStr { get; set; }
            [Key(3)] public List<string> LockedCraftRecipeGuidsStr { get; set; }
            
            [IgnoreMember] public List<Guid> UnlockCraftRecipeGuids => UnlockCraftRecipeGuidsStr.Select(Guid.Parse).ToList();
            [IgnoreMember] public List<Guid> LockedCraftRecipeGuids => LockedCraftRecipeGuidsStr.Select(Guid.Parse).ToList();
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseGameUnlockStateProtocolMessagePack() { }
            public ResponseGameUnlockStateProtocolMessagePack(List<string> unlockCraftRecipeGuidsStr, List<string> lockedCraftRecipeGuidsStr)
            {
                Tag = ProtocolTag;
                UnlockCraftRecipeGuidsStr = unlockCraftRecipeGuidsStr;
                LockedCraftRecipeGuidsStr = lockedCraftRecipeGuidsStr;
            }
        }
    }
}