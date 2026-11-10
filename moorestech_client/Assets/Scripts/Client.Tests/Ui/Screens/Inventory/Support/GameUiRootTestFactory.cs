using System.Reflection;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory.Support
{
    /// <summary>
    /// テストのパネルに層を組んだ GameUiRoot を作る（Presenter の L1 用）
    /// Creates a GameUiRoot whose layers live on the test panel (for presenter L1)
    /// </summary>
    public static class GameUiRootTestFactory
    {
        public static GameUiRoot Create(GameObject host, VisualElement panelRoot)
        {
            var root = host.AddComponent<GameUiRoot>();
            typeof(GameUiRoot).GetField("_layers", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(root, UiLayerStack.Build(panelRoot));
            return root;
        }
    }
}
