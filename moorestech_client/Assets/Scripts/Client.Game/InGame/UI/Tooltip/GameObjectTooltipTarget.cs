// [uGUI廃止Phase1] Web UI移行済みのため未メンテ・描画恒久停止。Phase2で削除予定（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] Unmaintained; rendering permanently disabled after the Web UI migration. Slated for deletion in Phase2 (docs/webui/ugui-retirement-plan.md)
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

        public void OnCursorEnter()
        {
            if (displayEnable) MouseCursorTooltip.Instance.Show(_tooltipOwner, new LocalizationKey(textKey));
        }
        
        public void OnCursorExit()
        {
            MouseCursorTooltip.Instance.Hide(_tooltipOwner);
        }
    }
}