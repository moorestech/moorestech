using System;
using System.Collections;
using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain.Build;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Facade;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Client.Tests.UnitTest.Terrain
{
    /// <summary>
    ///     アドレス未解決を例外で落とすことを検証（成功経路の実アセットはPersonalAssets依存でCIに無く、EditModeではロードも完了しない）
    ///     並びを決める側(DetailPrototypeRuntimeConfigCollector)の検証はサーバー側のテストが持つ
    ///     Verifies that an unresolved address throws (the success path needs a PersonalAssets-only asset that CI lacks and EditMode never finishes loading)
    ///     Verifying the side deciding order (DetailPrototypeRuntimeConfigCollector) is covered by the server-side test
    /// </summary>
    public class DetailPrototypeAssetResolverTest
    {
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
