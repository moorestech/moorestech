using System.Collections.Generic;
using Client.Game.InGame.Map.MapObject;
using NUnit.Framework;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;

namespace Client.Tests.Map
{
    /// <summary>
    ///     距離順ソートと近傍境界の件数算出を検証
    ///     Verifies the distance ordering and the near-field boundary count
    /// </summary>
    public class MapObjectLayoutDistanceOrderTest
    {
        [Test]
        public void 原点から近い順に並ぶ()
        {
            var layouts = new List<MapObjectLayoutMessagePack>
            {
                CreateLayout(1, 100f, 0f, 0f),
                CreateLayout(2, 10f, 0f, 0f),
                CreateLayout(3, 50f, 0f, 0f),
            };

            var order = MapObjectLayoutDistanceOrder.SortNearFieldFirst(layouts, Vector3.zero);

            Assert.AreEqual(2, order.Entries[0].Layout.InstanceId);
            Assert.AreEqual(3, order.Entries[1].Layout.InstanceId);
            Assert.AreEqual(1, order.Entries[2].Layout.InstanceId);
        }

        [Test]
        public void 距離はY成分も含む3Dで測る()
        {
            var layouts = new List<MapObjectLayoutMessagePack>
            {
                CreateLayout(1, 10f, 100f, 0f),
                CreateLayout(2, 20f, 0f, 0f),
            };

            var order = MapObjectLayoutDistanceOrder.SortNearFieldFirst(layouts, Vector3.zero);
            Assert.AreEqual(2, order.Entries[0].Layout.InstanceId);
        }

        [Test]
        public void 半径ちょうどの個体は近傍に含む()
        {
            var layouts = new List<MapObjectLayoutMessagePack>
            {
                CreateLayout(1, MapObjectLayoutDistanceOrder.NearFieldRadius, 0f, 0f),
                CreateLayout(2, MapObjectLayoutDistanceOrder.NearFieldRadius + 0.001f, 0f, 0f),
            };

            var order = MapObjectLayoutDistanceOrder.SortNearFieldFirst(layouts, Vector3.zero);
            Assert.AreEqual(1, order.NearFieldCount);
        }

        [Test]
        public void 全件が半径内なら全数を返す()
        {
            var layouts = new List<MapObjectLayoutMessagePack>
            {
                CreateLayout(1, 1f, 0f, 0f),
                CreateLayout(2, 2f, 0f, 0f),
            };

            var order = MapObjectLayoutDistanceOrder.SortNearFieldFirst(layouts, Vector3.zero);
            Assert.AreEqual(2, order.NearFieldCount);
        }

        [Test]
        public void 空のlayoutでも成立する()
        {
            var order = MapObjectLayoutDistanceOrder.SortNearFieldFirst(new List<MapObjectLayoutMessagePack>(), Vector3.zero);
            Assert.AreEqual(0, order.Entries.Count);
            Assert.AreEqual(0, order.NearFieldCount);
        }

        [Test]
        public void 近傍件数は入力順ではなく距離順の先頭から数える()
        {
            // 入力先頭が遠方でも近傍が0件にならないこと。SortとCountが1呼び出しに畳まれている根拠を突く
            // A distant first input must not zero the near field; this pins the fold of sorting and counting into one call
            var layouts = new List<MapObjectLayoutMessagePack>
            {
                CreateLayout(1, MapObjectLayoutDistanceOrder.NearFieldRadius + 100f, 0f, 0f),
                CreateLayout(2, 10f, 0f, 0f),
                CreateLayout(3, 20f, 0f, 0f),
            };

            var order = MapObjectLayoutDistanceOrder.SortNearFieldFirst(layouts, Vector3.zero);
            Assert.AreEqual(2, order.NearFieldCount);
        }

        private static MapObjectLayoutMessagePack CreateLayout(int instanceId, float x, float y, float z)
        {
            return new MapObjectLayoutMessagePack(
                instanceId, "00000000-0000-0000-0000-000000000001", x, y, z,
                1f, 1f, 1f,
                0f, 0f, 0f, 1f);
        }
    }
}
