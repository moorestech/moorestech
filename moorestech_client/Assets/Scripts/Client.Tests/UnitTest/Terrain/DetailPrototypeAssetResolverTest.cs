using System;
using System.Collections;
using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain.Build;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Facade;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.UnitTest.Terrain
{
    /// <summary>
    ///     detailプロトタイプ仕様のアドレスをAddressablesで解決し、値をUnity DetailPrototypeへ写すことを検証する。
    ///     並びを決める側(DetailPrototypeSpecCollector)の検証はサーバー側のDetailPrototypeSpecCollectorTestが持つ
    ///     Verifies a detail prototype spec's address resolves via Addressables and its values land on the Unity DetailPrototype;
    ///     the side deciding the order (DetailPrototypeSpecCollector) is covered by the server-side DetailPrototypeSpecCollectorTest
    /// </summary>
    public class DetailPrototypeAssetResolverTest
    {
        // 実プロジェクトに登録済みのメッシュDetailアドレス。解決の成功経路を偽物のアドレスなしで固定する
        // A mesh detail address actually registered in the project, pinning the success path without a fake address
        private const string RealMeshAddress = "Vanilla/Environment/Terrain/Detail/Mountains/Daisy";

        [UnityTest]
        public IEnumerator ResolvesAMeshPrototypeAndCopiesItsValues()
        {
            var specs = new List<DetailPrototypeSpec>
            {
                new()
                {
                    usePrototypeMesh = true,
                    prototypeMeshAddressablePath = RealMeshAddress,
                    renderMode = DetailRenderMode.VertexLit,
                    minWidth = 1f,
                    maxWidth = 2f,
                },
            };

            List<DetailPrototype> prototypes = null;
            yield return DetailPrototypeAssetResolver.ResolveAsync(specs).ToCoroutine(result => prototypes = result);

            Assert.That(prototypes.Count, Is.EqualTo(1));
            Assert.That(prototypes[0].usePrototypeMesh, Is.True);
            Assert.That(prototypes[0].prototype, Is.Not.Null);
            Assert.That(prototypes[0].minWidth, Is.EqualTo(1f));
            Assert.That(prototypes[0].maxWidth, Is.EqualTo(2f));
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
    }
}
