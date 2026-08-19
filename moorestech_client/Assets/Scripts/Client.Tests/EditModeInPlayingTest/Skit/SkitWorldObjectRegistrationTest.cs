using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.Context;
using CommandForgeGenerator.Command;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Skit
{
    /// <summary>
    /// テスト自体はEditModeで実行されるが、実行中にプレイモードに変更する
    /// スキットが世界を隠す束から、シーン上の世界オブジェクトが1つも漏れていないことを本番のDIコンテナで検証する
    /// This test runs in EditMode but switches to PlayMode during execution.
    /// Verifies against the production DI container that no scene world object is missing from the bundle a skit hides.
    /// </summary>
    public class SkitWorldObjectRegistrationTest
    {
        [UnityTest]
        public IEnumerator EveryWorldObjectComponentIsRegisteredAsSkitWorldObjectControl()
        {
            EnterPlayModeUtil();

            // yield return new EnterPlayMode　は必ず[UnityTest]関数の直下で呼び出すこと。そうでないとなぜかわからないがプレイモードに入らない
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function. Otherwise, for unknown reasons, it will not enter PlayMode.
            yield return new EnterPlayMode(expectDomainReload: true);

            // EnterPlayMode時のテストフレームワーク内部エラーでテストが失敗するのを防ぐ
            // Prevent test failure from test framework internal errors during EnterPlayMode.
            LogAssert.ignoreFailingMessages = true;

            yield return Body().ToCoroutine();

            yield return new ExitPlayMode();

            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask Body()
            {
                await LoadMainGame();

                // SkitManagerが実際に注入されるコンテナから解決する。別に組んだコンテナでは登録漏れを検出できない
                // Resolve from the very container injected into SkitManager; a separately built container would not detect a missing registration
                var resolver = ClientDIContext.DIContainer.DIContainerResolver;
                var registeredTypes = resolver.Resolve<IReadOnlyList<ISkitWorldObjectControl>>().Select(control => control.GetType()).ToList();

                // 件数assertでは5本目を足して登録し忘れたケースを通してしまうので、実装型を列挙して全数を突き合わせる
                // A count assertion would pass a forgotten fifth registration, so every implementation type is enumerated and matched
                foreach (var implementationType in CollectSceneWorldObjectTypes())
                    CollectionAssert.Contains(registeredTypes, implementationType, $"{implementationType.Name}がスキットの世界非表示対象としてDI登録されていない");
            }

            // シーン上のコンポーネント実装だけが束の母集団。SkitManagerが毎回生成する束ね役・台帳はDI登録の対象外
            // Only scene component implementations form the bundle; the grouping and ledger SkitManager creates per skit are not DI-registered
            IReadOnlyList<Type> CollectSceneWorldObjectTypes()
            {
                var declaringAssemblyName = typeof(ISkitWorldObjectControl).Assembly.GetName().Name;
                var implementationTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => assembly.GetName().Name == declaringAssemblyName || assembly.GetReferencedAssemblies().Any(reference => reference.Name == declaringAssemblyName))
                    .Where(IsProductionAssembly)
                    .SelectMany(assembly => assembly.GetTypes())
                    .Where(type => !type.IsAbstract && typeof(MonoBehaviour).IsAssignableFrom(type) && typeof(ISkitWorldObjectControl).IsAssignableFrom(type))
                    .ToList();

                // 走査が空振りしたまま素通りすると、上のassertが名目だけになる
                // A scan that silently finds nothing would reduce the assertion above to a formality
                Assert.IsNotEmpty(implementationTypes, "プロダクション実装の走査が1件も拾えていない");
                return implementationTypes;
            }

            // テストアセンブリのダミー実装が期待集合を汚染するのを防ぐ
            // Keeps the test assembly's dummy implementations from polluting the expected set
            bool IsProductionAssembly(Assembly assembly)
            {
                return assembly.GetReferencedAssemblies().All(reference => reference.Name != "nunit.framework");
            }

            #endregion
        }
    }
}
