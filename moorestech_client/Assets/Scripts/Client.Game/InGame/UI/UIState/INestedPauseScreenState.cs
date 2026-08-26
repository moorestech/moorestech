using System;
using UniRx;

namespace Client.Game.InGame.UI.UIState
{
    // 入れ子ポーズメニューを持つ画面。Web境界はこのIFだけを見て分岐する
    // A screen that owns a nested pause menu; the web boundary branches on this interface alone
    public interface INestedPauseScreenState
    {
        // Web配信用のサブステート名
        // Sub-state name published to the web
        string SubStateName { get; }
        
        IObservable<Unit> OnPresentationChanged { get; }
        
        // 入れ子ポーズだけを閉じる。実際に閉じたときだけtrue
        // Closes only the nested pause menu; true only when it actually closed
        bool RequestClosePauseMenu();
    }
}
