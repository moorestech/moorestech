using System.Collections;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.UI.Crosshair;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest.Input;
using Client.Playtest.Operations.Ui;
using Client.Playtest.UiElements.GameUi;
using Client.Ui.Foundation.Root;
using Client.Ui.State.WholeUiVisibility;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Ui.Stage2
{
    /// <summary>
    /// 段階 2 HUD の層への載りと出し分けの確認（R-4 L3）
    /// Checks stage-2 HUD layer mounting and switching (R-4 L3)
    /// </summary>
    [Category("CiShardClientPlay1")]
    public class Stage2HudGameUiTest : UiSystemPlayModeTestBase
    {
        // 開幕スキットの再生開始を待つ期限（無ければこの秒数で抜ける）
        // Deadline for an opening skit to start (returns after this when none plays)
        private const float OpeningSkitStartWaitSeconds = 5f;

        [UnityTest]
        public IEnumerator UITK側で段階2のHUDが各層に1つずつ載り画面遷移とCtrlUで出て消える()
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
                var errors = new List<string>();
                await LoadMainGame();
                await UniTask.DelayFrame(5);
                var resolver = ClientDIContext.DIContainer.DIContainerResolver;
                var uiStateControl = resolver.Resolve<UIStateControl>();
                var wholeUi = resolver.Resolve<WholeUiVisibilityModel>();
                var crosshair = resolver.Resolve<CrosshairVisibility>();
                var viewMode = resolver.Resolve<PlayerViewModeController>();
                var root = GameUiRoot.Instance.GetRootElement();
                using var backgroundInput = EditorBackgroundInputScope.Open();

                // Web ホスト無しで GameUi のみ動く
                // Only GameUi runs, no web host
                Assert.AreEqual(UiSystem.GameUi, UiInputExclusivity.ActiveUiSystem, "起動の系が GameUi でない");
                Assert.IsNull(Client.WebUiHost.Boot.WebUiHost.Hub, "UITK 側で Web ホストが起動している");

                // 開幕スキットの開始を期限まで待って飛ばす（走ると key-hints が隠れる）
                // Wait for an opening skit to start and skip it (it hides key-hints)
                await PlaytestSkitOps.SkipOpeningSkitIfPlaying(OpeningSkitStartWaitSeconds);
                Assert.AreEqual(UIStateEnum.GameScreen, uiStateControl.CurrentState, "開幕スキットを飛ばした後にゲーム画面でない");

                // 巡る間のエラーだけを集める（起動の後に購読し、必ず外す。例外は握らない）
                // Collect errors only while cycling (subscribe after boot and always unsubscribe; nothing is swallowed)
                Application.logMessageReceived += Collect;
                try
                {
                    await Cycle();
                }
                finally
                {
                    Application.logMessageReceived -= Collect;
                }
                Assert.IsEmpty(errors, "巡る間に Error/Exception のログが出た: " + string.Join(" / ", errors));

                async UniTask Cycle()
                {
                    // ゲーム画面で載る HUD が期待の層に 1 つずつ載る
                    // Each HUD mounted on the game screen sits once on its layer
                    AssertMountedIn(root, UIStateEnum.GameScreen);

                    // ゲーム画面で出るもの・出ないもの
                    // What shows and what does not on the game screen
                    Assert.IsTrue(Visible(root, Stage2HudSurfaces.KeyHints), "ゲーム画面で key-hints が見えない");
                    // 開始は三人称。V で一人称へ切り替えると crosshair が出る
                    // The game starts in third person; V switches to first person and shows the crosshair
                    Assert.AreEqual(PlayerViewMode.ThirdPerson, viewMode.GetCurrentMode(), "前提: ゲーム画面の開始が三人称でない");
                    Assert.IsFalse(crosshair.IsVisible(), "三人称なのに crosshair の可視フラグが立つ");
                    Assert.IsFalse(Visible(root, Stage2HudSurfaces.Crosshair), "三人称なのに crosshair が見える");
                    await SemanticInput.TapKey(Key.V);
                    await UniTask.DelayFrame(3);
                    Assert.AreEqual(PlayerViewMode.FirstPerson, viewMode.GetCurrentMode(), "V で一人称へ切り替わらない");
                    Assert.IsTrue(crosshair.IsVisible(), "一人称なのに crosshair の可視フラグが立たない");
                    Assert.IsTrue(Visible(root, Stage2HudSurfaces.Crosshair), "一人称なのに crosshair が見えない");
                    Assert.IsTrue(Visible(root, Stage2HudSurfaces.ChallengeHud), "ゲーム画面で目標 HUD が見えない");
                    Assert.IsFalse(Visible(root, Stage2HudSurfaces.ScreenBackdrop), "ゲーム画面で幕が見える");
                    Assert.IsFalse(Visible(root, Stage2HudSurfaces.DeleteModeWarning), "ゲーム画面で削除の帯が見える");

                    // UI 状態の遷移で出し分けが変わる
                    // Transitions switch what is shown
                    await TransitionTo(uiStateControl, UIStateEnum.PauseMenu);
                    Assert.IsFalse(Visible(root, Stage2HudSurfaces.KeyHints), "ポーズ中に key-hints が見える");
                    Assert.IsTrue(Visible(root, Stage2HudSurfaces.ScreenBackdrop), "ポーズ中に幕が出ない");
                    await TransitionTo(uiStateControl, UIStateEnum.GameScreen);
                    await TransitionTo(uiStateControl, UIStateEnum.DeleteBar);
                    AssertMountedIn(root, UIStateEnum.DeleteBar);
                    Assert.IsTrue(Visible(root, Stage2HudSurfaces.DeleteModeWarning), "削除モードで帯が出ない");
                    Assert.IsTrue(Visible(root, Stage2HudSurfaces.DeleteBoxHeightHud), "削除モードで削除箱の高さ HUD が出ない");
                    await TransitionTo(uiStateControl, UIStateEnum.GameScreen);
                    Assert.IsFalse(Visible(root, Stage2HudSurfaces.DeleteModeWarning), "ゲーム画面へ戻っても帯が残る");
                    await TransitionTo(uiStateControl, UIStateEnum.PlaceBlock);
                    AssertMountedIn(root, UIStateEnum.PlaceBlock);
                    Assert.IsTrue(Visible(root, Stage2HudSurfaces.PlacementModeHud), "設置モードで配置 HUD が出ない");
                    await TransitionTo(uiStateControl, UIStateEnum.GameScreen);
                    Assert.IsFalse(Visible(root, Stage2HudSurfaces.PlacementModeHud), "ゲーム画面へ戻っても配置 HUD が残る");

                    // Ctrl+U で根ごと消え、再度で戻る
                    // Ctrl+U hides the root, again restores it
                    await ToggleCtrlU();
                    Assert.IsFalse(wholeUi.IsShown.Value, "Ctrl+U で全体非表示にならない");
                    Assert.AreEqual(DisplayStyle.None, root.resolvedStyle.display, "Ctrl+U で根が隠れない");
                    foreach (var surface in Stage2HudSurfaces.All) Assert.IsFalse(Visible(root, surface), $"{surface.Name} が Ctrl+U 中も見える");
                    await ToggleCtrlU();
                    Assert.IsTrue(wholeUi.IsShown.Value, "2 回目の Ctrl+U で戻らない");
                    Assert.IsTrue(Visible(root, Stage2HudSurfaces.KeyHints), "Ctrl+U の往復の後に key-hints が見えない");
                }

                void Collect(string condition, string stackTrace, LogType type)
                {
                    if (type == LogType.Error || type == LogType.Exception) errors.Add(condition);
                }
            }

            #endregion
        }

        // その UI 状態で載る行（常駐を含む）が期待の層に 1 つずつ載る
        // Each row mounted in the UI state (residents included) sits once under its layer
        private static void AssertMountedIn(VisualElement root, UIStateEnum state)
        {
            foreach (var surface in Stage2HudSurfaces.All)
            {
                if (!surface.IsMountedIn(state)) continue;
                var matches = Matches(root, surface.Name, surface.ByClass);
                Assert.AreEqual(1, matches.Count, $"{surface.Name} が {state} で {matches.Count} 件載っている（期待 1 件）");
                var layer = GameUiRoot.Instance.GetLayer(surface.Layer, surface.Family);
                Assert.IsTrue(layer.Contains(matches[0]), $"{surface.Name} が層 {surface.Layer} の {surface.Family} の下に無い");
            }
        }

        // 表の ByClass に従って引き、どれかが見えていれば真（判定は DSL の GameUiElementQuery）
        // True when any element found per the row's ByClass is visible (uses the DSL's GameUiElementQuery)
        private static bool Visible(VisualElement root, Stage2HudSurface surface)
        {
            foreach (var element in Matches(root, surface.Name, surface.ByClass))
            {
                if (GameUiElementQuery.IsVisible(element)) return true;
            }
            return false;
        }

        private static List<VisualElement> Matches(VisualElement root, string name, bool byClass)
        {
            return byClass ? root.Query<VisualElement>(className: name).ToList() : root.Query<VisualElement>(name: name).ToList();
        }

        private static async UniTask TransitionTo(UIStateControl uiStateControl, UIStateEnum state)
        {
            // 遷移要求は次の Update で消費される
            // The transition request is consumed in the next Update
            uiStateControl.RequestTransition(state);
            await UniTask.DelayFrame(3);
            Assert.AreEqual(state, uiStateControl.CurrentState, $"{state} へ遷移していない");
        }

        private static async UniTask ToggleCtrlU()
        {
            // Ctrl を押したまま U を叩く（UIRoot が GetKey(LeftControl) と GetKeyDown(U) を読む）
            // Tap U while holding Ctrl (UIRoot reads GetKey(LeftControl) and GetKeyDown(U))
            SemanticInput.KeyDown(Key.LeftCtrl);
            await SemanticInput.TapKey(Key.U);
            SemanticInput.KeyUp(Key.LeftCtrl);
            await UniTask.DelayFrame(3);
        }
    }
}
