// [uGUI廃止Phase1] Web UI移行済みのため未メンテ・描画恒久停止。Phase2で削除予定（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] Unmaintained; rendering permanently disabled after the Web UI migration. Slated for deletion in Phase2 (docs/webui/ugui-retirement-plan.md)
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Common
{
    public class ProgressArrowView : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        
        public void SetProgress(float value)
        {
            slider.value = value;
        }
    }
}