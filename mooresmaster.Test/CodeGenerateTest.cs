// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using mooresmaster.Common;
// using mooresmaster.Generator; 
// using NUnit.Framework;
//
// namespace mooresmaster.Test
// {
//     public class CodeGenerateTest
//     {
//         [Test]
//         public static void GenerateTest()
//         {
//             var itemSchemaFile = new TextFile("item.json", File.ReadAllText("../../../TestFile/TestSchema/item.json"));
//             var blockSchemaFile = new TextFile("block.json", File.ReadAllText("../../../TestFile/TestSchema/item.json"));
//             var schemaFiles = new List<TextFile> { itemSchemaFile, blockSchemaFile };
//
//             var generatedFiles = MooresMasterSchemaCodeGenerator.GenerateCode(schemaFiles);
//             
//             var generatedItemCode = generatedFiles.First(file => file.FileName == "Items.g.cs").Content;
//             var itemCodeExpected = File.ReadAllText("../../../TestFile/SampleCode/Items.cs");
//             Assert.That(generatedItemCode, Is.EqualTo(itemCodeExpected));
//             
//             var generatedBlockCode = generatedFiles.First(file => file.FileName == "Blocks.g.cs").Content;
//             var blockCodeExpected = File.ReadAllText("../../../TestFile/SampleCode/Blocks.cs");
//             Assert.That(generatedBlockCode, Is.EqualTo(blockCodeExpected));
//         }
//     }
// }


