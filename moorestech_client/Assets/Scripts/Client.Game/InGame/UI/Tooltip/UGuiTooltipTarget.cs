using UnityEngine;
using UnityEngine.EventSystems;

namespace Client.Game.InGame.UI.Tooltip
{
    /// <summary>
    ///     UIにアタッチして、そのUI要素にマウスカーソルが乗ったら文字列を表示するシステム
    /// </summary>
    public class UGuiTooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        /// <summary>
        ///     カーソルに表示するテキスト
        /// </summary>
        [SerializeField,Multiline] private string textKey;
        
        /// <summary>
        ///     表示するかどうか
        /// </summary>
        [SerializeField] private bool displayEnable;
        
        [SerializeField] private int fontSize = IMouseCursorTooltip.DefaultFontSize;
        [SerializeField] private bool localize = true;
        
        private bool _pointerStay;
        
        public void OnPointerMove(PointerEventData eventData)
        {
            _pointerStay = true;
            UpdateMouseCursorTooltip();
        }
        
        
        public void SetText(string text, bool isLocalize = true)
        {
            localize = isLocalize;
            textKey = text;
        }
        
        /// <summary>
        ///     フラグが変更されたあと表示、非表示設定を行う
        /// </summary>
        private void UpdateMouseCursorTooltip()
        {
            //表示する設定で、ポインターが乗ったので表示
            if (_pointerStay && displayEnable)
            {
                MouseCursorTooltip.Instance.Show(textKey, fontSize, localize);
                return;
            }
            
            if (!_pointerStay || //ポインターから外れたので非表示
                _pointerStay && !displayEnable) //ポインターからは外れてないけど非表示設定なったから非表示
                MouseCursorTooltip.Instance.Hide();
        }
        
        
        #region flagController
        
        public void DisplayEnable(bool enable)
        {
            displayEnable = enable;
            if (_pointerStay) UpdateMouseCursorTooltip();
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerStay = true;
            UpdateMouseCursorTooltip();
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerStay = false;
            UpdateMouseCursorTooltip();
        }
        
        private void OnDestroy()
        {
            _pointerStay = false;
            UpdateMouseCursorTooltip();
        }
        
        private void OnDisable()
        {
            _pointerStay = false;
            UpdateMouseCursorTooltip();
        }
        
        #endregion
    }
}