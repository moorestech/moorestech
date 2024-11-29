using System;
using System.Collections.Generic;
using Game.Context;
using Game.CraftChainer.BlockComponent.Crafter;
using Game.CraftChainer.CraftChain;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class SetChainerCrafterRecipeProtocol : IPacketResponse
    {
        public const string Tag = "va:setChainerRecipe";
        
        public SetChainerCrafterRecipeProtocol(ServiceProvider serviceProvider) { }
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<SetChainerCrafterRecipeProtocolMessagePack>(payload.ToArray());
            
            var blockPos = data.BlockPos.Vector3Int;
            var recipe = data.Recipe.ToCraftingSolverRecipe();
            
            var crafterBlock = ServerContext.WorldBlockDatastore.GetBlock(blockPos);
            if (crafterBlock == null) return null;
            
            var chainerCrafter = crafterBlock.ComponentManager.GetComponent<ChainerCrafterComponent>();
            chainerCrafter.SetRecipe(recipe.Inputs, recipe.Outputs);
            
            return null;
        }
    }
    
    [MessagePackObject]
    public class SetChainerCrafterRecipeProtocolMessagePack : ProtocolMessagePackBase
    {
        [Key(2)] public Vector3IntMessagePack BlockPos { get; set; }
        [Key(3)] public CraftingSolverRecipeSerializeObject Recipe { get; set; }
        
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public SetChainerCrafterRecipeProtocolMessagePack() { }

        
        public SetChainerCrafterRecipeProtocolMessagePack(Vector3Int blockPos, CraftingSolverRecipe craftingSolverRecipe)
        {
            Tag = SetChainerCrafterRecipeProtocol.Tag;
            BlockPos = new Vector3IntMessagePack(blockPos);
            Recipe = new CraftingSolverRecipeSerializeObject(craftingSolverRecipe);
        }
    }
}