using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Tooltip
{
    /// <summary>
    /// 宿主が出す顔の口
    /// Port for the face the host shows
    /// </summary>
    public interface IHoverTooltipFace
    {
        void SetText(UiText text);
        void AttachTo(VisualElement parent);
    }
}
