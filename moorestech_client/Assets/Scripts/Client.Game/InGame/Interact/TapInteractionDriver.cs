using System.Collections.Generic;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;

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

        public UITransitContext Step(ITapInteractable target)
        {
            _lines.Clear();
            foreach (var action in target.Actions)
            {
                // 押されたアクションはヒントを畳んで即実行する（先に並べたヒントも用済み）
                // A pressed action folds the hints away and runs at once, discarding the ones already queued
                if (action.Key.GetKeyDown)
                {
                    Clear();
                    return action.Execute();
                }

                _lines.Add(new TooltipLine(action.HintKey, action.HintParams));
            }

            // 提示は同値比較で変化通知を抑えるため、使い回しリストではなく確定した配列を渡す
            // The presentation suppresses notifications by value comparison, so hand it a fixed array instead of the reused list
            MouseCursorTooltip.Instance.Show(TooltipOwner, _lines.ToArray());
            return null;
        }

        public void Clear()
        {
            MouseCursorTooltip.Instance.Hide(TooltipOwner);
        }
    }
}
