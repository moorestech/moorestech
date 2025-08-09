using System;
using System.Threading.Tasks;
using Client.Game.InGame.UI.Challenge;
using Client.PlayModeTests.Util;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.PlayModeTests
{
    public class PlayModeTest
    {
        [Test]
        public async Task  NewTestScriptSimplePasses()
        {
            await PlayModeTestUtil.LoadMainGame();
            
            // Wait 1 sec
            await UniTask.Delay(TimeSpan.FromSeconds(1));
            
            var challengeListView = Object.FindFirstObjectByType<ChallengeListView>(FindObjectsInactive.Include);
            var categoryParent = challengeListView.DebugCategoryListParent;
            
            // 子要素（カテゴリの要素）が1個であることを確認
            // Confirm that there is one child element (category element)
            Assert.AreEqual(1, categoryParent.childCount);
            
            // アイテムを付与してチャレンジ1を完了
            // Grant an item and complete Challenge 1
            await PlayModeTestUtil.GiveItem("小石", 1);

            // カテゴリが増えていないことを確認
            // Confirm that the category has not increased
            Assert.AreEqual(1, categoryParent.childCount);
            
            await PlayModeTestUtil.GiveItem("石器", 1);
            
            // 上記のチャレンジクリアによってカテゴリが増えることを確認
            // Confirm that the category increases due to the completion of the above challenge
            Assert.AreEqual(2, categoryParent.childCount);

        }
    }
}
