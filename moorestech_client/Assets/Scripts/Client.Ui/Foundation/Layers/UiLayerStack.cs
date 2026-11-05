using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Layers
{
    /// <summary>
    /// ルート直下に層コンテナを enum 順で並べ、層ごとに引けるようにする
    /// Lays out one container per layer under the root in enum order and looks them up by layer
    /// </summary>
    public sealed class UiLayerStack
    {
        private const string LayerUssClassName = "game-ui-layer";

        private readonly Dictionary<UiLayer, VisualElement> _layers;

        private UiLayerStack(Dictionary<UiLayer, VisualElement> layers)
        {
            _layers = layers;
        }

        public static UiLayerStack Build(VisualElement root)
        {
            // ルートも層もクリックを受けず、空の場所はワールドへ抜ける
            // Neither the root nor the layers take clicks, so empty spots fall through to the world
            root.pickingMode = PickingMode.Ignore;
            var layers = new Dictionary<UiLayer, VisualElement>();

            // 後に Add した子ほど前面に描かれるので enum 順に足す
            // Later children draw in front, so add them in enum order
            foreach (UiLayer layer in Enum.GetValues(typeof(UiLayer)))
            {
                var container = CreateLayerContainer(layer);
                root.Add(container);
                layers.Add(layer, container);
            }

            return new UiLayerStack(layers);
        }

        public VisualElement GetLayer(UiLayer layer)
        {
            return _layers[layer];
        }

        private static VisualElement CreateLayerContainer(UiLayer layer)
        {
            var container = new VisualElement { name = $"layer-{layer}", pickingMode = PickingMode.Ignore };
            container.AddToClassList(LayerUssClassName);

            // 全面に広げて層同士を重ねる
            // Stretch to the full root so the layers overlap
            container.style.position = Position.Absolute;
            container.style.left = 0;
            container.style.top = 0;
            container.style.right = 0;
            container.style.bottom = 0;
            return container;
        }
    }
}
