using System;
using System.Collections.Generic;
using System.Linq;
using Game.Challenge;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    public class GetRecipeUnlockStatusesProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getRecipeUnlockStatuses";
        
        private readonly IGameUnlockStateDatastore _gameUnlockStateDatastore;
        
        public GetRecipeUnlockStatusesProtocol(ServiceProvider serviceProvider)
        {
            _gameUnlockStateDatastore = serviceProvider.GetService<IGameUnlockStateDatastore>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var infos = _gameUnlockStateDatastore.RecipeUnlockStateInfos;
            var currentGuids = infos.Where(x => x.Value.IsUnlocked).Select(x => x.Key.ToString()).ToList();
            var lockedGuids = infos.Where(x => !x.Value.IsUnlocked).Select(x => x.Key.ToString()).ToList();
            
            return new ResponseRecipeUnlockStatusesMessagePack(currentGuids, lockedGuids);
        }
        
        
        [MessagePackObject]
        public class RequestRecipeUnlockStatusesMessagePack : ProtocolMessagePackBase
        {
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestRecipeUnlockStatusesMessagePack() { }
            public RequestRecipeUnlockStatusesMessagePack(int playerId)
            {
                Tag = ProtocolTag;
            }
        }
        
        [MessagePackObject]
        public class ResponseRecipeUnlockStatusesMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public List<string> UnlockRecipesGuidsStr { get; set; }
            [Key(3)] public List<string> LockedRecipesGuidsStr { get; set; }
            
            [IgnoreMember] public List<Guid> UnlockChallengeGuids => UnlockRecipesGuidsStr.Select(Guid.Parse).ToList();
            [IgnoreMember] public List<Guid> LockedChallengeGuids => LockedRecipesGuidsStr.Select(Guid.Parse).ToList();
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseRecipeUnlockStatusesMessagePack() { }
            public ResponseRecipeUnlockStatusesMessagePack(List<string> unlockRecipesGuidsStr, List<string> lockedRecipesGuidsStr)
            {
                Tag = ProtocolTag;
                UnlockRecipesGuidsStr = unlockRecipesGuidsStr;
                LockedRecipesGuidsStr = lockedRecipesGuidsStr;
            }
        }
    }
}