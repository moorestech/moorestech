using System;
using System.Text;
using System.Text.RegularExpressions;
using Core.Master;
using Game.Block.Blocks.OrbitalCargo;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Blueprint;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util.OrbitalCargo;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Game.OrbitalCargo
{
    // 積荷の選択をブループリントでコピーする（ADR 0106 3・5）
    // Copy the cargo selection through blueprints (ADR 0106 3, 5)
    public class OrbitalCargoBlueprintTest
    {
        [SetUp]
        public void Setup()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void 選んだ積荷がブループリントで別の台へ写る(bool isPad)
        {
            var blockId = isPad ? ForUnitTestModBlockId.RocketLaunchPadId : ForUnitTestModBlockId.TestSpaceElevatorBase;
            var payload = isPad ? ForUnitTestItemId.CablePayloadId : ForUnitTestItemId.TestElevatorBulkPayload;
            var source = Place(blockId, Vector3Int.zero, Array.Empty<BlockCreateParam>());
            OrbitalCargoTestUtil.SelectCargo(source, payload);

            // 範囲から抽出した設定で別の座標へ置き、同じ積荷が選ばれている
            // Place at another position with the extracted settings and the same cargo is selected
            Assert.IsTrue(BlueprintCreateService.TryCreateFromArea("cargo", Vector3Int.zero, Vector3Int.zero, out var blueprint));
            var json = blueprint.Blocks[0].Settings[OrbitalCargoBlueprintSettingsComponent.SettingsKey];
            StringAssert.Contains(MasterHolder.ItemMaster.GetItemGuid(payload).ToString(), json);
            var pasted = Place(blockId, new Vector3Int(20, 0, 20), Params(json));
            Assert.AreEqual(payload, ((SelectedOrbitalCargo)pasted.GetComponent<IOrbitalCargoSelectorComponent>().Selection).PayloadItemId);
        }

        [Test]
        public void 未解放の積荷を持つブループリントは未選択で置かれる()
        {
            // 解放前のプレイヤーが他人のブループリントを貼った場合（ADR 0106 3）
            // A player pasting someone else's blueprint before unlocking the payload (ADR 0106 3)
            OrbitalCargoTestUtil.LockItem(ForUnitTestItemId.CablePayloadId);
            var json = $"{{\"selectedCargoItemGuid\":\"{MasterHolder.ItemMaster.GetItemGuid(ForUnitTestItemId.CablePayloadId)}\"}}";
            LogAssert.Expect(LogType.Warning, new Regex("積荷の選択を拒否しました 理由:Locked"));
            var pad = Place(ForUnitTestModBlockId.RocketLaunchPadId, Vector3Int.zero, Params(json));
            Assert.IsInstanceOf<UnselectedOrbitalCargo>(pad.GetComponent<IOrbitalCargoSelectorComponent>().Selection);
        }

        [Test]
        public void 未選択や今のマスタに無い積荷は未選択で置かれる()
        {
            var unselected = Place(ForUnitTestModBlockId.RocketLaunchPadId, Vector3Int.zero, Params("{\"selectedCargoItemGuid\":null}"));
            Assert.IsInstanceOf<UnselectedOrbitalCargo>(unselected.GetComponent<IOrbitalCargoSelectorComponent>().Selection);

            LogAssert.Expect(LogType.Warning, new Regex("ブループリントの積荷 .* が今のマスタに無いため"));
            var unknown = Place(ForUnitTestModBlockId.RocketLaunchPadId, new Vector3Int(20, 0, 0), Params($"{{\"selectedCargoItemGuid\":\"{Guid.NewGuid()}\"}}"));
            Assert.IsInstanceOf<UnselectedOrbitalCargo>(unknown.GetComponent<IOrbitalCargoSelectorComponent>().Selection);
        }

        [TestCase("{")]
        [TestCase("[]")]
        [TestCase("null")]
        [TestCase("{}")]
        public void 読めない積荷設定は警告して未選択で設置する(string settings)
        {
            LogAssert.Expect(LogType.Warning, new Regex("ブループリントの積荷設定が不正なため"));
            var block = Place(ForUnitTestModBlockId.RocketLaunchPadId, Vector3Int.zero, Params(settings));
            Assert.IsInstanceOf<UnselectedOrbitalCargo>(block.GetComponent<IOrbitalCargoSelectorComponent>().Selection);
        }

        private static BlockCreateParam[] Params(string json)
        {
            return new[] { new BlockCreateParam(OrbitalCargoBlueprintSettingsComponent.SettingsKey, Encoding.UTF8.GetBytes(json)) };
        }

        private static IBlock Place(BlockId blockId, Vector3Int position, BlockCreateParam[] createParams)
        {
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(blockId, position, BlockDirection.North, createParams, out var block));
            return block;
        }
    }
}
