using System;
using System.Collections.Generic;
using Game.CraftTree;
using Game.CraftTree.Models;
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
            
            var craftTreeNodes = new List<CraftTreeNode>();
            foreach (var tree in data.CraftTrees)
            {
                var node = new CraftTreeNode(tree, null);
                craftTreeNodes.Add(node);
            }
            var craftTreeInfo = new PlayerCraftTreeInfo(data.CurrentTargetNode, craftTreeNodes);
            
            _craftTreeManager.ApplyCraftTree(data.PlayerId, craftTreeInfo);
            
            return null;
        }
        
        
        [MessagePackObject]
        public class ApplyCraftProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public Guid CurrentTargetNode { get; set; }
            [Key(4)] public List<CraftTreeNodeMessagePack> CraftTrees { get; set; }
            
            
            [Obsolete("This constructor is for deserialization. Do not use directly.")]
            public ApplyCraftProtocolMessagePack() { }
            
            public ApplyCraftProtocolMessagePack(int playerId, Guid currentTargetNode, List<CraftTreeNode> craftTrees)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                CurrentTargetNode = currentTargetNode;
                CraftTrees = craftTrees.ConvertAll(tree => new CraftTreeNodeMessagePack(tree));
            }
        }
    }
}