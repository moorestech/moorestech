using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Feedback
{
    /// <summary>
    ///     ドラッグ列からカーソル下セルを選ぶ規則を検証
    ///     Verify the rule that picks the cell under the cursor from a drag
    /// </summary>
    public class PlacementCursorCellResolverTest
    {
        [Test]
        public void カーソル一致セルを返し無ければ末尾を返し空なら負を返す()
        {
            var infos = new List<PlaceInfo>
            {
                new() { Position = new Vector3Int(0, 0, 0) },
                new() { Position = new Vector3Int(1, 0, 0) },
                new() { Position = new Vector3Int(2, 0, 0) },
            };

            Assert.AreEqual(1, PlacementCursorCellResolver.Resolve(infos, new Vector3Int(1, 0, 0)));
            Assert.AreEqual(2, PlacementCursorCellResolver.Resolve(infos, new Vector3Int(9, 9, 9)));
            Assert.AreEqual(-1, PlacementCursorCellResolver.Resolve(new List<PlaceInfo>(), Vector3Int.zero));
        }

        [Test]
        public void 張替え列はXZ一致で引き一致が無ければ末尾へ落とさない()
        {
            // 張替えセルは既設の高さへ追従するのでYはカーソルと一致しない
            // Replace cells follow the existing heights, so their Y never matches the cursor
            var infos = new List<PlaceInfo>
            {
                new() { Position = new Vector3Int(0, 0, 0), IsReplace = true },
                new() { Position = new Vector3Int(0, 1, 1), IsReplace = true },
            };

            Assert.AreEqual(1, PlacementCursorCellResolver.Resolve(infos, new Vector3Int(0, 5, 1)));

            // no-opセルが落ちてカーソル直下が経路に無いとき、無関係な末尾セルの理由を出さない
            // When a no-op cell drops out and the cursor cell is not in the run, no unrelated last cell is reported
            Assert.AreEqual(-1, PlacementCursorCellResolver.Resolve(infos, new Vector3Int(0, 0, 2)));
        }
    }
}
