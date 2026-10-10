using Core.BeltTransport;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Context;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.UnitTest.Game.SaveLoad
{
    // ワールド全体のセーブ→ロードを通しても、ベルコン上のアイテムが同じ位置から進み同じtickに届くか
    // Whether a belt item continues from the same place and arrives on the same tick across a whole-world save and load
    public class BeltConveyorSaveLoadTest
    {
        private static readonly ItemId ItemA = new(1);

        [Test]
        public void MidBeltItemKeepsProgressAndArrivalTickAcrossSaveLoad()
        {
            var (_, saveProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var source = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North));
            for (var z = 0; z < 3; z++) Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, z), BlockDirection.North);
            Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 3), BlockDirection.North);
            source.SetItem(0, ServerContext.ItemStackFactory.Create(ItemA, 1));

            // tick1で出口まで761、以後毎tick6進むので50tick後は467(BeltConveyorRebuildTestと同じ)
            // 761 to the exit after tick 1, then 6 per tick, so 467 after 50 ticks (same as BeltConveyorRebuildTest)
            GameUpdater.RunFrames(50);
            var before = ItemsOnSegmentAt(Vector3Int.zero);
            Assert.AreEqual(1, before.Length);
            Assert.AreEqual(467, before[0].DistanceToExit);
            var saveJson = saveProvider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson();

            // 新しいサーバーへロードする。最初のtick先頭の再構築で467に戻り、同じtickで6進む
            // Load into a new server; the first tick-head rebuild puts it back at 467 and the same tick advances it by 6
            var (_, loadProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ((WorldLoaderFromJson)loadProvider.GetRequiredService<IWorldSaveDataLoader>()).Load(saveJson);
            var output = Inventory(ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(0, 0, 3)));
            GameUpdater.UpdateOneTick();
            var after = ItemsOnSegmentAt(Vector3Int.zero);
            Assert.AreEqual(1, after.Length);
            Assert.AreEqual(461, after[0].DistanceToExit);
            Assert.AreEqual(ItemA, after[0].Item.ItemId);
            Assert.AreEqual(BeltEntryDirection.FromBack, after[0].Item.EntryDirection);
            Assert.AreNotEqual(before[0].Item.ItemInstanceId, after[0].Item.ItemInstanceId, "instance id is reissued on load");

            // 461=6*76+5なので76tick後に残り5、次のtickで出口に達してチェストへ渡る
            // 461=6*76+5, so it is 5 away after 76 ticks and reaches the exit and the chest on the next tick
            GameUpdater.RunFrames(76);
            Assert.AreEqual(0, CountOf(output, ItemA));
            Assert.AreEqual(5, ItemsOnSegmentAt(Vector3Int.zero)[0].DistanceToExit);
            GameUpdater.RunFrames(1);
            Assert.AreEqual(1, CountOf(output, ItemA));
            Assert.IsEmpty(ItemsOnSegmentAt(Vector3Int.zero));
        }
    }
}
