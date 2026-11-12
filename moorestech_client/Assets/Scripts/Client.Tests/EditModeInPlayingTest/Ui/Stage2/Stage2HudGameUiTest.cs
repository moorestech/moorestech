using System.Collections;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Crosshair;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest.Input;
using Client.Playtest.Operations.Ui;
using Client.Playtest.UiElements.GameUi;
using Client.Ui.Foundation.Root;
using Client.Ui.State.SkitPresence;
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
                var root = GameUiRoot.Instance.GetRootElement();
                using var backgroundInput = EditorBackgroundInputScope.Open();

                // Web ホスト無しで GameUi のみ動く
                // Only GameUi runs, no web host
                Assert.AreEqual(UiSystem.GameUi, UiInputExclusivity.ActiveUiSystem, "起動の系が GameUi でない");
                Assert.IsNull(Client.WebUiHost.Boot.WebUiHost.Hub, "UITK 側で Web ホストが起動している");

                // 開幕の blocking スキットが走っていれば抜けるまで待つ（走ると key-hints が隠れる）
                // Wait out an opening blocking skit if one runs (it hides key-hints)
                if (resolver.Resolve<BlockingSkitPresence>().IsBlocking.Value) await PlaytestSkitOps.SkipOpeningSkitIfPlaying(30f);
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
                    // 常駐 HUD が期待の層に 1 つずつ載る
                    // Each resident HUD mounts once on its layer
                    AssertMountedIn(root, UIStateEnum.GameScreen);

                    // ゲーム画面で出るもの・出ないもの
                    // What shows and what does not on the game screen
                    Assert.IsTrue(Visible(root, "key-hints"), "ゲーム画面で key-hints が見えない");
                    // 開始は三人称（PlayerViewModeController の初期値）。V で一人称へ切り替えると crosshair が出る
                    // The game starts in third person (PlayerViewModeController default); V switches to first person and shows the crosshair
                    Assert.IsFalse(crosshair.IsVisible(), "前提: ゲーム画面の開始が三人称でない");
                    Assert.IsFalse(Visible(root, "crosshair"), "三人称なのに crosshair が見える");
                    await SemanticInput.TapKey(Key.V);
                    await UniTask.DelayFrame(3);
                    Assert.IsTrue(crosshair.IsVisible(), "V で一人称へ切り替わらない");
                    Assert.IsTrue(Visible(root, "crosshair"), "一人称なのに crosshair が見えない");
                    Assert.IsTrue(Visible(root, "challenge-hud"), "ゲーム画面で目標 HUD が見えない");
                    Assert.IsFalse(Visible(root, "screen-backdrop"), "ゲーム画面で幕が見える");
                    Assert.IsFalse(Visible(root, "delete-mode-warning"), "ゲーム画面で削除の帯が見える");

                    // UI 状態の遷移で出し分けが変わる
                    // Transitions switch what is shown
                    await TransitionTo(uiStateControl, UIStateEnum.PauseMenu);
                    Assert.IsFalse(Visible(root, "key-hints"), "ポーズ中に key-hints が見える");
                    Assert.IsTrue(Visible(root, "screen-backdrop"), "ポーズ中に幕が出ない");
                    await TransitionTo(uiStateControl, UIStateEnum.GameScreen);
                    await TransitionTo(uiStateControl, UIStateEnum.DeleteBar);
                    AssertMountedIn(root, UIStateEnum.DeleteBar);
                    Assert.IsTrue(Visible(root, "delete-mode-warning"), "削除モードで帯が出ない");
                    Assert.IsTrue(Visible(root, "delete-box-height-hud"), "削除モードで削除箱の高さ HUD が出ない");
                    await TransitionTo(uiStateControl, UIStateEnum.GameScreen);
                    Assert.IsFalse(Visible(root, "delete-mode-warning"), "ゲーム画面へ戻っても帯が残る");
                    await TransitionTo(uiStateControl, UIStateEnum.PlaceBlock);
                    AssertMountedIn(root, UIStateEnum.PlaceBlock);
                    Assert.IsTrue(Visible(root, "placement-mode-hud"), "設置モードで配置 HUD が出ない");
                    await TransitionTo(uiStateControl, UIStateEnum.GameScreen);
                    Assert.IsFalse(Visible(root, "placement-mode-hud"), "ゲーム画面へ戻っても配置 HUD が残る");

                    // Ctrl+U で根ごと消え、再度で戻る
                    // Ctrl+U hides the root, again restores it
                    await ToggleCtrlU();
                    Assert.IsFalse(wholeUi.IsShown.Value, "Ctrl+U で全体非表示にならない");
                    Assert.AreEqual(DisplayStyle.None, root.resolvedStyle.display, "Ctrl+U で根が隠れない");
                    foreach (var surface in Stage2HudSurfaces.All) Assert.IsFalse(Visible(root, surface.Name), $"{surface.Name} が Ctrl+U 中も見える");
                    await ToggleCtrlU();
                    Assert.IsTrue(wholeUi.IsShown.Value, "2 回目の Ctrl+U で戻らない");
                    Assert.IsTrue(Visible(root, "key-hints"), "Ctrl+U の往復の後に key-hints が見えない");
                }

                void Collect(string condition, string stackTrace, LogType type)
                {
                    if (type == LogType.Error || type == LogType.Exception) errors.Add(condition);
                }
            }

            #endregion
        }

        // その UI 状態の行が期待の層に 1 つずつ載る
        // Each row of the UI state mounts once under its layer
        private static void AssertMountedIn(VisualElement root, UIStateEnum state)
        {
            foreach (var surface in Stage2HudSurfaces.All)
            {
                if (surface.MountedState != state) continue;
                var matches = Matches(root, surface.Name, surface.ByClass);
                Assert.AreEqual(1, matches.Count, $"{surface.Name} が {state} で {matches.Count} 件載っている（期待 1 件）");
                var layer = GameUiRoot.Instance.GetLayer(surface.Layer, surface.Family);
                Assert.IsTrue(layer.Contains(matches[0]), $"{surface.Name} が層 {surface.Layer} の {surface.Family} の下に無い");
            }
        }

        // 名前かクラスで引き、どれかが見えていれば真（判定は DSL の GameUiElementQuery を使う）
        // True when any element found by name or class is visible (uses the DSL's GameUiElementQuery)
        private static bool Visible(VisualElement root, string name)
        {
            var candidates = Matches(root, name, false);
            candidates.AddRange(Matches(root, name, true));
            foreach (var element in candidates)
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
