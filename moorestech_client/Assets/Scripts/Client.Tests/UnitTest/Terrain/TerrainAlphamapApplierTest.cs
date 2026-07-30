using System.Collections;
using Client.Game.InGame.Environment.Terrain.Build;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.UnitTest.Terrain
{
    public class TerrainAlphamapApplierTest
    {
        private const int AlphamapResolution = 128;
        private TerrainData _terrainData;
        private TerrainLayer _firstTerrainLayer;
        private TerrainLayer _secondTerrainLayer;

        [TearDown]
        public void TearDown()
        {
            if (_terrainData != null) Object.DestroyImmediate(_terrainData);
            if (_firstTerrainLayer != null) Object.DestroyImmediate(_firstTerrainLayer);
            if (_secondTerrainLayer != null) Object.DestroyImmediate(_secondTerrainLayer);
        }

        [UnityTest]
        public IEnumerator ApplyAsync_大きいAlphamapを複数フレームへ分散する()
        {
            _terrainData = new TerrainData { alphamapResolution = AlphamapResolution };
            _firstTerrainLayer = new TerrainLayer();
            _secondTerrainLayer = new TerrainLayer();
            _terrainData.terrainLayers = new[] { _firstTerrainLayer, _secondTerrainLayer };
            var alphamap = CreateTwoLayerAlphamap();

            // 一括適用でロード画面を止めず、少なくとも1度は次フレームへ制御を返す
            // Return control to a later frame at least once instead of stalling the loading screen with one bulk apply
            var applyTask = TerrainAlphamapApplier.ApplyAsync(_terrainData, alphamap);
            Assert.That(applyTask.Status, Is.EqualTo(UniTaskStatus.Pending));
            yield return applyTask.ToCoroutine();

            // 64行チャンクの前後と末尾を照合し、コピー元・適用先の行ずれを検出する
            // Compare both sides of the 64-row boundary and the final row to detect source or destination offset mistakes
            var appliedAlphamap = _terrainData.GetAlphamaps(0, 0, AlphamapResolution, AlphamapResolution);
            Assert.That(appliedAlphamap[63, 127, 0], Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(appliedAlphamap[64, 127, 0], Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(appliedAlphamap[127, 127, 0], Is.EqualTo(0.75f).Within(0.001f));
        }

        private static float[,,] CreateTwoLayerAlphamap()
        {
            var alphamap = new float[AlphamapResolution, AlphamapResolution, 2];
            for (var z = 0; z < AlphamapResolution; z++)
            for (var x = 0; x < AlphamapResolution; x++)
            {
                alphamap[z, x, 0] = z < 64 ? 0.25f : 0.75f;
                alphamap[z, x, 1] = z < 64 ? 0.75f : 0.25f;
            }
            return alphamap;
        }
    }
}
