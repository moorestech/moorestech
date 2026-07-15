using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using MapGenerator.Pipeline;
using MapGenerator.Pipeline.Config;

namespace MapGenerator.Tests.EditMode
{
    /// <summary>
    /// DefaultConfigを使った決定論的生成の回帰テスト。
    /// DefaultConfigは調整中に値が頻繁に変わるため、固定ハッシュではなく
    /// 同一条件の2回生成が一致することを検証する。
    /// </summary>
    [TestFixture]
    public class RefactoringRegressionTests
    {
        const string ConfigAssetPath = "Assets/MapGenerator/Presets/DefaultConfig.asset";

        TerrainGenerationResult _first;
        TerrainGenerationResult _second;

        [OneTimeSetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;

            var asset = AssetDatabase.LoadAssetAtPath<TerrainGenerationConfig>(ConfigAssetPath);
            Assert.IsNotNull(asset, $"Configアセットが見つかりません: {ConfigAssetPath}");

            var config = Object.Instantiate(asset);
            config.resolutionPreset = TerrainResolutionPreset._256;
            config.overrideResolution = 0;

            _first = TerrainGenerator.Generate(config);
            _second = TerrainGenerator.Generate(config);
            Object.DestroyImmediate(config);
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void Heightmap_IsDeterministic()
        {
            Assert.IsNotNull(_first.Heights, "1回目のHeights配列がnull");
            Assert.IsNotNull(_second.Heights, "2回目のHeights配列がnull");
            Assert.AreEqual(ComputeFloatArraySha256(_first.Heights), ComputeFloatArraySha256(_second.Heights),
                "同一DefaultConfigの2回生成でハイトマップが一致しない");
        }

        [Test]
        public void TreeCount_IsDeterministic()
        {
            Assert.AreEqual(_first.TreeInstances?.Length ?? 0, _second.TreeInstances?.Length ?? 0,
                "同一DefaultConfigの2回生成で樹木数が一致しない");
        }

        [Test]
        public void TreeData_IsDeterministic()
        {
            var firstHash = ComputeTreeSha256(_first.TreeInstances);
            var secondHash = ComputeTreeSha256(_second.TreeInstances);
            Assert.AreEqual(firstHash, secondHash,
                "同一DefaultConfigの2回生成で樹木配置データが一致しない");
        }

        [Test]
        public void Splatmap_IsDeterministic()
        {
            Assert.AreEqual(ComputeSplatmapSha256(_first.Splatmap), ComputeSplatmapSha256(_second.Splatmap),
                "同一DefaultConfigの2回生成でSplatmapが一致しない");
        }

        [Test]
        public void ObjectCount_IsDeterministic()
        {
            int firstCount = _first.ObjectPlacements?.Count ?? 0;
            int secondCount = _second.ObjectPlacements?.Count ?? 0;
            Assert.AreEqual(firstCount, secondCount,
                "同一DefaultConfigの2回生成でオブジェクト数が一致しない");
        }

        [Test]
        public void ObjectData_IsDeterministic()
        {
            Assert.AreEqual(ComputeObjectSha256(_first.ObjectPlacements), ComputeObjectSha256(_second.ObjectPlacements),
                "同一DefaultConfigの2回生成でオブジェクト配置データが一致しない");
        }

        [Test]
        public void HeightSamples_AreDeterministic()
        {
            Assert.AreEqual(_first.Heights[0], _second.Heights[0], 1e-7f, "H[0]");
            Assert.AreEqual(_first.Heights[1], _second.Heights[1], 1e-7f, "H[1]");
            Assert.AreEqual(_first.Heights[2], _second.Heights[2], 1e-7f, "H[2]");
            Assert.AreEqual(_first.Heights[3], _second.Heights[3], 1e-7f, "H[3]");
            Assert.AreEqual(_first.Heights[4], _second.Heights[4], 1e-7f, "H[4]");
        }

        static string ComputeTreeSha256(TreeInstance[] trees)
        {
            if (trees == null) return ComputeSha256(System.Array.Empty<byte>());
            var bytes = new byte[trees.Length * 20];
            for (int i = 0; i < trees.Length; i++)
            {
                var t = trees[i];
                var data = new float[]
                    { t.position.x, t.position.y, t.position.z, t.widthScale, t.heightScale };
                System.Buffer.BlockCopy(data, 0, bytes, i * 20, 20);
            }
            return ComputeSha256(bytes);
        }

        static string ComputeSplatmapSha256(float[,,] splatmap)
        {
            if (splatmap == null) return ComputeSha256(System.Array.Empty<byte>());
            int len = splatmap.Length;
            var bytes = new byte[len * 4];
            int idx = 0;
            foreach (float v in splatmap)
            {
                System.Buffer.BlockCopy(System.BitConverter.GetBytes(v), 0, bytes, idx * 4, 4);
                idx++;
            }
            return ComputeSha256(bytes);
        }

        static string ComputeObjectSha256(System.Collections.Generic.List<ObjectPlacementResult> objects)
        {
            int count = objects?.Count ?? 0;
            var bytes = new byte[count * 12];
            for (int i = 0; i < count; i++)
            {
                var p = objects[i].Position;
                System.Buffer.BlockCopy(new float[] { p.x, p.y, p.z }, 0, bytes, i * 12, 12);
            }
            return ComputeSha256(bytes);
        }

        static string ComputeFloatArraySha256(float[] data)
        {
            var bytes = new byte[data.Length * 4];
            System.Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
            return ComputeSha256(bytes);
        }

        static string ComputeSha256(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(data);
                return System.BitConverter.ToString(hash).Replace("-", "");
            }
        }
    }
}
