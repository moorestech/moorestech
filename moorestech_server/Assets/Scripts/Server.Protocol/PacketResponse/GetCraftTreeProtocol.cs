using System;
using System.Collections.Generic;
using Game.CraftTree;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     プレイヤーのクラフトツリー情報を取得するプロトコル
    /// </summary>
    public class GetCraftTreeProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getCraftTree";
        private readonly CraftTreeManager _craftTreeManager;
        
        public GetCraftTreeProtocol(ServiceProvider serviceProvider)
        {
            _craftTreeManager = serviceProvider.GetService<CraftTreeManager>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var request = MessagePackSerializer.Deserialize<RequestGetCraftTreeMessagePack>(payload.ToArray());
            var playerId = request.PlayerId;
            
            var craftTreeInfo = _craftTreeManager.GetCraftTreeInfo(playerId);
            
            if (craftTreeInfo == null)
            {
                // プレイヤーのクラフトツリー情報がまだ存在しない場合は空のリストを返す
                return new ResponseGetCraftTreeMessagePack(Guid.Empty, new List<CraftTreeNodeMessagePack>());
            }
            
            // クラフトツリーノードをMessagePack形式に変換
            var craftTreeNodeMessagePacks = new List<CraftTreeNodeMessagePack>();
            foreach (var node in craftTreeInfo.CraftTreese.Values)
            {
                var nodePack = new CraftTreeNodeMessagePack(node);
                craftTreeNodeMessagePacks.Add(nodePack);
            }
            
            return new ResponseGetCraftTreeMessagePack(craftTreeInfo.CurrentTargetNode, craftTreeNodeMessagePacks);
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
            [Key(2)] public Guid CurrentTargetNode { get; set; }
            [Key(3)] public List<CraftTreeNodeMessagePack> CraftTrees { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseGetCraftTreeMessagePack() { }
            
            public ResponseGetCraftTreeMessagePack(Guid currentTargetNode, List<CraftTreeNodeMessagePack> craftTrees)
            {
                Tag = ProtocolTag;
                CurrentTargetNode = currentTargetNode;
                CraftTrees = craftTrees;
            }
        }
    }
}