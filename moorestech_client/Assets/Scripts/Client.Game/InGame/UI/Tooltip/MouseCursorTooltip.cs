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
    public interface IMouseCursorTooltip
    {
        // TODO hotbarから毎フレーム呼び出されると常にfalseになってしまうので、何か実装方法を考えたいな、、
        public void Hide();
        public void Show(LocalizationKey key);
        public void Show(LocalizationKey key, IReadOnlyList<string> textParams);
    }
    
    /// <summary>
    ///     マウスカーソルのそばにアイテム名やTips、その他文章を表示するシステム
    /// </summary>
    public class MouseCursorTooltip : MonoBehaviour, IMouseCursorTooltip
    {
        [SerializeField] private GameObject itemNameBar;
        [SerializeField] private TMP_Text itemName;
        [SerializeField] private CanvasGroup canvasGroup;
        
        
        public static MouseCursorTooltip Instance { get; private set; }
        private readonly ReactiveProperty<TooltipPresentation> _presentation =
            new(TooltipPresentation.Hidden);

        public IObservable<TooltipPresentation> OnPresentationChanged => _presentation;
        public TooltipPresentation GetPresentation() => _presentation.Value;
        
        private void Awake()
        {
            Instance = this;
        }
        
        public void Show(LocalizationKey key)
        {
            Show(key, Array.Empty<string>());
        }

        public void Show(LocalizationKey key, IReadOnlyList<string> textParams)
        {
            canvasGroup.alpha = WebUiScreenGate.IsWebUiMode ? 0 : 1;
            itemName.text = InterpolateTextParams(Localize.Get(key), textParams);
            _presentation.Value = new TooltipPresentation(true, key.Key, textParams);
        }
        
        public void Hide()
        {
            canvasGroup.alpha = 0;
            _presentation.Value = TooltipPresentation.Hidden;
        }
        
        // 辞書テンプレートの{p0}プレースホルダを埋める（Web側translatorと同じ規約）
        // Fill the {p0} placeholders of the dictionary template, matching the web translator convention
        private static string InterpolateTextParams(string template, IReadOnlyList<string> textParams)
        {
            var text = template;
            for (var index = 0; index < textParams.Count; index++)
            {
                text = text.Replace($"{{p{index}}}", textParams[index]);
            }
            
            return text;
        }
    }

    /// <summary>
    ///     表示内容が同じなら同値として扱い、毎フレーム作り直される配列で変化通知が湧かないようにする
    ///     Equal content compares equal, so the array rebuilt every frame never raises a change notification
    /// </summary>
    public readonly struct TooltipPresentation : IEquatable<TooltipPresentation>
    {
        public static readonly TooltipPresentation Hidden =
            new(false, "", Array.Empty<string>());

        public readonly bool Visible;
        public readonly string TextKey;
        public readonly IReadOnlyList<string> TextParams;

        public TooltipPresentation(bool visible, string textKey, IReadOnlyList<string> textParams)
        {
            Visible = visible;
            TextKey = textKey;
            TextParams = textParams;
        }

        public bool Equals(TooltipPresentation other)
        {
            return Visible == other.Visible && TextKey == other.TextKey && TextParams.SequenceEqual(other.TextParams);
        }

        public override bool Equals(object obj)
        {
            return obj is TooltipPresentation other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = HashCode.Combine(Visible, TextKey, TextParams.Count);
            foreach (var textParam in TextParams) hash = HashCode.Combine(hash, textParam);
            return hash;
        }
    }
}
