using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Parts
{
    /// <summary>
    /// 1 つの値を描く区画の View（族の詳細・液体の区画が DetailSectionPresenter と組む）
    /// A section view drawing one value (family details and fluid sections pair it with DetailSectionPresenter); it never subscribes and only draws what it is given
    /// </summary>
    public interface IDetailSectionView<in T>
    {
        VisualElement Root { get; }
        void Render(T viewModel);
    }
}
