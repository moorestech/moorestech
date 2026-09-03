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
    }
}
