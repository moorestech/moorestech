// using System;
// using System.Collections.Generic;
// using System.Diagnostics.CodeAnalysis;
// using mooresmaster.Common;
// using Newtonsoft.Json.Linq;
// using UnityEngine;
//
// public class BlockConfig
// {
//     public List<BlocksElement> Blocks { get; init; }
// }
//
// public class BlocksElement
// {
//     public BlockId BlockId { get; init; }
//     public ItemId ItemId { get; init; }
//     public string BlockType { get; init; }
//
//     [AllowNull] public List<string> ArrayTest { get; init; }
//
//     [AllowNull] public ObjectTest ObjectTest { get; init; }
//
//     public Vector2? Vector2Test { get; init; }
//     public Vector3Int? Vector3IntTest { get; init; }
//     public Vector4? Vector4Test { get; init; }
//     public bool? BoolTest { get; init; }
//     public int? IntTest { get; init; }
//     public float? FloatTest { get; init; }
//
//
//     [AllowNull] public IBlockParam BlockParam { get; init; }
// }
//
// public class ObjectTest
// {
// }
//
// public struct BlockId
// {
//     private readonly Guid value;
//
//     public BlockId(Guid value)
//     {
//         this.value = value;
//     }
// }
//
// public interface IBlockParam
// {
// }
//
// public class TypeABlockParam : IBlockParam
// {
//     public string ParamA { get; init; }
//     public int ParamB { get; init; }
// }
//
// public class TypeBBlockParam : IBlockParam
// {
//     public bool ParamA { get; init; }
//     public float ParamB { get; init; }
// }
//
// public static class BlockTypes
// {
//     public const string TypeA = "TypeA";
//     public const string TypeB = "TypeB";
// }
//
// public static class BlockLoader
// {
//     public static BlockConfig LoadItemConfig(List<ModConfigInfo> sortedConfigs)
//     {
//         List<BlocksElement> blocks = new List<BlocksElement>();
//
//         foreach (var config in sortedConfigs)
//         {
//             if (!config.ConfigJsons.TryGetValue("block", out var jsonText)) continue;
//
//             dynamic jsonObject = JObject.Parse(jsonText);
//
//
//             foreach (var blocksJsonElement in jsonObject.blocks)
//             {
//                 string BlockIdStr = blocksJsonElement.blockId;
//                 BlockId BlockId = new BlockId(new Guid(BlockIdStr));
//
//                 string ItemIdStr = blocksJsonElement.itemId;
//                 ItemId ItemId = new ItemId(new Guid(ItemIdStr));
//
//                 string BlockType = blocksJsonElement.blockType;
//
//                 IBlockParam blockParam = null;
//                 switch (BlockType)
//                 {
//                     case BlockTypes.TypeA:
//                         blockParam = new TypeABlockParam()
//                         {
//                             ParamA = blocksJsonElement.blockParam.paramA,
//                             ParamB = blocksJsonElement.blockParam.paramB,
//                         };
//                         break;
//                     case BlockTypes.TypeB:
//                         blockParam = new TypeBBlockParam()
//                         {
//                             ParamA = blocksJsonElement.blockParam.paramA,
//                             ParamB = blocksJsonElement.blockParam.paramB,
//                         };
//                         break;
//                 }
//
//                 List<string> arrayTest = null;
//                 foreach (var arrayTestJsonElement in blocksJsonElement.arrayTest)
//                 {
//                     string arrayTestStr = arrayTestJsonElement;
//
//                     arrayTest ??= new List<string>();
//                     arrayTest.Add(arrayTestStr);
//                 }
//
//                 Vector2? vector2Test = null;
//                 if (blocksJsonElement.vector2Test != null)
//                 {
//                     float vector2TestX = blocksJsonElement.vector2Test[0];
//                     float vector2TestY = blocksJsonElement.vector2Test[1];
//                     vector2Test = new Vector2(vector2TestX, vector2TestY);
//                 }
//
//                 Vector3Int? vector3IntTest = null;
//                 if (blocksJsonElement.vector3IntTest != null)
//                 {
//                     int vector3IntTestX = blocksJsonElement.vector3Test[0];
//                     int vector3IntTestY = blocksJsonElement.vector3Test[1];
//                     int vector3IntTestZ = blocksJsonElement.vector3Test[2];
//                     vector3IntTest = new Vector3Int(vector3IntTestX, vector3IntTestY, vector3IntTestZ);
//                 }
//
//                 Vector4? vector4Test = null;
//                 if (blocksJsonElement.vector4Test != null)
//                 {
//                     int vector4TestX = blocksJsonElement.vector4Test[0];
//                     int vector4TestY = blocksJsonElement.vector4Test[1];
//                     int vector4TestZ = blocksJsonElement.vector4Test[2];
//                     int vector4TestW = blocksJsonElement.vector4Test[3];
//                     vector4Test = new Vector4(vector4TestX, vector4TestY, vector4TestZ, vector4TestW);
//                 }
//
//                 bool? boolTest = null;
//                 if (blocksJsonElement.boolTest != null)
//                 {
//                     boolTest = blocksJsonElement.boolTest;
//                 }
//
//                 int? intTest = null;
//                 if (blocksJsonElement.intTest != null)
//                 {
//                     intTest = blocksJsonElement.intTest;
//                 }
//
//                 float? floatTest = null;
//                 if (blocksJsonElement.floatTest != null)
//                 {
//                     floatTest = blocksJsonElement.floatTest;
//                 }
//
//                 BlocksElement blocksElementObject = new BlocksElement()
//                 {
//                     BlockId = BlockId,
//                     ItemId = ItemId,
//                     BlockType = BlockType,
//                     BlockParam = blockParam,
//                     Vector2Test = vector2Test,
//                     Vector3IntTest = vector3IntTest,
//                     Vector4Test = vector4Test,
//                     BoolTest = boolTest,
//                     IntTest = intTest,
//                     FloatTest = floatTest,
//                 };
//
//                 blocks.Add(blocksElementObject);
//             }
//         }
//
//
//         return new BlockConfig()
//         {
//             Blocks = blocks,
//         };
//     }
// }


