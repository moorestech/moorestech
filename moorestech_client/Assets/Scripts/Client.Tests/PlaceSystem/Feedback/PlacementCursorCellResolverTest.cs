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

            Assert.AreEqual(1, PlacementCursorCellResolver.Resolve(infos, new Vector3Int(1, 0, 0), PlacementCursorMatch.ExactCellOrLast));
            Assert.AreEqual(2, PlacementCursorCellResolver.Resolve(infos, new Vector3Int(9, 9, 9), PlacementCursorMatch.ExactCellOrLast));
            Assert.AreEqual(-1, PlacementCursorCellResolver.Resolve(new List<PlaceInfo>(), Vector3Int.zero, PlacementCursorMatch.ExactCellOrLast));
        }

        [Test]
        public void XZ規則は高さ違いを拾い一致が無ければ解決しない()
        {
            // 列のセルがカーソルと違う高さに並ぶ場合でも、XZが揃っていればそのセルを指す
            // Even when the run's cells sit at other heights, an XZ match still points at that cell
            var infos = new List<PlaceInfo>
            {
                new() { Position = new Vector3Int(0, 0, 0) },
                new() { Position = new Vector3Int(0, 1, 1) },
            };

            Assert.AreEqual(1, PlacementCursorCellResolver.Resolve(infos, new Vector3Int(0, 5, 1), PlacementCursorMatch.HorizontalOnly));

            // カーソルが列のXZから外れたら、無関係な末尾セルを指さずに解決を諦める
            // Once the cursor leaves the run's XZ it gives up rather than pointing at an unrelated last cell
            Assert.AreEqual(-1, PlacementCursorCellResolver.Resolve(infos, new Vector3Int(0, 0, 2), PlacementCursorMatch.HorizontalOnly));
            Assert.AreEqual(-1, PlacementCursorCellResolver.Resolve(new List<PlaceInfo>(), Vector3Int.zero, PlacementCursorMatch.HorizontalOnly));
        }
    }
}
