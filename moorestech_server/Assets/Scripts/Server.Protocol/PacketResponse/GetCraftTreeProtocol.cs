using System;
using System.Collections.Generic;
using Game.CraftTree;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    public class GetCraftTreeProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getCraftTree";
        private readonly CraftTreeManager _craftTreeManager;
        
        public GetCraftTreeProtocol(ServiceProvider serviceProvider)
        {
            _craftTreeManager = serviceProvider.GetService<CraftTreeManager>();
        }
        
        public ProtocolMessagePackBase GetResponse(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<RequestGetCraftTreeMessagePack>(payload);
            
            // プレイヤーIDからCraftTreeInfoを取得
            var craftTreeInfo = _craftTreeManager.GetCraftTreeInfo(data.PlayerId);
            
            // クラフトツリーがない場合は空のレスポンスを返す
            if (craftTreeInfo == null)
            {
                return new ResponseGetCraftTreeMessagePack(new List<CraftTreeNodeMessagePack>(), Guid.Empty);
            }
            
            // CraftTreeInfoからレスポンスを作成
            var craftTreeNodes = new List<CraftTreeNodeMessagePack>();
            foreach (var tree in craftTreeInfo.CraftTrees.Values)
            {
                craftTreeNodes.Add(new CraftTreeNodeMessagePack(tree));
            }
            
            return new ResponseGetCraftTreeMessagePack(craftTreeNodes, craftTreeInfo.CurrentTargetNode);
        }
        
        [MessagePackObject]
        public class RequestGetCraftTreeMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestGetCraftTreeMessagePack() { }
            
            public RequestGetCraftTreeMessagePack(int playerId)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
            }
        }
        
        [MessagePackObject]
        public class ResponseGetCraftTreeMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public List<CraftTreeNodeMessagePack> CraftTrees { get; set; }
            [Key(3)] public Guid CurrentTargetNode { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseGetCraftTreeMessagePack() { }
            
            public ResponseGetCraftTreeMessagePack(List<CraftTreeNodeMessagePack> craftTrees, Guid currentTargetNode)
            {
                Tag = ProtocolTag;
                CraftTrees = craftTrees;
                CurrentTargetNode = currentTargetNode;
            }
        }
    }
}