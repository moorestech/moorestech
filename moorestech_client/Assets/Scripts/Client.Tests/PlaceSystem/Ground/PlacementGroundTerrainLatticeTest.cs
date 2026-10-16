using System;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Game.Block.Interface;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Surface;
using Game.MapGeneration.Surface;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Tests.PlaceSystem.Ground
{
    // 実TerrainDataの16bit格子で整数を僅かに下回る地表（v5鉱脈パッド）に設置セルが沈まないことを検証する
    // Verify that placement cells do not sink on a surface just under an integer on a real TerrainData 16-bit lattice (v5 vein pad)
    public class PlacementGroundTerrainLatticeTest
    {
        private const float TerrainHeightRange = 600f;
        private const int PadIntegerY = 10;
        private static readonly Vector3 TerrainOrigin = new(3000f, 0f, 3000f);

        private TerrainData _terrainData;
        private GameObject _terrainObject;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_terrainObject);
            Object.DestroyImmediate(_terrainData);
        }

        [Test]
        public void 格子で整数を下回るパッド上の設置セルは整数Yになる()
        {
            var padUnits = (int)Math.Floor(PadIntegerY / TerrainHeightRange * TerrainHeightStorage.Steps);
            var padHeight = CreatePadTerrain(padUnits);

            // 前提: 格納された地表は整数を1段未満だけ下回る
            // Premise: the stored surface is under the integer by less than one step
            var step = TerrainHeightRange / TerrainHeightStorage.Steps;
            Assert.That(padHeight, Is.LessThan((float)PadIntegerY));
            Assert.That(PadIntegerY - padHeight, Is.LessThan(step));

            // 地表探査は格納された地表高をそのまま返す
            // Ground probing returns the stored surface height as is
            var cell = new Vector3Int(3010, 0, 3010);
            Assert.IsTrue(GroundHeightProbe.TryGetFootprintMaxGroundHeight(cell, BlockDirection.North, new Vector3Int(2, 1, 2), out var groundHeight));
            Assert.AreEqual(padHeight, groundHeight, 1e-4f);

            Assert.IsTrue(PlacementGroundCellResolver.TryResolveCellFromGround(cell, BlockDirection.North, new Vector3Int(2, 1, 2), 0, out var resolved));
            Assert.AreEqual(PadIntegerY, resolved.y);
        }

        // 生産のPadHeightが作る最悪ケース（整数を1段＋クリアランスだけ下回る）でも実TerrainDataのRaycast経由で整数セルになる
        // Even the worst case production PadHeight makes (one step + clearance under the integer) resolves to the integer cell via a real TerrainData raycast
        [TestCase(5)]
        [TestCase(305)]
        public void 生産PadHeightのパッド上の設置セルは採掘底面Yになる(int boxBottom)
        {
            var config = new TerrainGenerationConfig { terrainHeight = TerrainHeightRange };
            var productionPadHeight = SurfaceQuantization.PadHeight(boxBottom, config, "fixture");
            var padHeight = CreatePadTerrain(Mathf.RoundToInt(productionPadHeight / TerrainHeightRange * TerrainHeightStorage.Steps));

            // 前提: 格納された地表は整数を1段より多く下回る
            // Premise: the stored surface is more than one step under the integer
            var step = TerrainHeightRange / TerrainHeightStorage.Steps;
            Assert.AreEqual(productionPadHeight, padHeight, 1e-4f);
            Assert.That(boxBottom - productionPadHeight, Is.GreaterThan(step));

            var cell = new Vector3Int(3010, 0, 3010);
            Assert.IsTrue(PlacementGroundCellResolver.TryResolveCellFromGround(cell, BlockDirection.North, new Vector3Int(2, 1, 2), 0, out var resolved));
            Assert.AreEqual(boxBottom, resolved.y);
        }

        [Test]
        public void 地面ヒットの設置点も格子1段下の地表を整数セルへ置く()
        {
            var step = TerrainHeightRange / TerrainHeightStorage.Steps;
            var hitPoint = new Vector3(5.3f, PadIntegerY - 0.9f * step, 5.7f);

            var resolved = PlaceSystemUtil.CalcPlacePointBySize(Vector3Int.one, hitPoint, 0, (PreviewSurfaceType?)null);
            Assert.AreEqual(new Vector3Int(5, PadIntegerY, 5), resolved);
        }

        // 全面を指定の格納段で埋めた地形を作り、Raycastで測った地表高を返す
        // Builds a terrain filled with the given storage step and returns the raycast surface height
        private float CreatePadTerrain(int padUnits)
        {
            _terrainData = new TerrainData { heightmapResolution = 33 };
            _terrainData.size = new Vector3(32f, TerrainHeightRange, 32f);
            var heights = new float[33, 33];
            for (var z = 0; z < 33; z++)
            for (var x = 0; x < 33; x++)
                heights[z, x] = padUnits / (float)TerrainHeightStorage.Steps;
            _terrainData.SetHeights(0, 0, heights);

            _terrainObject = Terrain.CreateTerrainGameObject(_terrainData);
            _terrainObject.layer = LayerConst.GroundLayer;
            _terrainObject.transform.position = TerrainOrigin;
            Physics.SyncTransforms();

            Assert.IsTrue(GroundHeightProbe.TryGetGroundPoint(3016f, 3016f, out var padPoint));
            return padPoint.y;
        }

    }
}
