using System;
using System.IO;
using System.Linq;
using Core.Master;
using Newtonsoft.Json.Linq;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.SaveLoad.PowerGenerator
{
    internal static class ElectricGeneratorRecoveryTestMod
    {
        internal const string FuelGuid = "ffffffff-ffff-ffff-ffff-00000000c014";

        internal static void Create(string root)
        {
            foreach (var source in Directory.GetFiles(TestModDirectory.ForUnitTestModDirectory, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".meta")))
            {
                var destination = Path.Combine(root, Path.GetRelativePath(TestModDirectory.ForUnitTestModDirectory, source));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(source, destination);
            }
            // 専用燃料だけを追加して、原本と既存参照を保つ
            // Add a dedicated fuel item while preserving originals and existing references
            var path = MasterPath(root, "items.json");
            var items = JObject.Parse(File.ReadAllText(path));
            var item = items["data"][0].DeepClone();
            item["itemGuid"] = FuelGuid;
            item["name"] = "Recovery test fuel";
            ((JArray)items["data"]).Add(item);
            File.WriteAllText(path, items.ToString());
            var blocksPath = MasterPath(root, "blocks.json");
            var blocks = JObject.Parse(File.ReadAllText(blocksPath));
            var generator = blocks["data"].Single(b => (string)b["blockGuid"] == MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GeneratorId).BlockGuid.ToString());
            ((JArray)generator["blockParam"]["fuelItems"]).Insert(0, new JObject { ["itemGuid"] = FuelGuid, ["time"] = 1, ["power"] = 100 });
            File.WriteAllText(blocksPath, blocks.ToString());
        }

        internal static void RemoveFuel(string root, bool removeItem)
        {
            var path = MasterPath(root, "blocks.json");
            var blocks = JObject.Parse(File.ReadAllText(path));
            foreach (var fuel in blocks["data"].SelectMany(b => b["blockParam"]?["fuelItems"] as JArray ?? new JArray()).ToList())
                if ((string)fuel["itemGuid"] == FuelGuid) fuel.Remove();
            File.WriteAllText(path, blocks.ToString());
            if (!removeItem) return; // silent-ok: 設定削除のみの対照条件 / 呼出側のNUnitが拒否を検証する
            path = MasterPath(root, "items.json");
            var items = JObject.Parse(File.ReadAllText(path));
            items["data"].Single(i => (string)i["itemGuid"] == FuelGuid).Remove();
            File.WriteAllText(path, items.ToString());
        }

        private static string MasterPath(string root, string name)
        {
            return Path.Combine(root, "mods", "forUnitTest", "master", name);
        }
    }
}
