using UnityEngine.UIElements;

namespace Client.UitkPoc.Shell
{
    // PoCの1画面。タブから遅延生成される
    // One PoC screen, created lazily from its tab
    public interface IUitkPocDemo
    {
        string Title { get; }
        VisualElement CreateView();
    }
}
