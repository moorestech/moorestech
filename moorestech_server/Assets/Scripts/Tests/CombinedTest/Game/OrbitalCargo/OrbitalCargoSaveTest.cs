using System.Linq;
using System.Text.RegularExpressions;
using Core.Master;
using Game.Block.Blocks.OrbitalCargo;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.UnlockState;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using Tests.Util.Rocket;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorConnectionTest;

namespace Tests.CombinedTest.Game.OrbitalCargo
{
    // 積荷の選択のセーブ（ADR 0106 6）。版番号を上げず、項目が無い旧セーブは未選択で読む
    // Saving the cargo selection (ADR 0106 6): no version bump, and an old save without the item loads unselected
    public class OrbitalCargoSaveTest
    {
        [Test]
        public void 項目の無い旧セーブの発射台は未選択で読まれ建造を拒否する()
        {
            // 今のコードで保存した世界から項目を消し、巡回位置を足して旧セーブの形にする
            // Turn a world saved by current code into the old shape: drop the item and add the rotation index
            var save = RocketLaunchPadSaveTestUtil.CreateSavedWorld(RocketLaunchPadPhase.AwaitingBuildCommand);
            Assert.IsTrue(PadBlockState(save).Remove(OrbitalCargoSelectorComponent.SaveKeyName));
            RocketLaunchPadSaveTestUtil.State(save)["nextPayloadIndex"] = 1;

            LogAssert.Expect(LogType.Log, new Regex($"ブロックのセーブ状態にキー {OrbitalCargoSelectorComponent.SaveKeyName} がないため"));
            var pad = RocketLaunchPadSaveTestUtil.Load(save);
            Assert.IsInstanceOf<UnselectedOrbitalCargo>(pad.GetComponent<IOrbitalCargoSelectorComponent>().Selection);

            // 積み終えた段階は残り、選ぶまで建造の合図は通らない
            // The loaded stage remains, and the build command is refused until a cargo is chosen
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            Assert.AreEqual(RocketLaunchPadPhase.AwaitingBuildCommand, RocketLaunchPadTestUtil.ReadState(processor).Phase);
            LogAssert.Expect(LogType.Warning, new Regex("ロケット発射台への操作を拒否しました 理由:CargoNotSelected"));
            Assert.AreEqual(RocketLaunchPadCommandResult.CargoNotSelected, processor.RequestBuild());
            // 保存し直すと巡回位置は書かれない
            // Saving again does not write the rotation index
            Assert.IsNull(JObject.FromObject(RocketLaunchPadSaveTestUtil.Capture(pad))["nextPayloadIndex"]);
        }

        [Test]
        public void 読み込んだ選択は未解放でも残り今の便の積荷も変わらない()
        {
            // 建造中（衛星を積んだ便）に電線ケーブルへ選び直した世界を、衛星・ケーブルが未解放の状態で読む
            // Load a world reselected to cable mid-build (satellite flight) with both payloads locked
            var save = RocketLaunchPadSaveTestUtil.CreateSavedWorld(RocketLaunchPadPhase.Building);
            Assert.AreEqual(1, LockInSave(save, ForUnitTestItemId.CablePayloadId));
            Assert.AreEqual(1, LockInSave(save, ForUnitTestItemId.SatellitePayloadId));
            var pad = RocketLaunchPadSaveTestUtil.Load(save);
            Assert.IsFalse(ServerContext.GetService<IGameUnlockStateDataController>().ItemUnlockStateInfos[ForUnitTestItemId.CablePayloadId].IsUnlocked);

            // 解放はセーブの復元では見ない（ADR 0106 3）
            // Unlock state is not checked when restoring a save (ADR 0106 3)
            var detail = RocketLaunchPadTestUtil.ReadState(pad.GetComponent<RocketLaunchPadProcessorComponent>());
            Assert.AreEqual(ForUnitTestItemId.CablePayloadId, detail.SelectedCargoItemId);
            Assert.AreEqual(ForUnitTestItemId.SatellitePayloadId, detail.LoadedCargoItemId);
        }

        [Test]
        public void 項目の無い旧セーブの拠点は未選択で読まれ入口の中身は残る()
        {
            var (_, provider) = CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero, ForUnitTestItemId.TestElevatorSatellite);
            Inventory(elevatorBase).SetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot, ForUnitTestItemId.TestElevatorSatellite, 1);
            var save = JObject.Parse(SaveLoadJsonTestHelper.AssembleSaveJson(provider));
            var baseState = (JObject)save["world"].OfType<JObject>().Single(block => block["state"]?["spaceElevatorClimber"] != null)["state"];
            Assert.IsTrue(baseState.Remove(OrbitalCargoSelectorComponent.SaveKeyName));

            LogAssert.Expect(LogType.Log, new Regex($"ブロックのセーブ状態にキー {OrbitalCargoSelectorComponent.SaveKeyName} がないため"));
            var (_, loadedProvider) = CreateServer();
            SaveLoadJsonTestHelper.LoadFromJson(loadedProvider, save.ToString());
            var loaded = ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero);

            // 入口の中身は消さず、選ぶまで受け取りも昇降も止まる（ADR 0106 6・7）
            // Inlet contents are kept, and accepting and climbing stop until a cargo is chosen (ADR 0106 6, 7)
            Assert.AreEqual(1, Inventory(loaded).GetItem(SpaceElevatorBaseInventoryComponent.SatelliteSlot).Count);
            Assert.AreEqual(SpaceElevatorBaseStatus.CargoNotSelected, ReadDetail(loaded).Status);
            Assert.IsNull(ReadDetail(loaded).SelectedCargoItemId);
        }

        private static JObject PadBlockState(JObject save)
        {
            var key = typeof(RocketLaunchPadProcessorComponent).FullName;
            return (JObject)save["world"].OfType<JObject>().Single(block => block["state"]?[key] != null)["state"];
        }

        // セーブの解放状態で指定の品を未解放にし、書き換えた件数を返す
        // Mark the item locked in the saved unlock state and return how many entries were rewritten
        private static int LockInSave(JObject save, ItemId itemId)
        {
            var guid = MasterHolder.ItemMaster.GetItemGuid(itemId).ToString();
            var entries = save.Descendants().OfType<JObject>().Where(entry => (string)entry["guid"] == guid && entry["isUnlocked"] != null).ToList();
            foreach (var entry in entries) entry["isUnlocked"] = false;
            return entries.Count;
        }
    }
}
