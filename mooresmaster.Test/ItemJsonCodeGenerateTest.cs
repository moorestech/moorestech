using System.Collections.Generic;
using System.IO;
using System.Linq;
using mooresmaster.Common;
using mooresmaster.Generator;
using NUnit.Framework;

namespace mooresmaster.Test
{
    public class ItemJsonCodeGenerateTest
    {
        [Test]
        public static void GenerateTest()
        {
            var itemSchemaContent = File.ReadAllText("../../../TestFile/TestSchema/item.json");
            var itemSchemaFile = new TextFile("item.json", itemSchemaContent);
            var schemaFiles = new List<TextFile> { itemSchemaFile };

            var generatedFiles = MooresMasterSchemaCodeGenerator.GenerateCode(schemaFiles);
            var generatedItemCode = generatedFiles.First(file => file.FileName == "Items.g.cs").Content;

            var generatedItemCodeExpected = File.ReadAllText("../../../TestFile/SampleCode/Items.cs");

            Assert.That(generatedItemCode, Is.EqualTo(generatedItemCodeExpected));
        }
    }
}