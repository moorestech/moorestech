using System.Collections;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Tests.EditModeInPlayingTest.Ui.Stage4;
using Client.Ui.Foundation.Root;
using Client.Ui.State.Modal;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Ui.Stage3
{
    /// <summary>
    /// ビルドメニュー・ホットバーの選択・モーダルを本番の遷移で巡る（段階 3 の L3）
    /// Walks the build menu, hotbar selection and modal through production transitions (stage-3 L3)
    /// </summary>
    [Category("CiShardClientPlay1")]
    public class Stage3BuildSetGameUiTest : UiSystemPlayModeTestBase
    {
        private const float OpeningSkitStartWaitSeconds = 5f;
        // 遷移・開閉の動き・サーバーのエコーを待つ上限のフレーム数（どれも数十フレームで終わる）
        // Frame cap for transitions, open/close animations and server echoes (each finishes in tens of frames)
        private const int MaxWaitFrames = 300;

        [UnityTest]
        public IEnumerator ビルドメニューとホットバーの選択とモーダルを行き来しても各面が1つだけ見え覆われず閉じると残らない()
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
                var root = GameUiRoot.Instance.GetRootElement();
                using var backgroundInput = EditorBackgroundInputScope.Open();
                Assert.AreEqual(UiSystem.GameUi, UiInputExclusivity.ActiveUiSystem, "起動の系が GameUi でない");

                // 開幕スキットを飛ばしてゲーム画面から巡る（Stage2・Stage4 と同じ前段）
                // Skip the opening skit and start from the game screen (same prelude as Stage2 and Stage4)
                await PlaytestSetup.CreateFlatGroundAndWarp();
                await PlaytestSkitOps.SkipOpeningSkitIfPlaying(OpeningSkitStartWaitSeconds);
                Assert.AreEqual(UIStateEnum.GameScreen, uiStateControl.CurrentState, "開幕スキットを飛ばした後にゲーム画面でない");

                // 巡る間のエラーだけを集める（例外は握らない）
                // Collect errors only while cycling (nothing is swallowed)
                var errors = new List<string>();
                Application.logMessageReceived += Collect;
                try
                {
                    await Cycle();
                }
                finally
                {
                    Application.logMessageReceived -= Collect;
                }
                Assert.IsEmpty(errors, $"巡る間に Error・例外のログが出た:\n{string.Join("\n", errors)}");

                async UniTask Cycle()
                {
                    // ゲーム画面 → B でビルドメニュー → B で閉じる
                    // Game screen, B opens the build menu, B closes it
                    await AssertScreenSettled(root, UIStateEnum.GameScreen);
                    await TapKeyUntilState(uiStateControl, Key.B, UIStateEnum.BuildMenu);
                    await AssertScreenSettled(root, UIStateEnum.BuildMenu);
                    await TapKeyUntilState(uiStateControl, Key.B, UIStateEnum.GameScreen);
                    await AssertScreenSettled(root, UIStateEnum.GameScreen);

                    // 枠 1 に割り当てて 2 で選ぶと印が付き、Esc で戻ると外れる
                    // Assign slot 1 and pick it with 2 to mark it; Esc back removes the mark
                    await Stage3HotbarModalSteps.AssignFirstUnlockedBlock(resolver);
                    await TapKeyUntilState(uiStateControl, Key.Digit2, UIStateEnum.PlaceBlock);
                    await AssertScreenSettled(root, UIStateEnum.PlaceBlock);
                    await Stage3HotbarModalSteps.AssertSlotSelected(root, true);
                    await TapKeyUntilState(uiStateControl, Key.Escape, UIStateEnum.GameScreen);
                    await Stage3HotbarModalSteps.AssertSlotSelected(root, false);
                    await AssertScreenSettled(root, UIStateEnum.GameScreen);

                    await Stage3HotbarModalSteps.RoundTripInputModal(resolver.Resolve<ModalRequestService>(), root);
                    await AssertScreenSettled(root, UIStateEnum.GameScreen);
                }

                void Collect(string condition, string stackTrace, LogType type)
                {
                    if (type == LogType.Error || type == LogType.Exception) errors.Add($"[{type}] {condition}");
                }
            }

            #endregion
        }

        private static async UniTask TapKeyUntilState(UIStateControl uiStateControl, Key key, UIStateEnum expected)
        {
            await SemanticInput.TapKey(key);
            for (var i = 0; i < MaxWaitFrames && uiStateControl.CurrentState != expected; i++) await UniTask.Yield();
            Assert.AreEqual(expected, uiStateControl.CurrentState, $"{key} で {expected} へ遷移しない");
        }

        private static async UniTask AssertScreenSettled(VisualElement root, UIStateEnum state)
        {
            // 開閉の動きが落ち着くまで待ち、同名の全件で「出る面は可視 1 件・中心で自分が拾われる／出ない面は可視 0 件」を見る
            // Wait for animations to settle, then over every same-named element: shown surfaces have one visible picked at center, hidden ones none
            var expected = Stage3ExpectedSurfaces.Expected(state);
            for (var i = 0; i < MaxWaitFrames && !IsSettled(); i++) await UniTask.Yield();
            // silent-ok: 上限で落ち着かなくても続ける / 直後の Assert が面の名前付きで落とす
            foreach (var name in Stage3ExpectedSurfaces.All)
            {
                var visible = Stage4ScreenSetProbe.VisibleByName(root, name);
                if (!expected.Contains(name))
                {
                    Assert.AreEqual(0, visible.Count, $"{state}: {name} が {visible.Count} 個見えている");
                    continue;
                }
                Assert.AreEqual(1, visible.Count, $"{state}: 見える {name} が {visible.Count} 個（全件 {Stage4ScreenSetProbe.CountByName(root, name)}）");

                // 幕は入力を素通しするので Pick の照合から外す
                // The backdrop lets input through, so skip it in the pick check
                if (name == Stage3ExpectedSurfaces.Backdrop) continue;
                Assert.IsTrue(Stage4ScreenSetProbe.IsPickedAtCenter(visible[0]), $"{state}: {name} の中心で {Stage4ScreenSetProbe.DescribePicked(visible[0])} が拾われた（覆われている）");
            }

            #region Internal

            bool IsSettled()
            {
                foreach (var name in Stage3ExpectedSurfaces.All)
                {
                    if (Stage4ScreenSetProbe.VisibleByName(root, name).Count != (expected.Contains(name) ? 1 : 0)) return false;
                }
                return true;
            }

            #endregion
        }
    }
}
