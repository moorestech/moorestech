using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Localization.Generated;
using UniRx;

namespace Client.Game.InGame.UI.Tooltip
{
    /// <summary>
    ///     カーソル付近に出す文言の所有者付き表示状態。主表示の下に補足行を1組添えられる。描画は Web UI が tooltip topic 経由で行う
    ///     Owner-tracked state of the cursor tooltip with one supplement group under the main show; the Web UI renders it through the tooltip topic
    /// </summary>
    public class MouseCursorTooltipState : IMouseCursorTooltip
    {
        private readonly ReactiveProperty<TooltipPresentation> _presentation = new(TooltipPresentation.Hidden);

        private IReadOnlyList<TooltipLine> _mainLines = Array.Empty<TooltipLine>();
        private TooltipOwner _supplementOwner;
        private IReadOnlyList<TooltipLine> _supplementLines = Array.Empty<TooltipLine>();

        public TooltipOwner CurrentOwner { get; private set; }

        public IObservable<TooltipPresentation> OnPresentationChanged => _presentation;
        public TooltipPresentation GetPresentation() => _presentation.Value;

        public void Show(TooltipOwner owner, LocalizationKey key)
        {
            Show(owner, key, Array.Empty<string>());
        }

        public void Show(TooltipOwner owner, LocalizationKey key, IReadOnlyList<string> textParams)
        {
            Show(owner, new[] { new TooltipLine(key, textParams) });
        }

        // 表示したものは最後に呼んだ主体のものになる（所有権は毎回Showした側へ移る）
        // What is shown belongs to the last caller; ownership moves to whoever showed it
        public void Show(TooltipOwner owner, IReadOnlyList<TooltipLine> lines)
        {
            // 行が無い表示要求は非表示と同義（表示状態は行の有無から導出されるため）
            // A show request without lines means hidden, because visibility is derived from the lines
            if (lines.Count == 0)
            {
                Hide(owner);
                return;
            }

            CurrentOwner = owner;
            _mainLines = lines;
            Publish();
        }

        // 自分が出していない表示は消さない（毎フレームHideする書き手が他者の表示を潰さないため）
        // Never clear a tooltip shown by someone else, so writers that hide every frame cannot stomp on others
        public void Hide(TooltipOwner owner)
        {
            if (CurrentOwner != owner) return;

            CurrentOwner = null;
            _mainLines = Array.Empty<TooltipLine>();
            Publish();
        }

        // 補足も最後に出した主体のものになり、主表示の所有者とは独立に扱う
        // The supplement also belongs to its last caller and is tracked apart from the main owner
        public void ShowSupplement(TooltipOwner owner, IReadOnlyList<TooltipLine> lines)
        {
            if (lines.Count == 0)
            {
                HideSupplement(owner);
                return;
            }

            _supplementOwner = owner;
            _supplementLines = lines;
            Publish();
        }

        public void HideSupplement(TooltipOwner owner)
        {
            if (_supplementOwner != owner) return;

            _supplementOwner = null;
            _supplementLines = Array.Empty<TooltipLine>();
            Publish();
        }

        // 主表示の後ろに補足を並べた1つの表示にする。同じ内容なら ReactiveProperty が通知を抑える
        // Compose one presentation with the supplement after the main lines; the ReactiveProperty suppresses unchanged content
        private void Publish()
        {
            IReadOnlyList<TooltipLine> lines = _supplementLines.Count == 0 ? _mainLines : _mainLines.Concat(_supplementLines).ToArray();
            _presentation.Value = new TooltipPresentation(lines);
        }
    }
}
