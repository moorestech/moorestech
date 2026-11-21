using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Client.Ui.Foundation.Composite
{
    /// <summary>
    /// ポスト処理直後の世界色を同じカメラの合成パスへ渡すフレームデータ
    /// Frame data handing the post-processed world colour to the composite pass of the same camera
    /// </summary>
    internal sealed class UiSrgbWorldColorData : ContextItem
    {
        public TextureHandle WorldColor { get; private set; }

        public void SetWorldColor(TextureHandle worldColor)
        {
            WorldColor = worldColor;
        }

        public override void Reset()
        {
            WorldColor = TextureHandle.nullHandle;
        }
    }
}
