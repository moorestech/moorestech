using Client.Localization;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.UI.Util
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
        
        
        public static IMouseCursorTooltip Instance { get; private set; }
        
        private void Awake()
        {
            Instance = this;
        }
        
        public void Show(string key, int fontSize = IMouseCursorTooltip.DefaultFontSize, bool isLocalize = true)
        {
            canvasGroup.alpha = 1;
            itemName.text = isLocalize ? Localize.Get(key) : key;
            itemName.fontSize = fontSize;
        }
        
        public void Hide()
        {
            canvasGroup.alpha = 0;
        }
    }
}