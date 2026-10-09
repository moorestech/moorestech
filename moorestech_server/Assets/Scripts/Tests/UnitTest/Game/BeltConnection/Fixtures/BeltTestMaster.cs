using System;
using System.Linq;
using Core.Master;
using Mod.Config;
using Newtonsoft.Json.Linq;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.BeltConnection.Fixtures
{
    internal static class BeltTestMaster
    {
        private static readonly Guid UpGuid = Guid.Parse("00000000-0000-0000-0000-0000000000a3");
        private static readonly Guid DownGuid = Guid.Parse("00000000-0000-0000-0000-0000000000a4");
        private static readonly Guid FastGuid = Guid.Parse("00000000-0000-0000-0000-0000000000a5");
        internal static BlockId Up => MasterHolder.BlockMaster.GetBlockId(UpGuid);
        internal static BlockId Down => MasterHolder.BlockMaster.GetBlockId(DownGuid);
        // 分配器と同じ速度128の直線。同速の走行路が分岐segmentへ畳まれる経路のテスト用
        // A straight belt at speed 128, same as the splitter, for testing a same-speed run folded into a branch segment
        internal static BlockId Fast => MasterHolder.BlockMaster.GetBlockId(FastGuid);

        internal static void Load(MasterJsonFileContainer container)
        {
            var fileName = new JsonFileName("blocks");
            var config = container.ConfigJsons[0];
            var json = JObject.Parse(config.JsonContents[fileName]);
            var blocks = (JArray)json["data"];
            var straightGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).BlockGuid;
            var straight = blocks.Single(block => (Guid)block["blockGuid"] == straightGuid);

            // 通常斜面を対象テスト内で組み立てる
            // Build normal slope masters within the connection tests
            AddSlope(ForUnitTestModBlockId.TestGearBeltConveyorUp, UpGuid);
            AddSlope(ForUnitTestModBlockId.TestGearBeltConveyorDown, DownGuid);
            AddFast();
            var family = json["beltConveyorFamilies"].Single(entry => (Guid)entry["straightBlockGuid"] == straightGuid);
            family["upBlockGuid"] = UpGuid.ToString();
            family["downBlockGuid"] = DownGuid.ToString();
            config.JsonContents[fileName] = json.ToString();
            MasterHolder.Load(container);

            #region Internal
            void AddSlope(BlockId gearId, Guid blockGuid)
            {
                var gearGuid = MasterHolder.BlockMaster.GetBlockMaster(gearId).BlockGuid;
                var gear = blocks.Single(block => (Guid)block["blockGuid"] == gearGuid);
                var slope = straight.DeepClone();
                slope["blockGuid"] = blockGuid.ToString();
                slope["name"] = "TestBeltConveyor" + gear["blockParam"]["slopeType"];
                // 既存斜面のポート形状を共用する
                // Reuse the existing slope port geometry
                slope["blockParam"]["slopeType"] = gear["blockParam"]["slopeType"].DeepClone();
                slope["blockParam"]["inventoryConnectors"] = gear["blockParam"]["inventoryConnectors"].DeepClone();
                blocks.Add(slope);
            }

            void AddFast()
            {
                var fast = straight.DeepClone();
                fast["blockGuid"] = FastGuid.ToString();
                fast["name"] = "TestBeltConveyorFast";
                fast["blockParam"]["beltSpeedPerTick"] = 128;
                blocks.Add(fast);
                // 全ベルトはいずれかのファミリーに属する必要がある。斜面を持たない独立ファミリーにする
                // Every belt must belong to a family; give it its own family without slopes
                ((JArray)json["beltConveyorFamilies"]).Add(new JObject { ["straightBlockGuid"] = FastGuid.ToString() });
            }
            #endregion
        }
    }
}
