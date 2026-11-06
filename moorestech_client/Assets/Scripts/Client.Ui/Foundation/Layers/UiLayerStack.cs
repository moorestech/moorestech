using System;
using System.Collections.Generic;
using Client.Ui.Foundation.Stage;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Layers
{
    /// <summary>
    /// ルート直下に層コンテナを enum 順で並べ、各層に舞台と実画面の 2 族の子を置いて、層と族で引けるようにする
    /// Lays out one container per layer under the root in enum order, gives each the stage and viewport family children, and looks them up by layer and family
    /// </summary>
    public sealed class UiLayerStack
    {
        private const string LayerUssClassName = "game-ui-layer";

        private readonly Dictionary<UiLayer, UiStageFrame> _frames;

        private UiLayerStack(Dictionary<UiLayer, UiStageFrame> frames)
        {
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

            return new UiLayerStack(frames);

            #region Internal

            VisualElement CreateLayerContainer(UiLayer layer)
            {
                var container = new VisualElement { name = $"layer-{layer}", pickingMode = PickingMode.Ignore };
                // 全面に広げて重ねる配置は UiStageFrame.uss の .game-ui-layer が持つ
                // The full-root overlapping placement lives in .game-ui-layer of UiStageFrame.uss
                container.AddToClassList(LayerUssClassName);
                return container;
            }

            #endregion
        }

        public VisualElement GetLayer(UiLayer layer, UiFamily family)
        {
            return _frames[layer].Get(family);
        }
    }
}
