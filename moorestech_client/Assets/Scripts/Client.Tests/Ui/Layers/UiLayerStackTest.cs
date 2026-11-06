using System;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Layers
{
    /// <summary>
    /// 層スタックの並び順と、空の層がクリックを素通しすることを実パネルで確かめる（L1）
    /// Verifies the layer stack order and that empty layers let clicks through on a real panel (L1)
    /// </summary>
    public class UiLayerStackTest : UiTestHost
    {
        private static readonly Vector2 PanelCenter = new(640, 360);

        [Test]
        public void LayersAreRootChildrenInEnumOrderWithBothFamiliesIgnoringPicks()
        {
            var stack = UiLayerStack.Build(rootVisualElement);
            var layers = (UiLayer[])Enum.GetValues(typeof(UiLayer));

            // 子はenum順・各層に2族
            // Children follow enum order, two families per layer
            Assert.AreEqual(layers.Length, rootVisualElement.childCount);
            for (var i = 0; i < layers.Length; i++)
            {
                var container = rootVisualElement[i];
                Assert.AreEqual($"layer-{layers[i]}", container.name, $"{layers[i]} is not at index {i}");
                Assert.AreEqual(PickingMode.Ignore, container.pickingMode, $"{layers[i]} takes picks");
                foreach (UiFamily family in Enum.GetValues(typeof(UiFamily)))
                {
                    var child = stack.GetLayer(layers[i], family);
                    Assert.AreSame(container, child.parent, $"{layers[i]} {family} is not inside its layer");
                    Assert.AreEqual(PickingMode.Ignore, child.pickingMode, $"{layers[i]} {family} takes picks");
                }
            }
            Assert.AreEqual(PickingMode.Ignore, rootVisualElement.pickingMode);
        }

        [Test]
        public void EmptyLayersPickNothing()
        {
            UiLayerStack.Build(rootVisualElement);
            simulate.FrameUpdate();

            // 層が全面を覆っていても、中身が無ければワールドへ抜ける
            // Even though layers cover the whole panel, an empty stack lets the point through to the world
            Assert.AreEqual(new Vector2(1280, 720), rootVisualElement.layout.size);
            Assert.IsNull(panel.Pick(PanelCenter));
        }

        [Test]
        public void ModalElementIsPickedAboveScreenElement()
        {
            var stack = UiLayerStack.Build(rootVisualElement);
            var screenElement = CreatePickableBox("screen-box");
            var modalElement = CreatePickableBox("modal-box");
            stack.GetLayer(UiLayer.Screen, UiFamily.Stage).Add(screenElement);
            stack.GetLayer(UiLayer.Modal, UiFamily.Viewport).Add(modalElement);
            simulate.FrameUpdate();

            // 同じ位置で重なると前面の Modal 層の要素が拾われる
            // When both overlap at the same point, the element on the front Modal layer is picked
            Assert.AreSame(modalElement, panel.Pick(PanelCenter));

            // Modal を外すと下の Screen 層の要素が拾われる
            // Removing the modal exposes the element on the Screen layer below
            modalElement.RemoveFromHierarchy();
            simulate.FrameUpdate();
            Assert.AreSame(screenElement, panel.Pick(PanelCenter));
        }

        private static VisualElement CreatePickableBox(string name)
        {
            var box = new VisualElement { name = name, pickingMode = PickingMode.Position };
            box.style.position = Position.Absolute;
            box.style.left = PanelCenter.x - 100;
            box.style.top = PanelCenter.y - 50;
            box.style.width = 200;
            box.style.height = 100;
            return box;
        }
    }
}
