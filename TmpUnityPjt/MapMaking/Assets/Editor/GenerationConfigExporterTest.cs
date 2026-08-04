using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MapGenerator.EditorExport
{
    public class GenerationConfigExporterTest
    {
        [Test]
        public void EmptyMapObjectGuidDoesNotOverwriteExistingOutput()
        {
            var outputPath = Path.Combine(Path.GetTempPath(), $"generation-export-{Guid.NewGuid():N}.json");
            File.WriteAllText(outputPath, "existing");
            try
            {
                var root = JObject.Parse("{\"algorithmParam\":{\"mapObjects\":[{\"mapObjectGuid\":\"\"}]}}");
                Assert.Throws<InvalidOperationException>(() =>
                    GenerationConfigExporter.WriteValidatedOutput(outputPath, root));
                Assert.That(File.ReadAllText(outputPath), Is.EqualTo("existing"));
            }
            finally
            {
                File.Delete(outputPath);
            }
        }
    }
}
