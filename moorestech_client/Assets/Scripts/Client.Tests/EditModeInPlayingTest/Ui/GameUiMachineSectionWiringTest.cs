using System.Collections;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Input;
using Client.Tests.EditModeInPlayingTest.BlockInventory;
using Client.Tests.EditModeInPlayingTest.Util.World;
using Client.Ui.Foundation.Root;
using Client.Ui.Screens.BlockInventory.Context;
using Client.Ui.Screens.BlockInventory.Family;
using Client.Ui.Screens.BlockInventory.Fluid;
using Client.Ui.Screens.BlockInventory.Machine;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Selection;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SlotRestriction;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Ui
{
    /// <summary>
    /// 機械の区画の登録と開き直し
    /// Machine sections resolve; reopening leaves no previous value
    /// </summary>
    [Category("CiShardClientPlay1")]
    public class GameUiMachineSectionWiringTest : UiSystemPlayModeTestBase
    {
        private const string MachineBlockName = "釜";

        [UnityTest]
        public IEnumerator UITK起動で機械の区画が解決し開き直すと開いた機械の値へ替わる()
        {
            SelectUiSystemBeforePlayMode(UiSystem.GameUi);
            EnterPlayModeUtil();

            // EnterPlayMode は直下で呼ぶ
            // Call EnterPlayMode directly under the [UnityTest] function
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;

            yield return TestBody().ToCoroutine();

            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask TestBody()
            {
                await LoadMainGame();
                var ground = TestPlayerGround.Create();

                // 起動ログを流してから想定外のエラーを拾う（VContainer の解決失敗もここで落ちる）
                // Drain the startup logs, then catch unexpected errors (VContainer resolution failures fail here too)
                await UniTask.Yield();
                LogAssert.ignoreFailingMessages = false;
                await TestPlayerGround.AssertGroundedAsync();
                AssertRegistrationsResolve();

                var resolver = ClientDIContext.DIContainer.DIContainerResolver;
                var machine = resolver.Resolve<IMachineSectionModel>();
                var panels = resolver.Resolve<IBlockInventoryPanelSource>();
                var uiStateControl = resolver.Resolve<UIStateControl>();

                // 2 台を自機の +x と +z の隣に置く（到達範囲外で閉じないため。-x 側の枠は自機の体と重なり押し出されて落ちる）
                // Place two machines next to the player on +x and +z (so the reach check keeps them open; a -x cell overlaps the player body and pushes it down)
                var playerPosition = Vector3Int.RoundToInt(PlayerSystemContainer.Instance.PlayerObjectController.Position);
                var first = await PlaceMachine(playerPosition + new Vector3Int(1, 0, 0));
                var second = await PlaceMachine(playerPosition + new Vector3Int(0, 0, 1));

                // 1 台目を開くと区画が出て値が入る
                // Opening the first machine shows its section and value
                await OpenSubInventory(uiStateControl, first);
                var firstPanel = await WaitMachinePresent(machine, panels);
                Assert.IsTrue(machine.Current.Value.TryGetPresent(out var firstMachine));
                Assert.Less(0, firstMachine.Rows.Count, "解放済みのレシピの行が無い");
                Assert.IsTrue(IsMachineSectionShown(), "機械の区画が画面に出ていない");

                // 開き直しで前の機械の値が残らない
                // Reopening leaves nothing of the previous machine
                var secondState = await BlockSubInventoryOpener.Open(second);
                var secondPanel = await WaitMachinePresent(machine, panels);
                Assert.AreNotEqual(firstPanel.BlockInstanceId, secondPanel.BlockInstanceId);
                Assert.IsTrue(machine.Current.Value.TryGetPresent(out var secondMachine));
                Assert.AreEqual(secondPanel.BlockInstanceId, secondMachine.BlockInstanceId, "前の機械の値が残っている");
                Assert.IsTrue(IsMachineSectionShown(), "開き直した機械の区画が画面に出ていない");

                // 閉じると区画の値を手放す
                // Closing releases the section's value
                BlockSubInventoryOpener.Close(secondState);
                await TransitionTo(uiStateControl, UIStateEnum.GameScreen);
                Assert.IsFalse(machine.Current.Value.IsPresent, "閉じても機械の区画の値が残っている");

                Object.DestroyImmediate(ground);
                LogAssert.ignoreFailingMessages = true;
            }

            async UniTask<BlockGameObject> PlaceMachine(Vector3Int position)
            {
                PlaceBlock(MachineBlockName, position, BlockDirection.North);
                var block = await WaitBlockGameObjectSpawn(position);
                Physics.SyncTransforms();
                return block;
            }

            #endregion
        }

        private static void AssertRegistrationsResolve()
        {
            // 工場 7 本と寄与 2 本が引ける
            // 7 factories, 2 contributions and the command resolve
            var resolver = ClientDIContext.DIContainer.DIContainerResolver;
            var factoryTypes = resolver.Resolve<System.Collections.Generic.IReadOnlyList<ISectionStackSectionFactory>>().Select(f => f.GetType()).ToArray();
            foreach (var type in new[] { typeof(ContextSectionFactory), typeof(MachineSectionFactory), typeof(MinerSectionFactory), typeof(PumpSectionFactory), typeof(GeneratorSectionFactory), typeof(FluidDetailsSectionFactory), typeof(FluidRowSectionFactory) })
            {
                Assert.AreEqual(1, factoryTypes.Count(t => t == type), $"{type.Name} の登録が 1 本でない");
            }
            Assert.AreEqual(1, resolver.Resolve<System.Collections.Generic.IReadOnlyList<IBlockInventoryLargePanelRule>>().Count(r => r is RecipeMachineLargePanelRule), "大型の寄与の登録が 1 本でない");
            Assert.AreEqual(1, resolver.Resolve<System.Collections.Generic.IReadOnlyList<IBlockSlotRestrictionContributor>>().Count(c => c is MachineSlotRestrictionContributor), "枠の制限の寄与の登録が 1 本でない");
            Assert.IsInstanceOf<MachineRecipeSelectCommand>(resolver.Resolve<IMachineRecipeSelectCommand>());
        }

        // UIStateControl に文脈付き遷移の入口は無いので、先に SubInventory へ入れて（文脈なしのエラーログが 1 回出る）実プレイと同じ文脈で開き直す（BlockDetailStateProbe と同じ手順）
        // UIStateControl has no entry for a transition with context, so enter SubInventory first (one no-context error log) and reopen with the real-play context (same steps as BlockDetailStateProbe)
        private static async UniTask<SubInventoryState> OpenSubInventory(UIStateControl uiStateControl, BlockGameObject block)
        {
            LogAssert.Expect(LogType.Error, "SubInventoryState: サブインベントリソースが指定されていません");
            await TransitionTo(uiStateControl, UIStateEnum.SubInventory);
            return await BlockSubInventoryOpener.Open(block);
        }

        private static async UniTask TransitionTo(UIStateControl uiStateControl, UIStateEnum state)
        {
            uiStateControl.RequestTransition(state);
            for (var i = 0; i < 120 && uiStateControl.CurrentState != state; i++) await UniTask.Yield();
            Assert.AreEqual(state, uiStateControl.CurrentState, $"{state} へ遷移していない");
        }

        // 開いたパネルと同じ個体の機械の値が届くまで待つ
        // Wait until the machine value for the same instance as the open panel arrives
        private static async UniTask<OpenBlockInventoryPanel> WaitMachinePresent(IMachineSectionModel machine, IBlockInventoryPanelSource panels)
        {
            for (var i = 0; i < 300; i++)
            {
                if (panels.Current.Value is OpenBlockInventoryPanel panel && machine.Current.Value.TryGetPresent(out var present) && present.IsFor(panel)) return panel;
                await UniTask.Yield();
            }
            Assert.Fail("開いた機械の区画の値が届かない");
            return null;
        }

        private static bool IsMachineSectionShown()
        {
            var section = GameUiRoot.Instance.GetRootElement().Q<VisualElement>("machine-section");
            if (section == null) return false;
            for (var current = section; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None) return false;
            }
            return true;
        }
    }
}
