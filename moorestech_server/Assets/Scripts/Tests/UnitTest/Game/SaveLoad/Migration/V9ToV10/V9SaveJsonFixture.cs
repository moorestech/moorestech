using Core.Master;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad.Migration.V9ToV10
{
    /// <summary>
    /// 版9→版10の移行テストが手で組むセーブ JSON の部品。向きは版9の12向きの番号（Up 0..3・水平 4..7・Down 8..11）で書く
    /// Pieces of the hand-built save JSON for the version 9->10 migration tests; directions use version-9 twelve-direction numbers (Up 0..3, flat 4..7, Down 8..11)
    /// </summary>
    public static class V9SaveJsonFixture
    {
        public const string CleanRoomPanelGuid = "a069613c-bcb5-4b19-a956-1e264a23f81e";
        public const string CleanRoomWallPanelGuid = "de157d57-7b41-49f5-9228-3762fb196e71";
        public const string MissingMasterGuid = "0fffffff-0000-0000-0000-00000000dead";

        // テスト mod のマスタを読み、1×1×1 と 1×2×3 のブロックの guid を返す
        // Load the test mod master and return the guids of the 1x1x1 and 1x2x3 blocks
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

        // RailSegmentSaveData の端（ConnectionDestination の blockPosition）
        // A RailSegmentSaveData end (ConnectionDestination's blockPosition)
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
