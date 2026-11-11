// S/Client.Ui/Screens/Skit/SkitEnterKeyBinding.cs
using System;
using UniRx;

namespace Client.Ui.Screens.Skit
{
    /// <summary>
    /// Enter の押下を会話窓の評価器へつなぐ。全体非表示の間は使わない（Web は会話 UI ごと外れている）。画面と同じゲーム寿命なので購読は張りっぱなしで IDisposable を付けない
    /// Wires Enter presses to the dialogue window evaluator; unused while the whole UI is hidden (on the Web the skit UI is unmounted); game-lifetime like the screen, so the subscription stays and it carries no IDisposable
    /// </summary>
    public sealed class SkitEnterKeyBinding
    {
        public SkitEnterKeyBinding(IObservable<Unit> enterPressed, SkitScreenView view, IReadOnlyReactiveProperty<bool> wholeUiShown)
        {
            enterPressed.Subscribe(_ =>
            {
                // silent-ok: 全体非表示の間は Web も会話 UI が無く Enter を受けない / Ctrl+U で戻すと次の Enter から効く（SkitEnterKeyTest）
                if (!wholeUiShown.Value) return;
                view.HandleEnterKey();
            });
        }
    }
}
