using Client.Localization;
using TMPro;
using UnityEngine;
using System;
using Client.Game.InGame.UI.UIState;
using UniRx;

namespace Client.Game.InGame.UI.Tooltip
{
    public interface IMouseCursorTooltip
    {
        public const int DefaultFontSize = 36;
        
        // TODO hotbarから毎フレーム呼び出されると常にfalseになってしまうので、何か実装方法を考えたいな、、
        public void Hide();
        public void Show(string key, int fontSize = DefaultFontSize, bool isLocalize = true);
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
        private readonly ReactiveProperty<TooltipPresentation> _presentation = new(new TooltipPresentation(false, "", IMouseCursorTooltip.DefaultFontSize));

        public IObservable<TooltipPresentation> OnPresentationChanged => _presentation;
        public TooltipPresentation GetPresentation() => _presentation.Value;
        
        private void Awake()
        {
            Instance = this;
        }
        
        public void Show(string key, int fontSize = IMouseCursorTooltip.DefaultFontSize, bool isLocalize = true)
        {
            canvasGroup.alpha = WebUiScreenGate.IsWebUiMode ? 0 : 1;
            itemName.text = isLocalize ? Localize.Get(key) : key;
            itemName.fontSize = fontSize;
            _presentation.Value = new TooltipPresentation(true, key, fontSize);
        }
        
        public void Hide()
        {
            canvasGroup.alpha = 0;
            _presentation.Value = new TooltipPresentation(false, "", IMouseCursorTooltip.DefaultFontSize);
        }
    }

    public class TooltipPresentation
    {
        public readonly bool Visible;
        public readonly string TextKey;
        public readonly int FontSize;

        public TooltipPresentation(bool visible, string textKey, int fontSize)
        {
            Visible = visible;
            TextKey = textKey;
            FontSize = fontSize;
        }
    }
}
