using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Tests.EditModeInPlayingTest.BlockInventory;
using Client.Ui.Foundation.Root;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Ui.Stage4
{
    /// <summary>
    /// 段階 4 の同時に出る組（持ち物・研究・機械）を実ゲームで巡る
    /// Walks the stage-4 co-shown sets (inventory, research, machine) in the real game
    /// </summary>
    [Category("CiShardClientPlay1")]
    public class Stage4ScreenSetGameUiTest : UiSystemPlayModeTestBase
    {
        private const string MachineBlockName = "釜";
        private const string LeftPaddingCollapsedClass = "stage-columns--left-padding-collapsed";
        private const float OpeningSkitStartWaitSeconds = 5f;

        [UnityTest]
        public IEnumerator 持ち物と研究と機械の画面を行き来しても各面が自分の列に1つだけ見え覆われない()
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
                var uiStateControl = ClientDIContext.DIContainer.DIContainerResolver.Resolve<UIStateControl>();
                var root = GameUiRoot.Instance.GetRootElement();
                using var backgroundInput = EditorBackgroundInputScope.Open();
                Assert.AreEqual(UiSystem.GameUi, UiInputExclusivity.ActiveUiSystem, "起動の系が GameUi でない");

                // 開幕スキットを飛ばしてゲーム画面から巡る（Stage2HudGameUiTest と同じ前段）
                // Skip the opening skit and start from the game screen (same prelude as Stage2HudGameUiTest)
                await PlaytestSetup.CreateFlatGroundAndWarp();
                await PlaytestSkitOps.SkipOpeningSkitIfPlaying(OpeningSkitStartWaitSeconds);
                Assert.AreEqual(UIStateEnum.GameScreen, uiStateControl.CurrentState, "開幕スキットを飛ばした後にゲーム画面でない");
                var errors = new List<string>();
                Application.logMessageReceived += CollectErrors;

                // 持ち物 → 研究 → 機械 → 持ち物の順に本番の遷移で巡る
                // Walk inventory -> research -> machine -> inventory through production transitions
                await TransitionTo(uiStateControl, UIStateEnum.PlayerInventory);
                await WaitScreenSettled(root, UIStateEnum.PlayerInventory);
                AssertScreenSet(root, UIStateEnum.PlayerInventory);
                await TransitionTo(uiStateControl, UIStateEnum.GameScreen);
                await WaitScreenSettled(root, UIStateEnum.GameScreen);
                AssertScreenSet(root, UIStateEnum.GameScreen);
                await TransitionTo(uiStateControl, UIStateEnum.ResearchTree);
                await WaitScreenSettled(root, UIStateEnum.ResearchTree);
                AssertScreenSet(root, UIStateEnum.ResearchTree);
                AssertResearchTreeRightOfInventory(root);
                await TransitionTo(uiStateControl, UIStateEnum.GameScreen);

                // 機械は F キーの注入で開き、閉じた後の持ち物に残骸が無いことを見る
                // Open the machine by injected F and check the inventory has no leftovers after closing
                await InteractKeyBlockOpener.PlaceNearPlayerAndOpen(MachineBlockName);
                await WaitScreenSettled(root, UIStateEnum.SubInventory);
                AssertScreenSet(root, UIStateEnum.SubInventory);
                await InteractKeyBlockOpener.CloseToGameScreen();
                await TransitionTo(uiStateControl, UIStateEnum.PlayerInventory);
                await WaitScreenSettled(root, UIStateEnum.PlayerInventory);
                AssertScreenSet(root, UIStateEnum.PlayerInventory);

                Application.logMessageReceived -= CollectErrors;
                Assert.IsEmpty(errors, $"巡る間に Error・例外のログが出た:\n{string.Join("\n", errors)}");

                void CollectErrors(string condition, string stackTrace, LogType type)
                {
                    if (type == LogType.Error || type == LogType.Exception) errors.Add($"[{type}] {condition}");
                }
            }

            #endregion
        }

        // 開閉の動きが落ち着くまで待つ上限のフレーム数（動きは数十フレームで終わるので余裕を持たせた値）
        // Frame cap for waiting until open/close animations settle (they finish in tens of frames, so this leaves headroom)
        private const int MaxSettleFrames = 120;

        private static async UniTask WaitScreenSettled(VisualElement root, UIStateEnum state)
        {
            // 開閉の動きの途中は不透明度が 0 と 1 の間なので、出る面が見え出ない面が消えるまで待つ
            // Mid open/close animations sit between 0 and 1 opacity, so wait until shown surfaces appear and hidden ones vanish
            var expected = Stage4ExpectedSurfaces.Expected(state);
            for (var i = 0; i < MaxSettleFrames; i++)
            {
                var settled = Stage4ExpectedSurfaces.All.All(s => Stage4ScreenSetProbe.VisibleByName(root, s.Name).Count == (expected.Contains(s) ? 1 : 0));
                if (settled) return;
                await UniTask.Yield();
            }
            // silent-ok: MaxSettleFrames で落ち着かなくても続ける / 直後の AssertScreenSet がどの面かを名前付きで落とす
        }

        private static void AssertScreenSet(VisualElement root, UIStateEnum state)
        {
            var expected = Stage4ExpectedSurfaces.Expected(state);
            foreach (var surface in Stage4ExpectedSurfaces.All)
            {
                var shouldShow = expected.Contains(surface);
                var visible = Stage4ScreenSetProbe.VisibleByName(root, surface.Name);

                // 同名の要素を全件見て、出ない面は可視が 0 件、出る面は可視がちょうど 1 件（先頭だけを見ると残骸を見逃す）
                // Check every same-named element: hidden surfaces have zero visible, shown ones exactly one (the first match alone misses leftovers)
                if (!shouldShow)
                {
                    Assert.AreEqual(0, visible.Count, $"{state}: {surface.Name} が {visible.Count} 個見えている");
                    continue;
                }
                Assert.AreEqual(1, visible.Count, $"{state}: 見える {surface.Name} が {visible.Count} 個（全件 {Stage4ScreenSetProbe.CountByName(root, surface.Name)}）");
                var shown = visible[0];
                AssertColumn(state, surface, shown);

                // 幕は入力を素通しするので Pick の照合から外す
                // The backdrop lets input through, so skip it in the pick check
                if (surface.Name == Stage4ExpectedSurfaces.Backdrop.Name) continue;
                Assert.IsTrue(Stage4ScreenSetProbe.IsPickedAtCenter(shown), $"{state}: {surface.Name} の中心で {Stage4ScreenSetProbe.DescribePicked(shown)} が拾われた（覆われている）");
            }
        }

        // 列に載るべき面はその列の子孫に、列の外の面はどの列の子孫でもないことを見る
        // A column surface must sit under its column; an outside surface must sit under no column
        private static void AssertColumn(UIStateEnum state, Stage4Surface surface, VisualElement shown)
        {
            var inColumn = Stage4ScreenSetProbe.TryGetColumnName(shown, out var columnName);
            if (!surface.ExpectsColumn)
            {
                Assert.IsFalse(inColumn, $"{state}: {surface.Name} は列の外のはずが {columnName} にある");
                return;
            }
            Assert.IsTrue(inColumn, $"{state}: {surface.Name} がどの列にも無い（期待 {surface.ColumnName}）");
            Assert.AreEqual(surface.ColumnName, columnName, $"{state}: {surface.Name} の列が違う");
        }

        private static void AssertResearchTreeRightOfInventory(VisualElement root)
        {
            // 研究エリアは持ち物の右に置かれ、左余白が畳まれる
            // The research area sits right of the inventory and the left padding collapses
            var inventory = root.Q(Stage4ExpectedSurfaces.Inventory.Name).worldBound;
            var research = root.Q(Stage4ExpectedSurfaces.ResearchTree.Name).worldBound;
            Assert.GreaterOrEqual(research.xMin, inventory.xMax - 0.5f, $"研究エリアの左端 {research.xMin} が持ち物の右端 {inventory.xMax} より左");
            Assert.IsNotNull(root.Q(className: LeftPaddingCollapsedClass), "ResearchTree で左余白を畳むクラスが付いていない");
        }

        private static async UniTask TransitionTo(UIStateControl uiStateControl, UIStateEnum state)
        {
            // 遷移要求は次の Update で消費される
            // The transition request is consumed in the next Update
            uiStateControl.RequestTransition(state);
            await UniTask.DelayFrame(3);
            Assert.AreEqual(state, uiStateControl.CurrentState, $"{state} へ遷移していない");
        }
    }
}
