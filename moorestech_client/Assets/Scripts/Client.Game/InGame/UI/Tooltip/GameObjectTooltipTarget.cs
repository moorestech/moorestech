// ツールチップ入力アダプタ
// Tooltip input adapter
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Game.InGame.UI.Tooltip
{
    public class GameObjectTooltipTarget : MonoBehaviour
    {
        /// <summary>
        ///     カーソルに表示するテキストのキー
        /// </summary>
        [SerializeField] private string textKey;
        
        /// <summary>
        ///     表示するかどうか
        /// </summary>
        [SerializeField] private bool displayEnable = true;

        private readonly TooltipOwner _tooltipOwner = new();

        public void OnCursorEnter(IMouseCursorTooltip tooltip)
        {
            if (displayEnable) tooltip.Show(_tooltipOwner, new LocalizationKey(textKey));
        }

        public void OnCursorExit(IMouseCursorTooltip tooltip)
        {
            tooltip.Hide(_tooltipOwner);
        }
    }
}