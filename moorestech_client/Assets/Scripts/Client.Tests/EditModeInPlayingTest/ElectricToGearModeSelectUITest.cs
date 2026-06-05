using System.Collections;
using System.Threading;
using Client.Common.Asset;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Block;
using Client.Tests.EditModeInPlayingTest.Util;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.ElectricToGear;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    /// <summary>
    /// テスト自体はEditModeで実行されるが、実行中にプレイモードに変更する
    /// This test is executed in EditMode, but it switches to PlayMode during execution.
    /// </summary>
    public class ElectricToGearModeSelectUITest
    {
        private const string BlockName = "TestElectricToGearGeneratorUI";
        private const string UiAddress = "Vanilla/UI/Block/ElectricToGearBlockInventory";

        [UnityTest]
        public IEnumerator PrefabLoadsAndHasRootView()
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

            // テスト終了後にデバッグオブジェクト無効化フラグをクリア
            // Clear debug objects disabled flag after test.
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask Body()
            {
                await LoadMainGame();

                // UIプレハブをロードしてルートに対象Viewが付いていることを確認
                // Load the UI prefab and confirm the target View is on its root.
                using var loaded = await AddressableLoader.LoadAsync<GameObject>(UiAddress, CancellationToken.None);
                Assert.IsNotNull(loaded.Asset, "UI prefab not loaded");
                var view = loaded.Asset.GetComponent<ElectricToGearGeneratorBlockInventoryView>();
                Assert.IsNotNull(view, "prefab root missing View component");
            }

            #endregion
        }

        [UnityTest]
        public IEnumerator RowSelectChangesServerSelectedIndex()
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

            // テスト終了後にデバッグオブジェクト無効化フラグをクリア
            // Clear debug objects disabled flag after test.
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask Body()
            {
                await LoadMainGame();

                // テストブロックを配置し、サーバーコンポーネントの初期選択が0であることを確認
                // Place the test block and confirm the server component's initial selection is 0.
                var pos = new Vector3Int(0, 0, 0);
                var serverBlock = PlaceBlock(BlockName, pos, BlockDirection.North);
                var component = serverBlock.GetComponent<ElectricToGearGeneratorComponent>();
                Assert.AreEqual(0, component.SelectedIndex);

                // クライアント側のBlockGameObjectがスポーンするまで待機
                // Wait until the client-side BlockGameObject spawns.
                BlockGameObject blockGameObject = null;
                for (var i = 0; i < 60 && blockGameObject == null; i++)
                {
                    ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(pos, out blockGameObject);
                    await UniTask.Yield();
                }
                Assert.IsNotNull(blockGameObject, "client BlockGameObject not spawned");

                // UIをDI経由で生成しInitializeする
                // Instantiate the UI via DI and initialize it.
                using var loaded = await AddressableLoader.LoadAsync<GameObject>(UiAddress, CancellationToken.None);
                var instance = ClientDIContext.DIContainer.Instantiate(loaded.Asset);
                var view = instance.GetComponent<ElectricToGearGeneratorBlockInventoryView>();
                view.Initialize(blockGameObject);

                // 行index 2の選択操作をプログラムから起動
                // Drive a selection of row index 2 programmatically.
                view.SelectModeForTest(2);

                // ネットワーク往復後にサーバーの選択indexが2になることを確認
                // Confirm the server selected index becomes 2 after the network round-trip.
                for (var i = 0; i < 120 && component.SelectedIndex != 2; i++) await UniTask.Yield();
                Assert.AreEqual(2, component.SelectedIndex, "row select did not reach server SelectedIndex");

                Object.Destroy(instance);
            }

            #endregion
        }
    }
}
