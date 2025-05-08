using UnityEngine;
using UnityEngine.EventSystems;

namespace Client.Game.InGame.UI.Tooltip
{
    /// <summary>
    ///     UIにアタッチして、そのUI要素にマウスカーソルが乗ったら文字列を表示するシステム
    /// </summary>
    public class UGuiTooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        public bool PointerStay { get; private set; }
        
        /// <summary>
        ///     カーソルに表示するテキスト
        /// </summary>
        [SerializeField,Multiline] private string textKey;
        
        /// <summary>
        ///     表示するかどうか
        /// </summary>
        [SerializeField] private bool displayEnable;
        
        [SerializeField] private int fontSize = IMouseCursorTooltip.DefaultFontSize;
        private bool _isLocalize;
        
        public void OnPointerMove(PointerEventData eventData)
        {
            PointerStay = true;
            UpdateMouseCursorTooltip();
        }
        
        
        public void SetText(string text, bool isLocalize = true)
        {
            _isLocalize = isLocalize;
            textKey = text;
        }
        
        /// <summary>
        ///     フラグが変更されたあと表示、非表示設定を行う
        /// </summary>
        private void UpdateMouseCursorTooltip()
        {
            //表示する設定で、ポインターが乗ったので表示
            if (PointerStay && displayEnable)
            {
                MouseCursorTooltip.Instance.Show(textKey, fontSize, _isLocalize);
                return;
            }
            
            if (!PointerStay || //ポインターから外れたので非表示
                PointerStay && !displayEnable) //ポインターからは外れてないけど非表示設定なったから非表示
                MouseCursorTooltip.Instance.Hide();
        }
        
        
        #region flagController
        
        public void DisplayEnable(bool enable)
        {
            displayEnable = enable;
            if (PointerStay) UpdateMouseCursorTooltip();
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            PointerStay = true;
            UpdateMouseCursorTooltip();
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            PointerStay = false;
            UpdateMouseCursorTooltip();
        }
        
        private void OnDestroy()
        {
            PointerStay = false;
            UpdateMouseCursorTooltip();
        }
        
        private void OnDisable()
        {
            PointerStay = false;
            UpdateMouseCursorTooltip();
        }
        
        #endregion
    }
}