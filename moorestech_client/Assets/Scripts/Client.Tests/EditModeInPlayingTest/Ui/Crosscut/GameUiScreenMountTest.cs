using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Screens.Hotbar;
using Client.Ui.Screens.Inventory.Equipment;
using Client.Ui.Screens.Inventory.Panel;
using Client.Ui.Screens.KeyHints;
using Client.Ui.State.Routing;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Ui.Crosscut
{
    /// <summary>
    /// 実ゲームの GameUi 起動で、全 Presenter の View が表どおりの層に 1 つずつ載り、購読から Render が届いていることを見る（DC-31 A）
    /// On a real GameUi boot, checks every presenter's view sits once in its tabled layer and that Render arrived through the subscription (DC-31 A)
    /// </summary>
    [Category("CiShardClientPlay3")]
    public class GameUiScreenMountTest : UiSystemPlayModeTestBase
    {
        private const float OpeningSkitStartWaitSeconds = 5f;
        private const int InventoryRenderWaitFrameLimit = 120;

        [UnityTest]
        public IEnumerator 全画面のViewが表どおりの層に1つずつ載り起動直後のHUDと開いた持ち物が描かれる()
        {
            SelectUiSystemBeforePlayMode(UiSystem.GameUi);
            EnterPlayModeUtil();

            // yield return new EnterPlayMode は必ず[UnityTest]関数の直下で呼び出すこと
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;

            yield return TestBody().ToCoroutine();

            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask TestBody()
            {
                await LoadMainGame();
                await UniTask.DelayFrame(5);
                var resolver = ClientDIContext.DIContainer.DIContainerResolver;
                var uiStateControl = resolver.Resolve<UIStateControl>();
                var gameUiRoot = GameUiRoot.Instance;
                Assert.AreEqual(UiSystem.GameUi, UiInputExclusivity.ActiveUiSystem, "起動の系が GameUi でない");

                // 開幕スキットを飛ばしてゲーム画面の HUD を見る（Stage4ScreenSetGameUiTest と同じ前段）
                // Skip the opening skit and inspect the game-screen HUD (same prelude as Stage4ScreenSetGameUiTest)
                await PlaytestSetup.CreateFlatGroundAndWarp();
                await PlaytestSkitOps.SkipOpeningSkitIfPlaying(OpeningSkitStartWaitSeconds);
                Assert.AreEqual(UIStateEnum.GameScreen, uiStateControl.CurrentState, "開幕スキットを飛ばした後にゲーム画面でない");

                // 足りない・違うものを全部集めてから 1 回で落とす
                // Collect every missing or wrong item, then fail once
                var problems = new List<string>(GameUiScreenMountTable.DescribeTableDrift());
                foreach (var mount in GameUiScreenMountTable.All) CheckMount(gameUiRoot, mount, problems);
                CheckChromeOrder(gameUiRoot, problems);
                CheckRenderedAtBoot(gameUiRoot, problems);

                // 本番の要求の口でインベントリ画面を開き、持ち物の枠が描かれるまで待つ
                // Open the inventory through the production request path and wait for its slots to be drawn
                var requested = resolver.Resolve<IUiStateTransitionRequester>().RequestTransition(UIStateEnum.PlayerInventory);
                if (!requested) problems.Add("GameScreen から PlayerInventory への要求が拒まれた");
                var inventory = gameUiRoot.GetLayer(UiLayer.Screen, UiFamily.Stage).Q<InventoryView>();
                for (var i = 0; i < InventoryRenderWaitFrameLimit && CountInventorySlots(inventory) == 0; i++) await UniTask.Yield();
                if (CountInventorySlots(inventory) == 0) problems.Add($"PlayerInventory を開いても持ち物の枠が 0（画面 {uiStateControl.CurrentState}）");

                Assert.IsEmpty(problems, "載り先・描きの欠け:\n" + string.Join("\n", problems));
            }

            #endregion
        }

        private static void CheckMount(GameUiRoot gameUiRoot, GameUiScreenMount mount, List<string> problems)
        {
            // 表の層の中に 1 つ、ルート全体でも 1 つ（別の層・二重に載っていない）
            // Exactly one in the tabled layer and exactly one in the whole root (no other layer, no double mount)
            var inLayer = OfType(gameUiRoot.GetLayer(mount.Layer, mount.Family), mount).Count;
            var inRoot = OfType(gameUiRoot.GetRootElement(), mount).Count;
            if (inLayer != 1 || inRoot != 1) problems.Add($"{mount.ViewType.Name}: {mount.Layer}/{mount.Family} に {inLayer} 個、ルート全体に {inRoot} 個");
        }

        private static void CheckChromeOrder(GameUiRoot gameUiRoot, List<string> problems)
        {
            // Chrome/Viewport の兄弟の順は登録順の結果（Web の z はヒント 31・装備 > ホットバー 30）
            // Sibling order in Chrome/Viewport comes from registration order (Web z: hints 31 and equipment above the hotbar's 30)
            var chrome = gameUiRoot.GetLayer(UiLayer.Chrome, UiFamily.Viewport);
            var hotbar = chrome.IndexOf(chrome.Q<HotbarHudView>());
            var keyHint = chrome.IndexOf(chrome.Q<KeyHintHudView>());
            var equipment = chrome.IndexOf(chrome.Q<EquipmentHudView>());
            if (hotbar < 0 || keyHint <= hotbar) problems.Add($"Chrome/Viewport でキー操作ヒント({keyHint})がホットバー({hotbar})より前");
            if (hotbar < 0 || equipment <= hotbar) problems.Add($"Chrome/Viewport で装備({equipment})がホットバー({hotbar})より前");
        }

        private static void CheckRenderedAtBoot(GameUiRoot gameUiRoot, List<string> problems)
        {
            // 起動直後に Model の購読から Render が届くと枠・行が作られる（購読が切れていると 0 のまま）
            // Render through the model subscription builds cells and rows right after boot (a dropped subscription leaves zero)
            var chrome = gameUiRoot.GetLayer(UiLayer.Chrome, UiFamily.Viewport);
            AddIfEmpty(chrome.Q<HotbarHudView>(), "hotbar-hud__cell", "ホットバーの枠");
            AddIfEmpty(chrome.Q<EquipmentHudView>(), "equipment-hud__slot", "装備の枠");
            AddIfEmpty(chrome.Q<KeyHintHudView>(), "key-hint-row", "キー操作ヒントの行");

            #region Internal

            void AddIfEmpty(VisualElement view, string className, string label)
            {
                var count = view?.Query(className: className).ToList().Count ?? 0;
                if (count == 0) problems.Add($"{label}が 0（Render が届いていない）");
            }

            #endregion
        }

        private static int CountInventorySlots(InventoryView inventory)
        {
            return inventory?.Query(className: "slot-frame").ToList().Count ?? 0;
        }

        private static List<VisualElement> OfType(VisualElement scope, GameUiScreenMount mount)
        {
            return scope.Query<VisualElement>().Where(element => element.GetType() == mount.ViewType).ToList();
        }
    }
}
