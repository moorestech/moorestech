using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Tooltip
{
    /// <summary>
    /// 宿主が出す顔の口。Foundation は部品を参照できないので、部品の TooltipFace がこれを実装し Client.Starter が渡す
    /// Port for the face the host shows; Foundation cannot reference components, so the TooltipFace component implements this and Client.Starter hands it in
    /// </summary>
    public interface IHoverTooltipFace
    {
        void SetText(UiText text);
        void AttachTo(VisualElement parent);
    }
}
