using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery
{
    /// <summary>
    /// ギャラリーの見本1件。全状態を並べて返す（引数なしctorが要る）
    /// One gallery entry returning every state (needs a parameterless ctor)
    /// </summary>
    public interface IUiGalleryEntry
    {
        UiGalleryFamily Family { get; }
        string ComponentName { get; }
        VisualElement CreateSample();
    }
}
