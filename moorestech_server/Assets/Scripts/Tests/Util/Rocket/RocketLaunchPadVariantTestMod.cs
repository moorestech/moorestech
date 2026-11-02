using System;
using System.IO;
using System.Linq;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.Util.Rocket
{
    internal static class RocketLaunchPadVariantTestMod
    {
        internal static string CreateServer(string itemsJson, string blocksJson)
        {
            var root = Path.Combine(Path.GetTempPath(), $"rocket-variant-{Guid.NewGuid()}");
            // DI構築前に専用modを整え、到着サービスのキャッシュも同じ定義にする
            // Prepare the isolated mod before DI so the arrival cache uses the same definitions
            foreach (var source in Directory.GetFiles(TestModDirectory.ForUnitTestModDirectory, "*", SearchOption.AllDirectories).Where(path => !path.EndsWith(".meta")))
            {
                var destination = Path.Combine(root, Path.GetRelativePath(TestModDirectory.ForUnitTestModDirectory, source));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(source, destination);
            }
            var master = Path.Combine(root, "mods", "forUnitTest", "master");
            File.WriteAllText(Path.Combine(master, "items.json"), itemsJson);
            File.WriteAllText(Path.Combine(master, "blocks.json"), blocksJson);
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(root));
            return root;
        }
    }
}
