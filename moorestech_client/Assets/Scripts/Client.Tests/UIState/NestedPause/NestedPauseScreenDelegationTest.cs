using System.Reflection;
using System.Runtime.Serialization;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.NestedPause;
using Client.Game.InGame.UI.UIState.State.Skit;
using Client.Game.InGame.UI.UIState.State.TrainHUDScreen;
using NUnit.Framework;

namespace Client.Tests.UIState.NestedPause
{
    /// <summary>
    ///     入れ子ポーズを持つ実画面が宣言をサブステートへ委譲していることを確認
    ///     Verifies the real nested-pause screens delegate their declaration to the showing sub-state
    /// </summary>
    public class NestedPauseScreenDelegationTest
    {
        [Test]
        public void スキット画面は再生中は移動を許しポーズ中は止める()
        {
            var skitState = CreateScreenWithSubStates<SkitState>(new SkitGameScreenSubState(null));

            Assert.IsFalse(skitState.LocksPlayerMovement(), "スキット再生中に移動が止まっている");

            SetCurrentSubState(skitState, NestedPauseSubStateEnum.PauseMenuScreen);
            Assert.IsTrue(skitState.LocksPlayerMovement(), "スキット中のポーズで移動が止まらない");
        }

        [Test]
        public void 列車HUDは操作中もポーズ中も移動を止める()
        {
            var trainHudState = CreateScreenWithSubStates<TrainHUDScreenState>(new TrainHudGameScreenSubState(null));

            Assert.IsTrue(trainHudState.LocksPlayerMovement(), "列車操作中に自機が歩ける（乗車RPC待ちの窓が開く）");

            SetCurrentSubState(trainHudState, NestedPauseSubStateEnum.PauseMenuScreen);
            Assert.IsTrue(trainHudState.LocksPlayerMovement(), "列車HUDのポーズで移動が止まらない");
        }

        // 画面本体のctorはDI依存を要求するため、宣言の委譲だけを読むよう入れ子ステートマシンだけを差し込む
        // The screens' constructors demand DI dependencies, so only the nested state machine is injected to read the delegation
        private static TScreen CreateScreenWithSubStates<TScreen>(INestedPauseSubState gameScreenSubState) where TScreen : IUIState
        {
            var screen = (TScreen)FormatterServices.GetUninitializedObject(typeof(TScreen));
            var subStateController = new NestedPauseSubStateController(gameScreenSubState, null);
            typeof(TScreen).GetField("_subStateController", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(screen, subStateController);
            return screen;
        }

        // 遷移はOnEnter/OnExitを走らせてDI依存に触るため、公開値だけを直接書いてサブステートを切り替える
        // Transitions would run OnEnter/OnExit and touch DI dependencies, so the exposed sub-state is written directly
        private static void SetCurrentSubState(IUIState screen, NestedPauseSubStateEnum subState)
        {
            var subStateController = screen.GetType()
                .GetField("_subStateController", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(screen);
            subStateController.GetType()
                .GetField("<CurrentState>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(subStateController, subState);
        }
    }
}
