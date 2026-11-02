using Core.Master;
using Core.Update;
using Game.CleanRoom;
using Game.CleanRoom.Util;
using Game.Context;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom.Usability
{
    public class CleanRoomDoorPollutionTest
    {
        [Test]
        public void DoorAddsNoConnectorPollutionTest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(4, 3, 4));
            var interiorCell = new Vector3Int(2, 1, 2);
            var wallOnlyATotal = ATotalOfRoomAt(datastore, interiorCell);

            // 壁1枚をドアへ替えても流入は変わらない（ドアは接続口に数えない）
            // Swapping a wall for a door keeps the inflow unchanged (doors are not connectors)
            CleanRoomHatchTest.ReplaceBlock(ForUnitTestModBlockId.CleanRoomDoorId, new Vector3Int(2, 1, 0));
            Assert.AreEqual(wallOnlyATotal, ATotalOfRoomAt(datastore, interiorCell), 1e-9);

            // ハッチは従来どおり接続口1つ分だけ増やす
            // A hatch still adds exactly one connector's worth
            CleanRoomHatchTest.ReplaceBlock(ForUnitTestModBlockId.CleanRoomItemHatchId, new Vector3Int(2, 1, 4));
            var expected = wallOnlyATotal + MasterHolder.CleanRoomMaster.Pollution.AConnector;
            Assert.AreEqual(expected, ATotalOfRoomAt(datastore, interiorCell), 1e-9);
        }

        #region TestHelper

        private static double ATotalOfRoomAt(CleanRoomDatastore datastore, Vector3Int cell)
        {
            // 置き換え直後は再検出前なので、部屋が見つかるまでtickを進める
            // Right after a replacement the room is not re-detected yet, so tick until it is found
            for (var i = 0; i < 10 && !datastore.TryGetCleanRoomAt(cell, out _); i++) GameUpdater.UpdateOneTick();
            Assert.IsTrue(datastore.TryGetCleanRoomAt(cell, out var room), $"room containing {cell} was not detected");
            return CleanRoomPollution.ComputeATotal(room, ServerContext.WorldBlockDatastore);
        }

        #endregion
    }
}
