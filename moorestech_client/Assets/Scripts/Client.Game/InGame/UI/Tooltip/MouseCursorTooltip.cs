// [uGUI廃止Phase1] uGUI描画は恒久停止・ビューは未メンテ。ただし本クラスは外部（Web UIブリッジ等）から参照中のため削除前に整理が必要（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] uGUI rendering is permanently disabled and the view is unmaintained, but this class is still referenced externally (e.g. Web UI bridge); untangle before deletion (docs/webui/ugui-retirement-plan.md)
using Client.Localization;
using TMPro;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.UIState;
using Mooresmaster.Localization.Generated;
using UniRx;

namespace Client.Game.InGame.UI.Tooltip
{
    /// <summary>
    ///     マウスカーソルのそばにアイテム名やTips、その他文章を表示するシステム
    /// </summary>
    public class MouseCursorTooltip : MonoBehaviour
    {
        [SerializeField] private GameObject itemNameBar;
        [SerializeField] private TMP_Text itemName;
        [SerializeField] private CanvasGroup canvasGroup;


        public static MouseCursorTooltip Instance { get; private set; }
        private readonly ReactiveProperty<TooltipPresentation> _presentation =
            new(TooltipPresentation.Hidden);

        private TooltipOwner _currentOwner;

        public IObservable<TooltipPresentation> OnPresentationChanged => _presentation;
        public TooltipPresentation GetPresentation() => _presentation.Value;

        private void Awake()
        {
            Instance = this;
            // Webモードではこのビューは恒久非表示。以後uGUIへは一切書き込まないためここで一度だけ消す
            // In web mode this view stays hidden forever, so it is cleared once here and never written again
            if (WebUiScreenGate.IsWebUiMode) canvasGroup.alpha = 0;
        }

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

            _currentOwner = owner;
            _presentation.Value = new TooltipPresentation(lines);

            // Webモードでは描画されないTMPへ表示文字列を組み立てない（毎フレーム呼ばれる経路のため）
            // In web mode no display string is built for the TMP that never renders, since this runs every frame
            if (WebUiScreenGate.IsWebUiMode) return;

            canvasGroup.alpha = 1;
            // uGUI側は行を改行連結して描画
            // The uGUI side joins lines with newlines
            itemName.text = string.Join("\n", lines.Select(line => Localize.GetFormatted(line.Key, line.TextParams)));
        }

        // 自分が出していない表示は消さない（毎フレームHideする書き手が他者の表示を潰さないため）
        // Never clear a tooltip shown by someone else, so writers that hide every frame cannot stomp on others
        public void Hide(TooltipOwner owner)
        {
            if (_currentOwner != owner) return;

            _currentOwner = null;
            _presentation.Value = TooltipPresentation.Hidden;

            if (!WebUiScreenGate.IsWebUiMode) canvasGroup.alpha = 0;
        }
    }
}
