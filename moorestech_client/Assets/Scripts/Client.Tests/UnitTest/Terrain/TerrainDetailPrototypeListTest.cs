using System;
using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain.Build;
using Game.MapGeneration.Facade;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UnitTest.Terrain
{
    /// <summary>
    ///     プロトタイプ仕様と解決済みアセット辞書からUnity DetailPrototypeを組み立てる変換を検証する。
    ///     並びを決める側(DetailPrototypeSpecCollector)の検証はサーバー側のDetailPrototypeSpecCollectorTestが持つ
    ///     Verifies the conversion assembling Unity DetailPrototypes from prototype specs and a resolved asset dictionary;
    ///     the side deciding the order (DetailPrototypeSpecCollector) is covered by the server-side DetailPrototypeSpecCollectorTest
    /// </summary>
    public class TerrainDetailPrototypeListTest
    {
        private const string TextureAddress = "addr/grassTex";

        [Test]
        public void BuildsOneDetailPrototypePerSpecInOrder()
        {
            var specs = new List<DetailPrototypeSpec>
            {
                CreateSpec(1f),
                CreateSpec(2f),
            };
            var resolvedAssets = new Dictionary<string, UnityEngine.Object> { [TextureAddress] = new Texture2D(1, 1) };

            var prototypes = TerrainDetailPrototypeList.Build(specs, resolvedAssets);

            Assert.That(prototypes.Count, Is.EqualTo(2));
            Assert.That(prototypes[0].minWidth, Is.EqualTo(1f));
            Assert.That(prototypes[1].minWidth, Is.EqualTo(2f));
        }

        [Test]
        public void ThrowsWhenAPrototypeAssetIsUnresolved()
        {
            // 黙って読み飛ばすとアドレス整備漏れが「草が1本も生えない」形でしか現れず、原因に辿り着けない
            // Silently skipping would surface a missing address only as "no grass at all", leaving no trail to the cause
            var specs = new List<DetailPrototypeSpec> { CreateSpec(1f) };
            var emptyResolvedAssets = new Dictionary<string, UnityEngine.Object>();

            Assert.Throws<InvalidOperationException>(() => TerrainDetailPrototypeList.Build(specs, emptyResolvedAssets));
        }

        private static DetailPrototypeSpec CreateSpec(float minWidth)
        {
            return new DetailPrototypeSpec
            {
                usePrototypeMesh = false,
                prototypeTextureAddressablePath = TextureAddress,
                renderMode = DetailRenderMode.Grass,
                minWidth = minWidth,
            };
        }
    }
}
