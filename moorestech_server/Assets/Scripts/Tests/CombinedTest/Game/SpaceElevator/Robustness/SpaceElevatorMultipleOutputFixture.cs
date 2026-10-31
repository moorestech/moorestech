using System;
using System.IO;
using System.Linq;
using Core.Master;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    // 個別Modで2出口・2成果を構成し、共有のテストModは書き換えない
    // Build two outlets and two rewards in a private mod without editing the shared test mod
    public abstract class SpaceElevatorMultipleOutputFixture
    {
        private string _directory;

        [SetUp]
        public void SetupMultipleOutputs()
        {
            SpaceElevatorTestUtil.CreateServer();
            var satelliteGuid = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.TestElevatorSatellite).ItemGuid;
            var rewardGuid = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.TestElevatorReward).ItemGuid;
            var otherGuid = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.RocketComputerId).ItemGuid;
            var baseGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestSpaceElevatorBase).BlockGuid;

            // 実行ごとの一時ディレクトリへ必要なファイルだけを複製する
            // Copy the mod files into a unique temporary directory for each run
            _directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var source = TestModDirectory.ForUnitTestModDirectory;
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories).Where(file => !file.EndsWith(".meta")))
            {
                var destination = Path.Combine(_directory, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination);
            }

            // 成果だけを上限10にし、到着順をA5・B5に固定する
            // Give only the rewards a limit of ten and set arrival order to A5 then B5
            var masterDirectory = Path.Combine(_directory, "mods", "forUnitTest", "master");
            var itemsPath = Path.Combine(masterDirectory, "items.json");
            var items = JObject.Parse(File.ReadAllText(itemsPath));
            var tableGuid = Guid.NewGuid().ToString();
            ((JArray)items["itemStackLevelTables"]).Add(new JObject
            {
                ["tableGuid"] = tableGuid, ["name"] = "MultipleOutputTest", ["stackCounts"] = new JArray(10),
            });
            foreach (var item in items["data"].Where(item => (Guid)item["itemGuid"] == rewardGuid || (Guid)item["itemGuid"] == otherGuid))
                item["stackLevelTableGuid"] = tableGuid;
            var payload = items["orbitalPayloads"].Single(item => (Guid)item["payloadItemGuid"] == satelliteGuid);
            payload["arrivalOutputs"] = new JArray(
                new JObject { ["itemGuid"] = rewardGuid, ["count"] = 5 },
                new JObject { ["itemGuid"] = otherGuid, ["count"] = 5 });
            File.WriteAllText(itemsPath, items.ToString());

            // 2出口で起動検査と実テンプレートを通す
            // Run boot validation and production templates with two outlets
            var blocksPath = Path.Combine(masterDirectory, "blocks.json");
            var blocks = JObject.Parse(File.ReadAllText(blocksPath));
            blocks["data"].Single(block => (Guid)block["blockGuid"] == baseGuid)["blockParam"]["outputSlotCount"] = 2;
            File.WriteAllText(blocksPath, blocks.ToString());
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(_directory));
        }

        [TearDown]
        public void RemovePrivateMod()
        {
            // silent-ok: 作成前に初期化が失敗した場合は削除対象が無い / NUnitが初期化の失敗を報告する
            if (_directory == null || !Directory.Exists(_directory)) return;
            Directory.Delete(_directory, true);
        }
    }
}
