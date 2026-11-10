using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Family
{
    /// <summary>
    /// 1 つの値を描く区画の View。購読せず、渡された値を描くだけ
    /// A section view drawing one value; it never subscribes and only draws what it is given
    /// </summary>
    public interface IDetailSectionView<in T>
    {
        VisualElement Root { get; }
        void Render(T viewModel);
    }
}
