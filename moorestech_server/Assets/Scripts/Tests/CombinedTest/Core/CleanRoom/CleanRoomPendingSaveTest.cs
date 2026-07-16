using Core.Update;
using Game.CleanRoom;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom
{
    public class CleanRoomPendingSaveTest
    {
        [Test]
        public void SaveImmediatelyAfterWallPlacementPersistsSplitRoomsTest()
        {
            var (_, sourceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sourceDatastore = sourceProvider.GetRequiredService<CleanRoomDatastore>();

            // 内部3x1x1の密閉室を確定し、不純物を90に設定する
            // Finalize a sealed 3x1x1 room and set its impurity to 90
            CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(4, 2, 2));
            for (var i = 0; i < 5; i++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(1, sourceDatastore.Rooms.Count);
            sourceDatastore.Rooms[0].SetImpurity(90.0);

            // 中央の壁を置いた直後に保存し、通常tickを挟まない
            // Place the center wall and save immediately without a normal tick
            CleanRoomDetectionTest.AddBlock(ForUnitTestModBlockId.CleanRoomWallId, new Vector3Int(2, 1, 1));
            var json = sourceProvider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson();

            // 新しい世界へ読み込み、二部屋への状態引き継ぎを確認する
            // Load into a fresh world and verify state was carried into both rooms
            var (_, loadedProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var loader = (WorldLoaderFromJson)loadedProvider.GetRequiredService<IWorldSaveDataLoader>();
            loader.Load(json);
            var loadedRooms = loadedProvider.GetRequiredService<CleanRoomDatastore>().Rooms;

            Assert.AreEqual(2, loadedRooms.Count);
            foreach (var room in loadedRooms) Assert.AreEqual(30.0, room.ImpurityCount, 0.001);
        }
    }
}
