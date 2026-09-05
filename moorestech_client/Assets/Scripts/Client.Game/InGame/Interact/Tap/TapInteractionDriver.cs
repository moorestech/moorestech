using System.Collections.Generic;
using Client.Game.InGame.Interact.Selection;
using Client.Game.InGame.UI.Tooltip;
using Client.Input;

namespace Client.Game.InGame.Interact.Tap
{
    /// <summary>
    ///     単押し対象のヒント表示とキー実行。アクションの中身は知らない
    ///     Shows tap hints and executes on key press without knowing what the actions do
    /// </summary>
    public class TapInteractionDriver
    {
        private static readonly TooltipOwner TooltipOwner = new();

        private readonly IMouseCursorTooltip _tooltip;
        private readonly List<TooltipLine> _lines = new();
        private readonly List<InputKey> _candidateKeys = new();

        // 直前に行を組み立てた対象
        // The target the lines were built for
        private ITapInteractable _shownTarget;

        public TapInteractionDriver(IMouseCursorTooltip tooltip)
        {
            _tooltip = tooltip;
        }

        public InteractExecuteResult Step(ITapInteractable target, IInteractSelection selection)
        {
            // 主対象の押下は主対象が引き受ける
            // A press the primary target offers is answered by the primary target
            if (TryExecutePressed(out var primaryResult)) return primaryResult;

            // 主対象が応じないキーは応じる候補へ回す
            // A key the primary target does not offer goes to whichever candidate answers it
            if (TryExecuteOnRespondingCandidate(out var alternateResult)) return alternateResult;

            ShowHints();
            return InteractExecuteResult.NotHandled;

            #region Internal

            bool TryExecutePressed(out InteractExecuteResult result)
            {
                result = InteractExecuteResult.NotHandled;
                if (target == null) return false;

                foreach (var action in target.Actions)
                {
                    // 押されたアクションはヒントを畳んで即実行する
                    // A pressed action folds the hints away and runs at once
                    if (!action.Key.GetKeyDown) continue;

                    Clear();
                    result = action.Execute();
                    return true;
                }

                return false;
            }

            bool TryExecuteOnRespondingCandidate(out InteractExecuteResult result)
            {
                result = InteractExecuteResult.NotHandled;

                selection.CollectCandidateKeys(_candidateKeys);
                foreach (var key in _candidateKeys)
                {
                    if (!key.GetKeyDown) continue;
                    if (Offers(key)) continue;

                    if (selection.SelectRespondingTo(key) is not ITapInteractable responder) continue;
                    if (!TryExecuteKey(responder, key, out result)) continue;

                    return true;
                }

                return false;
            }

            bool TryExecuteKey(ITapInteractable responder, InputKey key, out InteractExecuteResult result)
            {
                foreach (var action in responder.Actions)
                {
                    if (action.Key != key) continue;

                    Clear();
                    result = action.Execute();
                    return true;
                }

                result = InteractExecuteResult.NotHandled;
                return false;
            }

            void ShowHints()
            {
                if (target == null)
                {
                    Clear();
                    return;
                }

                if (ReferenceEquals(_shownTarget, target)) return;

                _lines.Clear();
                foreach (var action in target.Actions) _lines.Add(new TooltipLine(action.HintKey, action.HintParams));

                // 提示は同値比較で変化通知を抑えるため確定した配列を渡す
                // The presentation compares by value, so hand it a fixed array instead of the reused list
                _tooltip.Show(TooltipOwner, _lines.ToArray());
                _shownTarget = target;
            }

            bool Offers(InputKey key)
            {
                if (target == null) return false;

                foreach (var action in target.Actions)
                    if (action.Key == key)
                        return true;

                return false;
            }

            #endregion
        }

        public void Clear()
        {
            _shownTarget = null;
            _tooltip.Hide(TooltipOwner);
        }
    }
}
