using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Ground
{
    // 底面から1/3mより深い沈みだけを埋まりとする（ADR 0082）
    // Only a sink deeper than 1/3 m below the bottom counts as buried (ADR 0082)
    public class PlacementTerrainSinkQueryTest
    {
        private readonly List<GameObject> _slabs = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var slab in _slabs) Object.DestroyImmediate(slab);
            _slabs.Clear();
        }

        [TestCase(10, 10.3f, false)]
        [TestCase(10, 10.334f, false)]
        [TestCase(10, 10.336f, true)]
        [TestCase(10, 10.6f, true)]
        [TestCase(9, 10.3f, true)]
        [TestCase(11, 10.6f, false)]
        public void 地表が底面から3分の1mを超えたら埋まり(int baseY, float groundMax, bool expected)
        {
            Assert.AreEqual(expected, PlacementTerrainSinkQuery.IsSunkAbove(baseY, groundMax));
        }

        [Test]
        public void 高さ3は底から0点3沈んでも可で1点3なら不可()
        {
            // 天面が y=30.3 の地面を作る
            // Ground whose top is y=30.3
            CreateGroundSlab(new Vector3(0.5f, 29.8f, 0.5f), new Vector3(4f, 1f, 4f));
            var size = new Vector3Int(1, 3, 1);

            Assert.IsFalse(PlacementTerrainSinkQuery.IsSunk(new Vector3Int(0, 30, 0), BlockDirection.North, size));
            Assert.IsTrue(PlacementTerrainSinkQuery.IsSunk(new Vector3Int(0, 29, 0), BlockDirection.North, size));
        }

        [Test]
        public void 高さ1も底から0点6沈めば不可()
        {
            // 天面が y=30.6 の地面を作る
            // Ground whose top is y=30.6
            CreateGroundSlab(new Vector3(0.5f, 30.1f, 0.5f), new Vector3(4f, 1f, 4f));

            Assert.IsFalse(PlacementTerrainSinkQuery.IsSunk(new Vector3Int(0, 31, 0), BlockDirection.North, Vector3Int.one));
            Assert.IsTrue(PlacementTerrainSinkQuery.IsSunk(new Vector3Int(0, 30, 0), BlockDirection.North, Vector3Int.one));
        }

        [Test]
        public void 地表が無ければ埋まりではない()
        {
            Assert.IsFalse(PlacementTerrainSinkQuery.IsSunk(new Vector3Int(500, 0, 500), BlockDirection.North, Vector3Int.one));
        }

        private void CreateGroundSlab(Vector3 position, Vector3 scale)
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.layer = LayerConst.GroundLayer;
            slab.transform.position = position;
            slab.transform.localScale = scale;
            Physics.SyncTransforms();
            _slabs.Add(slab);
        }
    }
}
