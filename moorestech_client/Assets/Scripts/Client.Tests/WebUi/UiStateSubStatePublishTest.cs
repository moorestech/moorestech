using System;
using Client.Game.InGame.UI.UIState.State.NestedPause;
using Client.WebUiHost.Game.Topics;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.WebUi
{
    // ui_state.current の subState 配信規約を固定する。Web側ルーティングはこの語彙だけを見る（ADR 0035）
    // Locks the subState publishing contract of ui_state.current; web-side routing reads only this vocabulary (ADR 0035)
    public class UiStateSubStatePublishTest
    {
        // 入れ子ポーズを持たない画面はsubState自体を持たず、Web側は素の画面として扱う
        // A screen without a nested pause carries no subState at all, so the web treats it as a plain screen
        [Test]
        public void ScreenWithoutNestedPausePublishesNoSubState()
        {
            Assert.IsNull(UiStateTopic.ResolveSubState(null));
        }

        // 配信名はenum名そのもの。ここがずれるとWeb側のポーズ画面昇格が丸ごと止まる
        // The published name is the enum name itself; drift here silently stops the web's pause-screen promotion
        [Test]
        public void NestedPauseScreenPublishesItsSubStateName()
        {
            Assert.AreEqual("GameScreen", UiStateTopic.ResolveSubState(new NestedPauseScreenStub(NestedPauseSubStateEnum.GameScreen)));
            Assert.AreEqual("PauseMenuScreen", UiStateTopic.ResolveSubState(new NestedPauseScreenStub(NestedPauseSubStateEnum.PauseMenuScreen)));
        }

        private class NestedPauseScreenStub : INestedPauseScreenState
        {
            public NestedPauseSubStateEnum SubState { get; }
            public IObservable<Unit> OnPresentationChanged => Observable.Never<Unit>();

            public NestedPauseScreenStub(NestedPauseSubStateEnum subState)
            {
                SubState = subState;
            }

            public bool RequestClosePauseMenu()
            {
                return false;
            }
        }
    }
}
