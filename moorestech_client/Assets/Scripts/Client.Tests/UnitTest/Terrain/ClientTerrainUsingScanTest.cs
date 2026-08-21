using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UnitTest.Terrain
{
    // クライアントは生成システムのファサードと転送路だけを参照する。Pipeline/Cache/Provisioning/Identity への using は境界違反
    // The client references only the generation system's facade and transfer layer; a using of Pipeline/Cache/Provisioning/Identity breaks the boundary
    public class ClientTerrainUsingScanTest
    {
        private static readonly string[] ForbiddenUsings =
        {
            "using Game.MapGeneration.Pipeline", "using Game.MapGeneration.Cache", "using Game.MapGeneration.Identity",
            "using Game.MapGeneration.Export",
        };

        [Test]
        public void ClientCodeNeverUsesGenerationInternals()
        {
            var clientRoot = Path.Combine(Application.dataPath, "Scripts");
            var offenders = Directory.EnumerateFiles(clientRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Client.Tests{Path.DirectorySeparatorChar}"))
                .Where(path => File.ReadLines(path).Any(line => ForbiddenUsings.Any(line.TrimStart().StartsWith)))
                .ToList();
            Assert.That(offenders, Is.Empty, string.Join("\n", offenders));
        }
    }
}
