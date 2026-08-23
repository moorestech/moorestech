using System.Collections;
using Client.Game.InGame.Environment.Terrain.Build;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Facade;
using Game.MapGeneration.Pipeline.Visual;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.UnitTest.Terrain
{
    /// <summary>
    ///     RGBA8平面の直接適用を検証
    ///     層はUnityのテクスチャ4枚組へ畳まれるため、平面と channel の対応が崩れると別のレイヤーが塗られる
    ///     Verifies direct RGBA8-plane upload.
    ///     Layers fold into Unity's four-per-texture groups, so a broken plane-and-channel mapping paints another layer
    /// </summary>
    public class TerrainAlphamapApplierTest
    {
        private const int AlphamapResolution = 64;

        // 4の倍数から1つ外し、端数の層が最後の平面へ載ることまで含めて確かめる
        // One off a multiple of four, so the trailing layers landing on the last plane are covered too
        private const int LayerCount = 19;

        private TerrainData _terrainData;
        private TerrainLayer[] _terrainLayers;

        [SetUp]
        public void SetUp()
        {
            _terrainData = new TerrainData();
            _terrainLayers = new TerrainLayer[LayerCount];
            for (var index = 0; index < LayerCount; index++) _terrainLayers[index] = new TerrainLayer();
        }

        [TearDown]
        public void TearDown()
        {
            if (_terrainData != null) Object.DestroyImmediate(_terrainData);
            if (_terrainLayers == null) return;
            foreach (var terrainLayer in _terrainLayers) Object.DestroyImmediate(terrainLayer);
        }

        [UnityTest]
        public IEnumerator ApplyAsync_平面をalphamapへ載せ複数フレームへ分散する()
        {
            var alphamap = TileAlphamap.Create(CreatePlanes(), AlphamapResolution, LayerCount);

            // 一括適用でロード画面を止めず、少なくとも1度は次フレームへ制御を返す
            // Return control to a later frame at least once instead of stalling the loading screen with one bulk apply
            var applyTask = TerrainAlphamapApplier.ApplyAsync(_terrainData, _terrainLayers, CreateTile(alphamap));
            Assert.That(applyTask.Status, Is.EqualTo(UniTaskStatus.Pending));
            yield return applyTask.ToCoroutine();

            var appliedAlphamap = _terrainData.GetAlphamaps(0, 0, AlphamapResolution, AlphamapResolution);

            // 非既定の解像度と19層を実際に確保し、全5平面が南東端まで届くことを読む
            // Observe the nondefault resolution and 19 layers with all five planes reaching the south-east edge
            Assert.That(_terrainData.alphamapResolution, Is.EqualTo(AlphamapResolution));
            Assert.That(_terrainData.terrainLayers.Length, Is.EqualTo(LayerCount));
            Assert.That(appliedAlphamap[63, 63, 0], Is.EqualTo(40 / 255f).Within(0.005f));
            Assert.That(appliedAlphamap[63, 63, 4], Is.EqualTo(70 / 255f).Within(0.005f));
            Assert.That(appliedAlphamap[63, 63, 8], Is.EqualTo(100 / 255f).Within(0.005f));
            Assert.That(appliedAlphamap[63, 63, 12], Is.EqualTo(130 / 255f).Within(0.005f));
            Assert.That(appliedAlphamap[63, 63, 16], Is.EqualTo(160 / 255f).Within(0.005f));
            Assert.That(appliedAlphamap[63, 63, 18], Is.EqualTo(220 / 255f).Within(0.005f));
        }

        [Test]
        public void ApplyAsync_レイヤー数が食い違えばTerrainDataを変えずに落とす()
        {
            _terrainData.alphamapResolution = 32;
            _terrainData.terrainLayers = _terrainLayers;
            var initialWeight = _terrainData.GetAlphamaps(0, 0, 32, 32)[0, 0, 0];
            var alphamap = TileAlphamap.Create(
                new[] { new byte[AlphamapResolution * AlphamapResolution * 4] }, AlphamapResolution, 1);
            var applyTask = TerrainAlphamapApplier.ApplyAsync(_terrainData, System.Array.Empty<TerrainLayer>(), CreateTile(alphamap));
            Assert.That(applyTask.Status, Is.EqualTo(UniTaskStatus.Faulted));
            var thrownException = Assert.Throws<System.InvalidOperationException>(() => applyTask.GetAwaiter().GetResult());
            Assert.That(thrownException.Message,
                Does.Contain("0 terrain layers were resolved").And.Contain("baked for 1"));

            // 失敗は割り当てより前なので、既存の解像度・層表・画素を観測しても変化がない
            // Failure occurs before assignment, leaving the observable resolution, layers, and pixels unchanged
            Assert.That(_terrainData.alphamapResolution, Is.EqualTo(32));
            Assert.That(_terrainData.terrainLayers.Length, Is.EqualTo(LayerCount));
            Assert.That(_terrainData.GetAlphamaps(0, 0, 32, 32)[0, 0, 0], Is.EqualTo(initialWeight));
        }

        // 各平面の南東端へ別の値を置き、最後の端数チャンネルも明示する
        // Put distinct south-east values in every plane and state the final remainder channel explicitly
        private static byte[][] CreatePlanes()
        {
            var planes = new byte[5][];
            for (var planeIndex = 0; planeIndex < planes.Length; planeIndex++)
                planes[planeIndex] = new byte[AlphamapResolution * AlphamapResolution * 4];

            var southEastOffset = (AlphamapResolution * AlphamapResolution - 1) * 4;
            for (var planeIndex = 0; planeIndex < planes.Length; planeIndex++)
                planes[planeIndex][southEastOffset] = (byte)(40 + planeIndex * 30);
            planes[4][southEastOffset + 2] = 220;

            return planes;
        }

        private static BakedTerrainTile CreateTile(TileAlphamap alphamap)
        {
            return new BakedTerrainTile(Vector3.zero, new float[1, 1], alphamap, System.Array.Empty<int[,]>());
        }
    }
}
