using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery
{
    /// <summary>
    /// 部品ギャラリーの見本 1 件。部品レーンは部品ごとに 1 つ実装し、全状態を並べた見本を返す（引数なしの ctor が要る）
    /// One component gallery entry; each component lane implements one returning a sample with every state (needs a parameterless ctor)
    /// </summary>
    public interface IUiGalleryEntry
    {
        UiGalleryFamily Family { get; }
        string ComponentName { get; }
        VisualElement CreateSample();
    }
}
