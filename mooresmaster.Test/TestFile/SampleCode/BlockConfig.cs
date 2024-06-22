using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using mooresmaster.Common;
using Newtonsoft.Json.Linq;
using UnityEngine;


namespace mooresmaster.Test.TestFile.SampleCode
{
    public class BlockConfig
    {
        public List<BlocksElement> Blocks { get; init; }
        
        [AllowNull]
        public string[] ArrayTest { get; init; }
        
        [AllowNull]
        public ObjectTest ObjectTest { get; init; }
        
        public Vector2? Vector2Test { get; init; }
        public Vector3Int? Vector3IntTest { get; init; }
        public Vector4? Vector4Test { get; init; }
        public bool? BoolTest { get; init; }
        public int? IntTest { get; init; }
        public float? FloatTest { get; init; }
    }
    
    public class BlocksElement
    {
        public BlockId BlockId { get; init; }
        public ItemId ItemId { get; init; }
        public string BlockType { get; init; }
        
        [AllowNull]
        public IBlockParam BlockParam { get; init; }
    }

    public class ObjectTest
    {
        
    }
    public struct BlockId
    {
        private readonly Guid value;

        public BlockId(Guid value)
        {
            this.value = value;
        }
    }

    public interface IBlockParam { }
    
    public class TypeABlockParam : IBlockParam
    {
        public string ParamA { get; init; }
        public int ParamB { get; init; }
    }

    public class TypeBBlockParam : IBlockParam
    {
        public bool ParamA { get; init; }
        public float ParamB { get; init; }
    }

    public static class BlockTypes
    {
        public const string TypeA = "TypeA";
        public const string TypeB = "TypeB";
    }

    public static class BlockLoader
    {
        public static BlockConfig LoadItemConfig(List<ModConfigInfo> sortedConfigs)
        {
            List<BlocksElement> blocks = new List<BlocksElement>();
            
            foreach (var config in sortedConfigs)
            {
                if (!config.ConfigJsons.TryGetValue("block",out var jsonText)) continue;
                
                dynamic jsonObject = JObject.Parse(jsonText);


                foreach (var jsonBlocksElement in jsonObject.blocks)
                {
                    string BlockIdStr = jsonBlocksElement.blockId;
                    BlockId BlockId = new BlockId(new Guid(BlockIdStr));
                    
                    string ItemIdStr = jsonBlocksElement.itemId;
                    ItemId ItemId = new ItemId(new Guid(ItemIdStr));
                    
                    string BlockType = jsonBlocksElement.blockType;
                    
                    IBlockParam blockParam = null;
                    switch (BlockType)
                    {
                        case BlockTypes.TypeA:
                            blockParam = new TypeABlockParam()
                            {
                                ParamA = jsonBlocksElement.blockParam.paramA,
                                ParamB = jsonBlocksElement.blockParam.paramB,
                            };
                            break;
                        case BlockTypes.TypeB:
                            blockParam = new TypeBBlockParam()
                            {
                                ParamA = jsonBlocksElement.blockParam.paramA,
                                ParamB = jsonBlocksElement.blockParam.paramB,
                            };
                            break;
                    }

                    BlocksElement itemsElementObject = new BlocksElement()
                    {
                        BlockId = BlockId,
                        ItemId = ItemId,
                        BlockType = BlockType,
                        BlockParam = blockParam,
                    };

                    blocks.Add(itemsElementObject);
                }
                
                string[] arrayTest = null;
            
            }
            

            return new BlockConfig()
            {
                Blocks = blocks,
            };
        }
    }
}