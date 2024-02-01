using System;
using Localization;
using TMPro;
using UnityEngine;

namespace MainGame.UnityView.UI.Util
{
    public interface IMouseCursorExplainer
    {
        public const int DefaultFontSize = 36;
        // TODO hotbarから毎フレーム呼び出されると常にfalseになってしまうので、何か実装方法を考えたいな、、
        public void Hide();
        public void Show(string key, int fontSize = DefaultFontSize);
        public void ShowText(string text, int fontSize = DefaultFontSize);
    }

    /// <summary>
    ///     マウスカーソルのそばにアイテム名やTips、その他文章を表示するシステム
    /// </summary>
    public class MouseCursorExplainer : MonoBehaviour, IMouseCursorExplainer
    {
        [SerializeField] private GameObject itemNameBar;
        [SerializeField] private TMP_Text itemName;
        [SerializeField] private CanvasGroup canvasGroup;


        public static IMouseCursorExplainer Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        public void Show(string key, int fontSize)
        {
            canvasGroup.alpha = 1;
            itemName.text = Localize.Get(key);
            itemName.fontSize = fontSize;
        }

        public void ShowText(string text, int fontSize = IMouseCursorExplainer.DefaultFontSize)
        {
            canvasGroup.alpha = 1;
            itemName.text = text;
            itemName.fontSize = fontSize;
        }


        public void Hide()
        {
            canvasGroup.alpha = 0;
        }
    }
}