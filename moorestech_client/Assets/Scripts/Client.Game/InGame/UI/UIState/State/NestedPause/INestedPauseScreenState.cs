using System;
using UniRx;

namespace Client.Game.InGame.UI.UIState.State.NestedPause
{
    // 入れ子ポーズメニューを持つ画面。Web境界はこのIFだけを見て分岐する
    // A screen that owns a nested pause menu; the web boundary branches on this interface alone
    public interface INestedPauseScreenState
    {
        // 表示中のサブステート。Web語彙への文字列化は境界側だけが行う
        // The sub-state currently showing; only the web boundary turns it into a string
        NestedPauseSubStateEnum SubState { get; }
        
        IObservable<Unit> OnPresentationChanged { get; }
        
        // 入れ子ポーズだけを閉じる。実際に閉じたときだけtrue
        // Closes only the nested pause menu; true only when it actually closed
        bool RequestClosePauseMenu();
    }
}
