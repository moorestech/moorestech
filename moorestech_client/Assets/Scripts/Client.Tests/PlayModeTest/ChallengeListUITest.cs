using System.Collections;
using Client.Game.InGame.UI.Challenge;
using Client.Tests.PlayModeTest.Util;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Client.Tests.PlayModeTest
{
    /// <summary>
    /// テスト自体はEditModeで実行されるが、実行中にプレイモードに変更する
    /// This test is executed in EditMode, but it switches to PlayMode during execution.
    /// </summary>
    public class ChallengeListUITest
    {
        [UnityTest]
        public IEnumerator CategoryElementTest()
        {
            // テスト中はデバッグオブジェクトの生成を無効化（ドメインリロード後も保持される）
            // Disable debug object creation during test (persists across domain reload).
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", true);

            AssetBundle.UnloadAllAssetBundles(true);

            yield return new EnterPlayMode(expectDomainReload: true);

            // EnterPlayMode時のテストフレームワーク内部エラーでテストが失敗するのを防ぐ
            // Prevent test failure from test framework internal errors during EnterPlayMode.
            LogAssert.ignoreFailingMessages = true;

            yield return Test().ToCoroutine();

            yield return new ExitPlayMode();

            // テスト終了後にデバッグオブジェクト無効化フラグをクリア
            // Clear debug objects disabled flag after test.
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
            
            #region Internal
            
            async UniTask Test()
            {
                await PlayModeTestUtil.LoadMainGame();
                
                var challengeListView = Object.FindFirstObjectByType<ChallengeListView>(FindObjectsInactive.Include);
                var categoryParent = challengeListView.DebugCategoryListParent;
                
                // 子要素（カテゴリの要素）が1個であることを確認
                // Confirm that there is one child element (category element)
                Assert.AreEqual(1, GetCategoryCount(categoryParent));
                
                // アイテムを付与してチャレンジ1を完了
                // Grant an item and complete Challenge 1
                await PlayModeTestUtil.GiveItem("小石", 1);
                
                // カテゴリが増えていないことを確認
                // Confirm that the category has not increased
                Assert.AreEqual(1, GetCategoryCount(categoryParent));
                
                await PlayModeTestUtil.GiveItem("石器", 1);
                
                // 上記のチャレンジクリアによってカテゴリが増えることを確認
                // Confirm that the category increases due to the completion of the above challenge
                Assert.AreEqual(2, GetCategoryCount(categoryParent));
            }
            
            int GetCategoryCount(Transform categoryParent)
            {
                return categoryParent.GetComponentsInChildren<ChallengeListViewCategoryElement>().Length;
            }
            
            #endregion
        }
    }
}
