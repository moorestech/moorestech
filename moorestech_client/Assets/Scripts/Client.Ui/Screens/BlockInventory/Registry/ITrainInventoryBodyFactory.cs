using Client.Ui.State.BlockInventory.Panel;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Registry
{
    /// <summary>
    /// 拡張口 (4): source=train の本文（Web TrainInventoryBody。登録は 1 本だけ）
    /// Extension port (4): the source=train body (Web TrainInventoryBody; exactly one registration)
    /// </summary>
    public interface ITrainInventoryBodyFactory
    {
        ITrainInventoryPart Create();
    }

    // 列車の本文の Presenter。Attach で列車の詳細 Model（S4-train の State）を購読し View へ流す
    // Train body presenter; Attach subscribes the train detail model (S4-train State) into the view
    public interface ITrainInventoryPart
    {
        VisualElement Root { get; }
        void Attach(OpenTrainInventoryPanel panel);
        void Detach();
    }
}
