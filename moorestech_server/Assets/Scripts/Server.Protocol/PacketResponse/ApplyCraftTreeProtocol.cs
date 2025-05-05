using System;
using System.Collections.Generic;
using Game.CraftTree;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    public class ApplyCraftTreeProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:applyCraftTree";
        private readonly CraftTreeManager _craftTreeManager;
        
        public ApplyCraftTreeProtocol(ServiceProvider serviceProvider)
        {
            _craftTreeManager = serviceProvider.GetService<CraftTreeManager>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<ApplyCraftProtocolMessagePack>(payload.ToArray());
            
            var node = data.TreeNode.CreateCraftTreeNode();
            _craftTreeManager.ApplyCraftTree(data.PlayerId, node);
            
            return null;
        }
        
        
        [MessagePackObject]
        public class ApplyCraftProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public CraftTreeNodeMessagePack TreeNode { get; set; }
            
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ApplyCraftProtocolMessagePack() { }
            
            public ApplyCraftProtocolMessagePack(int playerId, CraftTreeNode treeNode)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                TreeNode = new CraftTreeNodeMessagePack(treeNode);
            }
        }
    }
}