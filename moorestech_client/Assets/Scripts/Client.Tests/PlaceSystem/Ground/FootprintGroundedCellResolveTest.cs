using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Ground
{
    // 地形ゲート付き4設置系のカーソルセルが照準点でなく占有セルの地形最高点でYを決めることを検証する
    // Verify the terrain-gated placements' cursor cell takes Y from the footprint terrain max, not the aim point
    public class FootprintGroundedCellResolveTest
    {
        private static readonly Vector3Int PoleSize = new(3, 4, 3);
        private readonly List<GameObject> _slabs = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var slab in _slabs) Object.DestroyImmediate(slab);
            _slabs.Clear();
        }

        [Test]
        public void GroundHitTakesFootprintMaxInsteadOfAimPoint()
        {
            // X方向に3セルで1m上がる斜面。占有範囲x=500〜503の地表は約20.3〜21.3
            // A slope rising 1m over 3 cells along X; ground over x=500..503 spans about 20.3..21.3
            CreateSlope(new Vector3(501.5f, 20.8f, 600.5f), 1f / 3f);
            var result = PlaceSystemUtil.ResolveFootprintGroundedCell(new Vector3Int(500, 20, 599), PlacementHitSurfaceKind.Ground, BlockDirection.North, PoleSize, 0, out var resolved);

            Assert.AreEqual(FootprintGroundedCellResult.Resolved, result);
            Assert.AreEqual(new Vector3Int(500, 21, 599), resolved);
        }

        [Test]
        public void HeightOffsetIsAddedAfterFootprintResolution()
        {
            CreateSlope(new Vector3(701.5f, 20.8f, 800.5f), 1f / 3f);
            var result = PlaceSystemUtil.ResolveFootprintGroundedCell(new Vector3Int(700, 21, 799), PlacementHitSurfaceKind.Ground, BlockDirection.North, PoleSize, 1, out var resolved);

            Assert.AreEqual(FootprintGroundedCellResult.Resolved, result);
            Assert.AreEqual(22, resolved.y);
        }

        [Test]
        public void BlockFaceHitKeepsTheRayCell()
        {
            // 積み重ねはレイのセルのまま。下に地面があっても動かさない
            // Stacking keeps the ray cell even with ground below
            CreateSlope(new Vector3(901.5f, 20.8f, 1000.5f), 1f / 3f);
            var rayCell = new Vector3Int(900, 25, 999);
            var result = PlaceSystemUtil.ResolveFootprintGroundedCell(rayCell, PlacementHitSurfaceKind.BlockFace, BlockDirection.North, PoleSize, 0, out var resolved);

            Assert.AreEqual(FootprintGroundedCellResult.Resolved, result);
            Assert.AreEqual(rayCell, resolved);
        }

        [Test]
        public void MissingFootprintGroundReportsGroundNotFound()
        {
            var result = PlaceSystemUtil.ResolveFootprintGroundedCell(new Vector3Int(4000, 7, 4000), PlacementHitSurfaceKind.Ground, BlockDirection.North, PoleSize, 0, out _);
            Assert.AreEqual(FootprintGroundedCellResult.GroundNotFound, result);
        }

        private void CreateSlope(Vector3 surfaceCenter, float rise)
        {
            // 厚さ2の箱をZ軸回りに傾け、上面がsurfaceCenterを通りX方向へ1mあたりriseで上がるよう置く
            // Tilt a 2-thick box about Z so its top passes surfaceCenter and rises by `rise` per meter along X
            var rotation = Quaternion.Euler(0f, 0f, Mathf.Atan(rise) * Mathf.Rad2Deg);
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.layer = LayerConst.GroundLayer;
            slab.transform.localScale = new Vector3(20f, 2f, 20f);
            slab.transform.SetPositionAndRotation(surfaceCenter - rotation * Vector3.up, rotation);
            Physics.SyncTransforms();
            _slabs.Add(slab);
        }
    }
}
