using Client.Ui.State.BlockInventory.Panel;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Registry
{
    /// <summary>
    /// 題の操作が無いとき・列車の本文が未登録のときの Null Object（Root は空の VisualElement、Attach・Detach は何もしない）
    /// Null Objects for no title action or an unregistered train body (empty Root, no-op Attach and Detach)
    /// </summary>
    public sealed class EmptyBlockInventoryPart : IBlockInventoryPart
    {
        public VisualElement Root { get; private set; } = new();

        public void Attach(OpenBlockInventoryPanel panel)
        {
        }

        public void Detach()
        {
        }
    }

    public sealed class EmptyTrainInventoryPart : ITrainInventoryPart
    {
        public VisualElement Root { get; private set; } = new();

        public void Attach(OpenTrainInventoryPanel panel)
        {
        }

        public void Detach()
        {
        }
    }
}
