using System.Collections;
using Client.Game.InGame.Environment.Terrain.Build;
using Cysharp.Threading.Tasks;
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
            _terrainData = new TerrainData { alphamapResolution = AlphamapResolution };
            _terrainLayers = new TerrainLayer[LayerCount];
            for (var index = 0; index < LayerCount; index++) _terrainLayers[index] = new TerrainLayer();
            _terrainData.terrainLayers = _terrainLayers;
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
            var planes = CreatePlanes();

            // 一括適用でロード画面を止めず、少なくとも1度は次フレームへ制御を返す
            // Return control to a later frame at least once instead of stalling the loading screen with one bulk apply
            var applyTask = TerrainAlphamapApplier.ApplyAsync(_terrainData, planes, AlphamapResolution);
            Assert.That(applyTask.Status, Is.EqualTo(UniTaskStatus.Pending));
            yield return applyTask.ToCoroutine();

            var appliedAlphamap = _terrainData.GetAlphamaps(0, 0, AlphamapResolution, AlphamapResolution);

            // 行の前後を照合し、平面のバイト列とalphamapのz行の向きが揃っていることを見る
            // Compare both ends of the rows to confirm the plane's byte order and the alphamap's z rows point the same way
            Assert.That(appliedAlphamap[0, 0, 0], Is.EqualTo(64 / 255f).Within(0.005f));
            Assert.That(appliedAlphamap[0, 0, 1], Is.EqualTo(191 / 255f).Within(0.005f));
            Assert.That(appliedAlphamap[63, 0, 0], Is.EqualTo(192 / 255f).Within(0.005f));
            Assert.That(appliedAlphamap[63, 0, 1], Is.EqualTo(63 / 255f).Within(0.005f));

            // 平面1以降へ触れていないことを、載せなかった層が0のままであることで見る
            // The untouched later planes show up as layers that stayed at zero
            Assert.That(appliedAlphamap[0, 0, 4], Is.EqualTo(0f).Within(0.005f));
        }

        [Test]
        public void ApplyAsync_平面数がテクスチャ数と食い違えば落とす()
        {
            // 数が合わないまま載せると、余った層が既定値のまま描かれて原因の分からない見た目になる
            // Uploading a mismatched count leaves the surplus layers at their defaults and yields an unexplainable look
            var applyTask = TerrainAlphamapApplier.ApplyAsync(_terrainData, new[] { new byte[AlphamapResolution * AlphamapResolution * 4] }, AlphamapResolution);
            Assert.That(applyTask.Status, Is.EqualTo(UniTaskStatus.Faulted));
            var thrownException = Assert.Throws<System.InvalidOperationException>(() => applyTask.GetAwaiter().GetResult());
            Assert.That(thrownException.Message,
                Does.Contain("5 alphamap textures").And.Contain("1 planes were baked"));
        }

        // 平面0のRへ行値、Gへ補数を格納
        // Stores the row value in plane 0 R and its complement in G.
        private static byte[][] CreatePlanes()
        {
            var planes = new byte[5][];
            for (var planeIndex = 0; planeIndex < planes.Length; planeIndex++)
                planes[planeIndex] = new byte[AlphamapResolution * AlphamapResolution * 4];

            for (var z = 0; z < AlphamapResolution; z++)
            for (var x = 0; x < AlphamapResolution; x++)
            {
                var offset = (z * AlphamapResolution + x) * 4;
                planes[0][offset] = (byte)(z < 32 ? 64 : 192);
                planes[0][offset + 1] = (byte)(z < 32 ? 191 : 63);
            }

            return planes;
        }
    }
}
