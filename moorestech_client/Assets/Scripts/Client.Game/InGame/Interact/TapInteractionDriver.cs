using System.Collections.Generic;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.Interact
{
    /// <summary>
    ///     単押し対象のヒント表示とキー実行。アクションの中身は知らない
    ///     Shows tap hints and executes on key press without knowing what the actions do
    /// </summary>
    public class TapInteractionDriver
    {
        private static readonly TooltipOwner TooltipOwner = new();

        private readonly Dictionary<InputKey, TapKeyPressLatch> _latches = new();
        private readonly List<TooltipLine> _lines = new();

        public UITransitContext Step(ITapInteractable target)
        {
            _lines.Clear();
            foreach (var action in target.Actions)
            {
                // 押されたアクションはヒントを畳んで即実行する（先に並べたヒントも用済み）
                // A pressed action folds the hints away and runs at once, discarding the ones already queued
                // 掛け金は購読を張った次フレームから効くため、本番の押下判定も併記して初回フレームを取りこぼさない
                // The latch only works from the frame after it subscribes, so the production press check covers the first frame
                if (GetLatch(action.Key).WasPressedThisFrame || action.Key.GetKeyDown)
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

            #region Internal

            TapKeyPressLatch GetLatch(InputKey key)
            {
                if (_latches.TryGetValue(key, out var latch)) return latch;

                latch = new TapKeyPressLatch(key);
                _latches.Add(key, latch);
                return latch;
            }

            #endregion
        }

        public void Clear()
        {
            MouseCursorTooltip.Instance.Hide(TooltipOwner);
        }

        /// <summary>
        ///     押下フレームを購読で受け取る掛け金。毎フレームのポーリングをやめ、押下のあったフレームでだけ成立する
        ///     Latch fed by subscription instead of per-frame polling; it holds only on the frame the key went down
        /// </summary>
        private sealed class TapKeyPressLatch
        {
            // 未押下と「フレーム0で押された」を取り違えないよう範囲外から始める
            // Starts out of range so "never pressed" is never mistaken for a press on frame 0
            private int _pressedFrame = -1;

            public bool WasPressedThisFrame => _pressedFrame == Time.frameCount;

            public TapKeyPressLatch(InputKey key)
            {
                key.OnGetKeyDown += () => _pressedFrame = Time.frameCount;
            }
        }
    }
}
