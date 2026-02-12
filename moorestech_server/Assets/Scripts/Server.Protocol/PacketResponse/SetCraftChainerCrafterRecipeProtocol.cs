using System;
using System.Collections.Generic;
using System.Linq;
using Game.Context;
using Game.CraftChainer.BlockComponent.Crafter;
using Game.CraftChainer.CraftChain;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class SetCraftChainerCrafterRecipeProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:setChainerRecipe";
        
        public SetCraftChainerCrafterRecipeProtocol(ServiceProvider serviceProvider) { }
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<SetCraftChainerCrafterRecipeProtocolMessagePack>(payload.ToArray());
            
            var blockPos = data.BlockPos.Vector3Int;
            
            var crafterBlock = ServerContext.WorldBlockDatastore.GetBlock(blockPos);
            if (crafterBlock == null) return null;
            
            var chainerCrafter = crafterBlock.ComponentManager.GetComponent<CraftCraftChainerCrafterComponent>();
            
            var inputs = data.GetInputs();
            var outputs = data.GetOutputs();
            chainerCrafter.SetRecipe(inputs, outputs);
            
            return null;
        }
        
        [MessagePackObject]
        public class SetCraftChainerCrafterRecipeProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack BlockPos { get; set; }
            [Key(3)] public List<CraftingSolverItemJsonObjectMessagePack> Inputs { get; set; }
            [Key(4)] public List<CraftingSolverItemJsonObjectMessagePack> Outputs { get; set; }
            
            [Obsolete("This constructor is for deserialization. Do not use directly.")]
            public SetCraftChainerCrafterRecipeProtocolMessagePack() { }
            
            public SetCraftChainerCrafterRecipeProtocolMessagePack(Vector3Int blockPos, List<CraftingSolverItem> inputs, List<CraftingSolverItem> outputs)
            {
                Tag = ProtocolTag;
                BlockPos = new Vector3IntMessagePack(blockPos);
                Inputs = inputs.Select(item => new CraftingSolverItemJsonObjectMessagePack(item)).ToList();
                Outputs = outputs.Select(item => new CraftingSolverItemJsonObjectMessagePack(item)).ToList();
            }
            
            public List<CraftingSolverItem> GetInputs()
            {
                return Inputs.Select(item => item.ToCraftingSolverItem()).ToList();
            }
            public List<CraftingSolverItem> GetOutputs()
            {
                return Outputs.Select(item => item.ToCraftingSolverItem()).ToList();
            }
        }
    }
}