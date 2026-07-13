using NUnit.Framework;
using UnityEngine;
using MapGenerator.Pipeline;
using MapGenerator.Pipeline.Config;
using MapGenerator.Pipeline.Biomes;
using MapGenerator.Pipeline.Spawn;

namespace MapGenerator.Tests.EditMode.Spawn
{
    public class SpawnRegionFinderTests
    {
        static TerrainGenerationConfig MakeConfig(int seed)
        {
            // 生のCreateInstanceではバイオームSO参照（grassland/forest/alpine…）がnullのままで、
            // 段2分類パイプライン(JobDataConverter)がNREになる。TestConfigFactoryは全バイオームSOを
            // 割り当て済みのconfigを返すので、これをベースにテスト対象の2バイオームだけ有効化する。
            var c = TestConfigFactory.Create();
            c.seed = seed;
            // Grassland + Forest のみの世界（plan の MakeConfig が想定した有効バイオーム集合）
            c.grasslandEnabled = true;
            c.forestEnabled = true;
            c.savannaEnabled = false;
            c.desertEnabled = false;
            c.mesaEnabled = false;
            c.alpineEnabled = false;
            c.jungleEnabled = false;
            c.woodsEnabled = false;
            c.gridSizeX = 3; c.gridSizeZ = 3;
            c.terrainWidth = 1000f; c.terrainLength = 1000f;
            c.useSpawnOffsetSearch = true;
            // 軽量化のため低解像度プリセット（段2窓のpxは本番m/pxに従う）
            c.resolutionPreset = TerrainResolutionPreset._256;
            return c;
        }

        [Test]
        public void Find_SameSeed_IsDeterministic()
        {
            var c1 = MakeConfig(160);
            var c2 = MakeConfig(160);
            var bt = TerrainGenerator.GetEnabledBiomeTypesPublic(c1);
            var r1 = SpawnRegionFinder.Find(c1, bt);
            var r2 = SpawnRegionFinder.Find(c2, bt);
            Assert.AreEqual(r1.Success, r2.Success);
            if (r1.Success)
            {
                Assert.AreEqual(r1.SpawnWorldPosition.x, r2.SpawnWorldPosition.x, 0.01f);
                Assert.AreEqual(r1.SpawnWorldPosition.y, r2.SpawnWorldPosition.y, 0.01f);
                Assert.AreEqual(r1.WorldOffset.x, r2.WorldOffset.x, 0.01f);
            }
            Object.DestroyImmediate(c1); Object.DestroyImmediate(c2);
        }

        [Test]
        public void Find_MissingBiome_ReturnsFallback()
        {
            var c = MakeConfig(160);
            c.forestEnabled = false; // Forest無効
            var bt = TerrainGenerator.GetEnabledBiomeTypesPublic(c);
            var r = SpawnRegionFinder.Find(c, bt);
            Assert.IsFalse(r.Success);
            // フォールバックでも spawn はマップ中心(gridCenter)であること（原点(0,0)化を防ぐ回帰固定）。
            // GridCenterWorld と同じ式を再現（halfは整数除算）。
            int halfX = c.gridSizeX / 2;
            int halfZ = c.gridSizeZ / 2;
            float expX = ((-halfX * c.terrainWidth) + ((c.gridSizeX - halfX) * c.terrainWidth)) * 0.5f;
            float expZ = ((-halfZ * c.terrainLength) + ((c.gridSizeZ - halfZ) * c.terrainLength)) * 0.5f;
            Assert.AreEqual(expX, r.SpawnWorldPosition.x, 0.01f);
            Assert.AreEqual(expZ, r.SpawnWorldPosition.y, 0.01f);
            Object.DestroyImmediate(c);
        }

        [Test]
        public void Find_SpawnPoint_IsGrasslandInFinalWinner()
        {
            var c = MakeConfig(160);
            var bt = TerrainGenerator.GetEnabledBiomeTypesPublic(c);
            int grassBi = System.Array.IndexOf(bt, BiomeType.Grassland);
            var r = SpawnRegionFinder.Find(c, bt);
            if (!r.Success) Assert.Ignore("この設定では候補が見つからず予測一致は検証不能");

            var win = TerrainGenerator.RunClassificationDetailed(
                c, bt, r.SpawnWorldPosition.x, r.SpawnWorldPosition.y, 600f);
            // S直下のサンプル点インデックス
            int bx = Mathf.RoundToInt((r.SpawnWorldPosition.x - win.OriginX) / win.PitchX);
            int by = Mathf.RoundToInt((r.SpawnWorldPosition.y - win.OriginZ) / win.PitchZ);
            bx = Mathf.Clamp(bx, 0, win.Resolution - 1);
            by = Mathf.Clamp(by, 0, win.Resolution - 1);
            Assert.AreEqual(grassBi, win.WinnerBiomeIndex[by * win.Resolution + bx]);
            Object.DestroyImmediate(c);
        }
    }
}
