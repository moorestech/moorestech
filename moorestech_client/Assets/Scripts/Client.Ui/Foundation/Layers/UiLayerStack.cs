using System;
using System.Collections.Generic;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Stage;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Layers
{
    /// <summary>
    /// 層コンテナをenum順に並べ層と族で引く
    /// Layer containers in enum order, looked up by layer and family
    /// </summary>
    public sealed class UiLayerStack
    {
        private const string LayerUssClassName = "game-ui-layer";

        private readonly VisualElement _root;
        private readonly Dictionary<UiLayer, UiStageFrame> _frames;

        private UiLayerStack(VisualElement root, Dictionary<UiLayer, UiStageFrame> frames)
        {
            _root = root;
            _frames = frames;
        }

        public static UiLayerStack Build(VisualElement root)
        {
            // ルートも層もクリックを受けず、空の場所はワールドへ抜ける
            // Neither the root nor the layers take clicks, so empty spots fall through to the world
            root.pickingMode = PickingMode.Ignore;
            var frames = new Dictionary<UiLayer, UiStageFrame>();

            // 後に Add した子ほど前面に描かれるので enum 順に足す
            // Later children draw in front, so add them in enum order
            foreach (UiLayer layer in Enum.GetValues(typeof(UiLayer)))
            {
                var container = CreateLayerContainer(layer);
                root.Add(container);
                frames.Add(layer, UiStageFrame.Build(container));
            }

            return new UiLayerStack(root, frames);

            #region Internal

            VisualElement CreateLayerContainer(UiLayer layer)
            {
                var container = new VisualElement { name = $"layer-{layer}", pickingMode = PickingMode.Ignore };
                // 配置はUSSが持つ
                // Placement lives in USS
                container.AddToClassList(LayerUssClassName);
                return container;
            }

            #endregion
        }

        public VisualElement GetLayer(UiLayer layer, UiFamily family)
        {
            return _frames[layer].Get(family);
        }

        // 全部隠し（HideAll）の中身。全層をルート 1 つの display でまとめて出し入れする。要素は階層に残すので購読・入力中の値・動きは保たれる（A-80）
        // Body of HideAll: shows or hides every layer at once through the root's display; elements stay in the hierarchy, so subscriptions, typed values and motion survive (A-80)
        public void SetAllLayersVisible(bool visible)
        {
            // 隠れた要素のフォーカスは UITK 自身が外すので、ここでは Blur しない（Web の focus fixup と同じ。A-275）
            // UITK itself blurs the focus of a hidden element, so nothing blurs here (same as the Web focus fixup; A-275)
            UiDisplay.SetShown(_root, visible);
        }
    }
}
