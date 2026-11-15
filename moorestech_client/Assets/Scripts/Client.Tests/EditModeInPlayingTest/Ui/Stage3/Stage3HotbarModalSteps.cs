using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Hotbar;
using Client.Playtest.Operations;
using Client.Playtest.UiElements;
using Client.Tests.EditModeInPlayingTest.Ui.Stage4;
using Client.Ui.State.Modal;
using Cysharp.Threading.Tasks;
using Game.PlacementTarget;
using NUnit.Framework;
using UnityEngine.UIElements;
using VContainer;

namespace Client.Tests.EditModeInPlayingTest.Ui.Stage3
{
    // 段 3 の L3 のホットバーの選択とモーダルの往復の手順
    // Stage-3 L3 steps for the hotbar selection and the modal round trip
    public static class Stage3HotbarModalSteps
    {
        private const int SelectedSlotIndex = 1;
        private const string SelectedSlotName = "hotbar-slot-1";
        private const string SlotSelectedClass = "slot-frame--selected";
        private const string ModalEnteredText = "stage3-bp";
        // サーバーのエコーと開閉の動きを待つ上限のフレーム数（どれも数十フレームで終わる）
        // Frame cap for server echoes and open/close animations (each finishes in tens of frames)
        private const int MaxWaitFrames = 300;

        public static async UniTask AssignFirstUnlockedBlock(IObjectResolver resolver)
        {
            // ビルドメニューと同じ供給源の最初の解放済みブロックを割り当て、サーバーのエコーを待つ
            // Assign the first unlocked block from the build menu's source and wait for the server echo
            var block = new List<PlacementTargetEntry>();
            foreach (var entry in resolver.Resolve<PlacementTargetResolver>().UnlockedEntries())
            {
                if (entry.Kind == PlacementTargetKind.Block) block.Add(entry);
            }
            Assert.IsNotEmpty(block, "前提: 解放済みのブロックが無い");
            var hotbar = resolver.Resolve<ClientHotbarDatastore>();
            hotbar.RequestAssign(SelectedSlotIndex, block[0].Id);
            for (var i = 0; i < MaxWaitFrames && hotbar.Assignments[SelectedSlotIndex] != block[0].Id; i++) await UniTask.Yield();
            Assert.AreEqual(block[0].Id, hotbar.Assignments[SelectedSlotIndex], $"枠 {SelectedSlotIndex} に {block[0].MasterDisplayName} が割り当たらない");
        }

        public static async UniTask AssertSlotSelected(VisualElement root, bool selected)
        {
            for (var i = 0; i < MaxWaitFrames && IsSlotSelected() != selected; i++) await UniTask.Yield();
            Assert.AreEqual(selected, IsSlotSelected(), $"{SelectedSlotName} の選択の印が {(selected ? "付かない" : "外れない")}");

            #region Internal

            bool IsSlotSelected()
            {
                var hud = Stage4ScreenSetProbe.VisibleByName(root, Stage3ExpectedSurfaces.HotbarHud);
                return hud.Count == 1 && hud[0].Q(name: SelectedSlotName).ClassListContains(SlotSelectedClass);
            }

            #endregion
        }

        public static async UniTask RoundTripInputModal(ModalRequestService modalService, VisualElement root)
        {
            // 入力付きモーダルを出し、1 つだけ見えて前面にあり、入力欄があることを見る
            // Show an input modal; it shows once, sits in front and has an input field
            var (_, response) = modalService.RequestInputModal("t", "m", "OK");
            await WaitModalCount(root, 1);
            var modal = Stage4ScreenSetProbe.VisibleByName(root, Stage3ExpectedSurfaces.Modal);
            Assert.AreEqual(1, modal.Count, $"モーダルが {modal.Count} 個見えている");
            Assert.IsTrue(Stage4ScreenSetProbe.IsPickedAtCenter(modal[0]), $"モーダルの中心で {Stage4ScreenSetProbe.DescribePicked(modal[0])} が拾われた（覆われている）");
            Assert.IsNotNull(modal[0].Q(name: Stage3ExpectedSurfaces.ModalInput), "モーダルに入力欄が無い");

            // DSL と同じ口で文字を入れて押す（try/finally はセッションを必ず閉じるためで、例外は握らない）
            // Type and press through the DSL's path (try/finally only guarantees the session closes; nothing is swallowed)
            PlaytestUiLocatorSession.Start();
            try
            {
                await PlaytestUiElementOps.SetText(Stage3ExpectedSurfaces.ModalInput, ModalEnteredText);
                await PlaytestUiElementOps.ClickUi(Stage3ExpectedSurfaces.ModalButton);
            }
            finally
            {
                PlaytestUiLocatorSession.Stop();
            }
            var (result, text) = await response.Timeout(TimeSpan.FromSeconds(5));
            Assert.AreEqual(ModalResult.Confirm, result, "モーダルのボタンで確定にならない");
            Assert.AreEqual(ModalEnteredText, text, "確定の結果に入れた文字が返らない");
            await WaitModalCount(root, 0);
            Assert.AreEqual(0, Stage4ScreenSetProbe.VisibleByName(root, Stage3ExpectedSurfaces.Modal).Count, "確定の後もモーダルが残る");
        }

        public static async UniTask WaitModalCount(VisualElement root, int count)
        {
            for (var i = 0; i < MaxWaitFrames && Stage4ScreenSetProbe.VisibleByName(root, Stage3ExpectedSurfaces.Modal).Count != count; i++) await UniTask.Yield();
        }
    }
}
