using System.Collections.Generic;
using Client.Game.InGame.UI.Tooltip;
using Client.Input;

namespace Client.Game.InGame.Interact
{
    /// <summary>
    ///     単押し対象のヒント表示とキー実行。アクションの中身は知らない
    ///     Shows tap hints and executes on key press without knowing what the actions do
    /// </summary>
    public class TapInteractionDriver
    {
        private static readonly TooltipOwner TooltipOwner = new();

        private readonly List<TooltipLine> _lines = new();
        private readonly List<InputKey> _candidateKeys = new();

        // 直前に行を組み立てた対象。同じ対象を見続ける間は組み立て直さない
        // The target the lines were built for; looking at the same target rebuilds nothing
        private ITapInteractable _shownTarget;

        public InteractExecuteResult Step(ITapInteractable target, IInteractTargetSelector selector)
        {
            // 主対象の押下は主対象が引き受ける
            // A press the primary target offers is answered by the primary target
            if (TryExecutePressed(target, out var primaryResult)) return primaryResult;

            // 主対象が応じないキーは、そのキーに応じる候補へ回す（照準を占有する対象が他キーを塞がないため）
            // A key the primary target does not offer goes to whichever candidate answers it, so the aimed target never blocks the others
            if (TryExecuteOnRespondingCandidate(target, selector, out var alternateResult)) return alternateResult;

            ShowHints(target);
            return InteractExecuteResult.NotHandled;
        }

        public void Clear()
        {
            _shownTarget = null;
            MouseCursorTooltip.Instance.Hide(TooltipOwner);
        }

        private bool TryExecutePressed(ITapInteractable target, out InteractExecuteResult result)
        {
            result = InteractExecuteResult.NotHandled;
            if (target == null) return false;

            foreach (var action in target.Actions)
            {
                // 押されたアクションはヒントを畳んで即実行する（先に並べたヒントも用済み）
                // A pressed action folds the hints away and runs at once, discarding the ones already queued
                if (!action.Key.GetKeyDown) continue;

                Clear();
                result = action.Execute();
                return true;
            }

            return false;
        }

        private bool TryExecuteOnRespondingCandidate(ITapInteractable target, IInteractTargetSelector selector, out InteractExecuteResult result)
        {
            result = InteractExecuteResult.NotHandled;

            selector.CollectCandidateKeys(_candidateKeys);
            foreach (var key in _candidateKeys)
            {
                if (!key.GetKeyDown) continue;
                if (Offers(target, key)) continue;

                if (selector.SelectRespondingTo(key) is not ITapInteractable responder) continue;
                if (!TryExecuteKey(responder, key, out result)) continue;

                return true;
            }

            return false;
        }

        private bool TryExecuteKey(ITapInteractable responder, InputKey key, out InteractExecuteResult result)
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

        private void ShowHints(ITapInteractable target)
        {
            if (target == null)
            {
                Clear();
                return;
            }

            if (ReferenceEquals(_shownTarget, target)) return;

            _lines.Clear();
            foreach (var action in target.Actions) _lines.Add(new TooltipLine(action.HintKey, action.HintParams));

            // 提示は同値比較で変化通知を抑えるため、使い回しリストではなく確定した配列を渡す
            // The presentation suppresses notifications by value comparison, so hand it a fixed array instead of the reused list
            MouseCursorTooltip.Instance.Show(TooltipOwner, _lines.ToArray());
            _shownTarget = target;
        }

        private static bool Offers(ITapInteractable target, InputKey key)
        {
            if (target == null) return false;

            foreach (var action in target.Actions)
                if (action.Key == key)
                    return true;

            return false;
        }
    }
}
