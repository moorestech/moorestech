using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Parts
{
    /// <summary>
    /// 1 つの値を描く区画の View。購読しない
    /// Section view drawing one value; never subscribes
    /// </summary>
    public interface IDetailSectionView<in T>
    {
        VisualElement Root { get; }
        void Render(T viewModel);
    }
}
