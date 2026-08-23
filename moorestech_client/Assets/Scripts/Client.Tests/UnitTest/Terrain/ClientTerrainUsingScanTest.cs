using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UnitTest.Terrain
{
    // クライアントは生成システムのファサードと転送路だけを参照する。Pipeline/Cache/Provisioning/Identity/Export への using は境界違反
    // 第一の防壁はアセンブリ分割で、本テストはエイリアスusingやasmdefを持たないコードなど参照解決で止まらない記述を拾う二重の網
    // The client references only the generation system's facade and transfer layer; a using of Pipeline/Cache/Provisioning/Identity/Export breaks the boundary
    // The assembly split is the first wall; this test is the second net catching what reference resolution does not stop, such as alias usings and code without an asmdef
    public class ClientTerrainUsingScanTest
    {
        private static readonly string[] ForbiddenNamespaces =
        {
            "Game.MapGeneration.Pipeline", "Game.MapGeneration.Cache", "Game.MapGeneration.Identity",
            "Game.MapGeneration.Export", "Game.MapGeneration.Provisioning",
        };

        // 行頭一致だと「using Alias = Game.MapGeneration.Pipeline.Foo;」を取り逃がす。usingと禁止名前空間が同じ行に並ぶことだけを見る
        // A start-of-line match misses "using Alias = Game.MapGeneration.Pipeline.Foo;", so only the co-occurrence of using and a forbidden namespace on one line is checked
        private static readonly Regex[] ForbiddenUsingPatterns = ForbiddenNamespaces
            .Select(forbiddenNamespace => new Regex($@"using[^;]*\b{Regex.Escape(forbiddenNamespace)}\b"))
            .ToArray();

        [Test]
        public void ClientCodeNeverUsesGenerationInternals()
        {
            var clientRoot = Path.Combine(Application.dataPath, "Scripts");
            var scannedFiles = Directory.EnumerateFiles(clientRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Client.Tests{Path.DirectorySeparatorChar}"))
                .ToList();

            // パス解決が壊れて0件走査になっても緑になる。走査対象が居ることを先に固定する
            // A broken path resolution would scan nothing and still pass, so pin down that there is something to scan first
            Assert.That(scannedFiles, Is.Not.Empty, $"No client script was scanned under '{clientRoot}'.");

            var offenders = scannedFiles
                .Where(path => File.ReadLines(path).Any(line => ForbiddenUsingPatterns.Any(pattern => pattern.IsMatch(line))))
                .ToList();
            Assert.That(offenders, Is.Empty, string.Join("\n", offenders));
        }
    }
}
