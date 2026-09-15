using System;
using UniRx;

namespace Client.WebUiHost.Game.StartGates
{
    // 開始ゲートの待機状態だけをtopicへ見せる窓口。応答の受け方はゲートごとに違うのでここには載せない
    // The window exposing only a start gate's waiting state to its topic; how answers arrive differs per gate, so that stays out
    internal interface IStartGateWaitState
    {
        bool IsWaiting { get; }
        IObservable<Unit> OnWaitingChanged { get; }
    }
}
