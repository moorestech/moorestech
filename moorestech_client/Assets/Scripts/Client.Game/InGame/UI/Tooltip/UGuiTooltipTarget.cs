// [uGUI廃止Phase1] Web UI移行済みのため未メンテ・描画恒久停止。Phase2で削除予定（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] Unmaintained; rendering permanently disabled after the Web UI migration. Slated for deletion in Phase2 (docs/webui/ugui-retirement-plan.md)
using System;
using Mooresmaster.Localization.Generated;
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
        ///     カーソルに表示するテキストの辞書キー
        /// </summary>
        [SerializeField,Multiline] private string textKey;
        
        /// <summary>
        ///     辞書テンプレートの{p0}へ差し込む値
        /// </summary>
        [SerializeField] private string[] textParams = Array.Empty<string>();
        
        /// <summary>
        ///     表示するかどうか
        /// </summary>
        [SerializeField] private bool displayEnable;

        private bool _pointerStay;
        
        public void OnPointerMove(PointerEventData eventData)
        {
            _pointerStay = true;
            UpdateMouseCursorTooltip();
        }
        
        
        public void SetText(LocalizationKey key, string[] tooltipTextParams)
        {
            textKey = key.Key;
            textParams = tooltipTextParams;
        }
        
        /// <summary>
        ///     フラグが変更されたあと表示、非表示設定を行う
        /// </summary>
        private void UpdateMouseCursorTooltip()
        {
            // Webモードやシーン破棄中はtooltipシングルトンが存在しない（ライフサイクル境界）
            // The tooltip singleton may not exist in Web mode or during scene teardown (lifecycle boundary)
            if (MouseCursorTooltip.Instance == null) return;

            //表示する設定で、ポインターが乗ったので表示
            if (_pointerStay && displayEnable)
            {
                MouseCursorTooltip.Instance.Show(new LocalizationKey(textKey), textParams);
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