using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Environment.Terrain.Assets;
using Client.Game.InGame.Environment.Terrain.Build;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Facade;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.UnitTest.Terrain
{
    /// <summary>
    ///     アドレス未解決を例外で落とすことを検証（成功経路の実アセットはPersonalAssets依存でCIに無く、EditModeではロードも完了しない）
    ///     並びを決める側(DetailPrototypeRuntimeConfigCollector)の検証はサーバー側のテストが持つ
    ///     Verifies that an unresolved address throws (the success path needs a PersonalAssets-only asset that CI lacks and EditMode never finishes loading)
    ///     Verifying the side deciding order (DetailPrototypeRuntimeConfigCollector) is covered by the server-side test
    /// </summary>
    // shard割当はクラスと一緒に移動・改名される
    // The shard assignment travels with the class through moves and renames
    [Category("CiShardClientPlay3")]
    public class DetailPrototypeAssetResolverTest
    {
        [Test]
        public void SharedLoaderPreservesOrderAllParametersAndCancellationToken()
        {
            var mesh = new GameObject("DetailMesh");
            var texture = new Texture2D(2, 2);
            using var cancellation = new CancellationTokenSource();
            try
            {
                var assets = new FixtureAssetLoader(mesh, texture);
                var spec = new DetailPrototypeSpec
                {
                    prototypeMeshAddressablePath = "Mesh", usePrototypeMesh = true,
                    renderMode = DetailRenderMode.VertexLit, minWidth = 0.6f, maxWidth = 1.7f,
                    minHeight = 0.8f, maxHeight = 2.3f, noiseSeed = 78, noiseSpread = 0.35f,
                    dryColor = Color.yellow, healthyColor = Color.green, useInstancing = true,
                    alignToGround = 0.75f, positionJitter = 0.4f, targetCoverage = 0.6f,
                    holeEdgePadding = 0.2f, useDensityScaling = false,
                };
                var textureSpec = new DetailPrototypeSpec { usePrototypeMesh = false, prototypeTextureAddressablePath = "Texture" };

                var resolved = DetailPrototypeAssetResolver.ResolveAsync(new[] { spec, textureSpec }, assets, cancellation.Token).GetAwaiter().GetResult();

                // 列順と全描画設定が、ローダー差し替えで失われないことを固定する
                // Changing loaders must preserve column order and every rendering setting
                Assert.That(resolved.Count, Is.EqualTo(2));
                Assert.That(resolved[0].prototype, Is.SameAs(mesh));
                Assert.That(resolved[1].prototypeTexture, Is.SameAs(texture));
                Assert.That(resolved[0].renderMode, Is.EqualTo(spec.renderMode));
                Assert.That(resolved[0].minWidth, Is.EqualTo(spec.minWidth));
                Assert.That(resolved[0].maxWidth, Is.EqualTo(spec.maxWidth));
                Assert.That(resolved[0].minHeight, Is.EqualTo(spec.minHeight));
                Assert.That(resolved[0].maxHeight, Is.EqualTo(spec.maxHeight));
                Assert.That(resolved[0].noiseSeed, Is.EqualTo(spec.noiseSeed));
                Assert.That(resolved[0].noiseSpread, Is.EqualTo(spec.noiseSpread));
                Assert.That(resolved[0].dryColor, Is.EqualTo(spec.dryColor));
                Assert.That(resolved[0].healthyColor, Is.EqualTo(spec.healthyColor));
                Assert.That(resolved[0].useInstancing, Is.EqualTo(spec.useInstancing));
                Assert.That(resolved[0].usePrototypeMesh, Is.True);
                Assert.That(resolved[1].usePrototypeMesh, Is.False);
                Assert.That(resolved[0].alignToGround, Is.EqualTo(spec.alignToGround));
                Assert.That(resolved[0].positionJitter, Is.EqualTo(spec.positionJitter));
                Assert.That(resolved[0].targetCoverage, Is.EqualTo(spec.targetCoverage));
                Assert.That(resolved[0].holeEdgePadding, Is.EqualTo(spec.holeEdgePadding));
                Assert.That(resolved[0].useDensityScaling, Is.EqualTo(spec.useDensityScaling));
                Assert.That(assets.ReceivedToken, Is.EqualTo(cancellation.Token));
                Assert.That(assets.RequestedAddresses, Is.EqualTo(new[] { "Mesh", "Texture" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [UnityTest]
        public IEnumerator ThrowsWhenAPrototypeAssetIsUnresolved()
        {
            // 黙って読み飛ばすとアドレス整備漏れが「草が1本も生えない」形でしか現れず、原因に辿り着けない
            // Silently skipping would surface a missing address only as "no grass at all", leaving no trail to the cause
            var specs = new List<DetailPrototypeSpec>
            {
                new() { usePrototypeMesh = true, prototypeMeshAddressablePath = "Vanilla/Environment/Terrain/Detail/DoesNotExist" },
            };

            // Addressablesの内部ログ(InvalidKeyException)はコルーチン境界をまたいで数フレーム後に出ることがあり、
            // Expectの1件固定では取りこぼして無関係の後続テストを巻き込む。数フレーム抑制してから戻す
            // Addressables' own InvalidKeyException log can land a few frames after the coroutine settles, and a single
            // fixed Expect misses it and drags an unrelated later test down; suppress for a few frames, then restore
            var savedIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                Exception thrown = null;
                yield return DetailPrototypeAssetResolver.ResolveAsync(specs).ToCoroutine(_ => { }, exception => thrown = exception);
                Assert.That(thrown, Is.TypeOf<InvalidOperationException>());

                for (var frame = 0; frame < 5; frame++) yield return null;
            }
            finally
            {
                LogAssert.ignoreFailingMessages = savedIgnoreFailingMessages;
            }
        }

        private sealed class FixtureAssetLoader : ITerrainAssetLoader
        {
            private readonly GameObject _mesh;
            private readonly Texture2D _texture;
            public readonly List<string> RequestedAddresses = new();
            public CancellationToken ReceivedToken;

            public FixtureAssetLoader(GameObject mesh, Texture2D texture)
            {
                _mesh = mesh;
                _texture = texture;
            }

            public UniTask<T> LoadAsync<T>(string address, CancellationToken cancellationToken) where T : UnityEngine.Object
            {
                ReceivedToken = cancellationToken;
                RequestedAddresses.Add(address);
                return UniTask.FromResult(address == "Mesh" ? (T)(UnityEngine.Object)_mesh : (T)(UnityEngine.Object)_texture);
            }
        }
    }
}
