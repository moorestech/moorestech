using UnityEngine;
using UnityEngine.EventSystems;

namespace Client.Game.InGame.UI.Util
{
    /// <summary>
    ///     UIにアタッチして、そのUI要素にマウスカーソルが乗ったら文字列を表示するシステム
    /// </summary>
    public class UIEnterExplainerController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        /// <summary>
        ///     カーソルに表示するテキスト
        /// </summary>
        [SerializeField] private string textKey;
        
        /// <summary>
        ///     表示するかどうか
        /// </summary>
        [SerializeField] private bool displayEnable;
        
        [SerializeField] private int fontSize = IMouseCursorExplainer.DefaultFontSize;
        private bool _isLocalize;
        
        private bool _pointerStay;
        
        public void OnPointerMove(PointerEventData eventData)
        {
            _pointerStay = true;
            UpdateMouseCursorExplainer();
        }
        
        
        public void SetText(string text, bool isLocalize = true)
        {
            _isLocalize = isLocalize;
            textKey = text;
        }
        
        /// <summary>
        ///     フラグが変更されたあと表示、非表示設定を行う
        /// </summary>
        private void UpdateMouseCursorExplainer()
        {
            //表示する設定で、ポインターが乗ったので表示
            if (_pointerStay && displayEnable)
            {
                MouseCursorExplainer.Instance.Show(textKey, fontSize, _isLocalize);
                return;
            }
            
            if (!_pointerStay || //ポインターから外れたので非表示
                _pointerStay && !displayEnable) //ポインターからは外れてないけど非表示設定なったから非表示
                MouseCursorExplainer.Instance.Hide();
        }
        
        
        #region フラグコントローラー
        
        public void DisplayEnable(bool enable)
        {
            displayEnable = enable;
            if (_pointerStay) UpdateMouseCursorExplainer();
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerStay = true;
            UpdateMouseCursorExplainer();
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerStay = false;
            UpdateMouseCursorExplainer();
        }
        
        private void OnDestroy()
        {
            _pointerStay = false;
            UpdateMouseCursorExplainer();
        }
        
        private void OnDisable()
        {
            _pointerStay = false;
            UpdateMouseCursorExplainer();
        }
        
        #endregion
    }
}