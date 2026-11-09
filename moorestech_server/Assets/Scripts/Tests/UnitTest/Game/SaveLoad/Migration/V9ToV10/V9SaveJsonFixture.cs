using Core.Master;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad.Migration.V9ToV10
{
    /// <summary>
    /// 移行テスト用の版9セーブJSON部品（向きは版9の12向き番号）
    /// Version-9 save JSON pieces for migration tests (twelve-direction numbers)
    /// </summary>
    public static class V9SaveJsonFixture
    {
        public const string CleanRoomPanelGuid = "a069613c-bcb5-4b19-a956-1e264a23f81e";
        public const string CleanRoomWallPanelGuid = "de157d57-7b41-49f5-9228-3762fb196e71";
        public const string MissingMasterGuid = "0fffffff-0000-0000-0000-00000000dead";

        // 1×1×1と1×2×3のguidを返す
        // Return the guids of the 1x1x1 and 1x2x3 blocks
        public static (string cubeGuid, string tallGuid) LoadTestMasterGuids()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return (GuidOf(ForUnitTestModBlockId.BlockId, new Vector3Int(1, 1, 1)), GuidOf(MultiSizeBlockTest.Block_1x2x3_Id, new Vector3Int(1, 2, 3)));
        }

        public static JObject WorldBlock(int instanceId, string guid, int legacyDirection, int x, int y, int z)
        {
            return new JObject { ["blockGuid"] = guid, ["instanceId"] = instanceId, ["X"] = x, ["Y"] = y, ["Z"] = z, ["direction"] = legacyDirection, ["state"] = null };
        }

        public static JObject BlueprintBlock(string guid, int legacyDirection, int x, int y, int z)
        {
            return new JObject { ["offsetX"] = x, ["offsetY"] = y, ["offsetZ"] = z, ["blockGuid"] = guid, ["direction"] = legacyDirection, ["settings"] = new JObject() };
        }

        public static JObject Line(int blockIndexA, int blockIndexB)
        {
            return new JObject { ["blockIndexA"] = blockIndexA, ["blockIndexB"] = blockIndexB, ["connectToolGuid"] = "00000000-0000-0000-0000-0000000000aa" };
        }

        // レール端のblockPositionを組む
        // Build the rail end blockPosition
        public static JObject Destination(int x, int y, int z)
        {
            return new JObject { ["blockPosition"] = new JObject { ["x"] = x, ["y"] = y, ["z"] = z } };
        }

        private static string GuidOf(BlockId blockId, Vector3Int expectedSize)
        {
            var master = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            Assert.AreEqual(expectedSize, master.BlockSize, "テストの前提のブロックサイズが違います");
            return master.BlockGuid.ToString();
        }
    }
}
