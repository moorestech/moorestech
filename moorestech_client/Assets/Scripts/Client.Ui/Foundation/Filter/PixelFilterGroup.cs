using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Filter
{
    /// <summary>
    /// filter を物理画素の解像度で掛ける群（Web の群の opacity を写す。設計書 A-226）
    /// A group whose filter renders at physical-pixel resolution (maps the Web group opacity; design A-226)
    /// </summary>
    /// <remarks>
    /// Unity は filter の中間 RT を filter を持つ要素の自分の座標（layout の点）で取り、倍率 1.5 以上では画面へ引き伸ばしてぼかす。
    /// 外の層を scale 1/倍率・中の層を scale 倍率にすると、外の層の座標の 1 単位が物理 1 画素になり、RT が物理画素で取られる。見た目の位置と大きさ（当たり判定も）は倍率に依らず置き場の箱のまま。
    /// filter は置き場の USS が `.置き場の状態 > .pixel-filter-group` に書く。倍率の変化はスタイルの解決（CustomStyleResolvedEvent）で拾う。
    /// Unity takes the filter's intermediate RT in the filtering element's own coordinates (layout points) and stretches it to the screen, blurring at scale 1.5 and above.
    /// Scaling the outer layer by 1/scale and the inner layer by scale makes one outer-layer unit one physical pixel, so the RT is taken at physical pixels; the visible position and size (and hit testing) stay the host box at any scale.
    /// The host USS writes the filter on `.host-state > .pixel-filter-group`; scale changes are caught on style resolution (CustomStyleResolvedEvent).
    /// </remarks>
    public sealed class PixelFilterGroup : VisualElement
    {
        public const string UssClassName = "pixel-filter-group";
        public const string ContentUssClassName = "pixel-filter-group__content";
        private const string FlowUssClassName = "pixel-filter-group--flow";

        private readonly VisualElement _content;
        private float _appliedPixelsPerPoint;

        public override VisualElement contentContainer => _content;

        public PixelFilterGroup(PixelFilterGroupFlow flow)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList(UssClassName);
            EnableInClassList(FlowUssClassName, flow == PixelFilterGroupFlow.Flow);
            _content = new VisualElement { pickingMode = PickingMode.Ignore };
            _content.AddToClassList(ContentUssClassName);
            hierarchy.Add(_content);

            // 載ったときと、倍率の変化で走るスタイルの解決のたびに倍率を読み直す
            // Re-read the scale when attached and on every style resolution (a scale change resolves styles again)
            RegisterCallback<AttachToPanelEvent>(_ => ApplyPixelsPerPoint());
            RegisterCallback<CustomStyleResolvedEvent>(_ => ApplyPixelsPerPoint());
        }

        private void ApplyPixelsPerPoint()
        {
            // silent-ok: パネルに載る前は倍率が無い / 載った時の AttachToPanelEvent でここへ戻る
            if (panel == null) return;
            var pixelsPerPoint = scaledPixelsPerPoint;
            // silent-ok: 倍率が同じなら付け直さない（付け直すとスタイルの解決が回り続ける） / 倍率が変われば次の解決で付け直す
            if (Mathf.Approximately(pixelsPerPoint, _appliedPixelsPerPoint)) return;

            // 読めない倍率は物理画素へ揃えられないので、開発者へ知らせて等倍のまま描く（ぼけるだけで位置は正しい）
            // An unreadable scale cannot align to physical pixels, so tell the developer and draw at unit scale (only blurs; positions stay right)
            if (pixelsPerPoint <= 0f)
            {
                Debug.LogError($"[PixelFilterGroup] scaledPixelsPerPoint is {pixelsPerPoint}; drawing the group at unit scale: {parent?.name}");
                pixelsPerPoint = 1f;
            }
            _appliedPixelsPerPoint = pixelsPerPoint;
            style.scale = new Scale(new Vector2(1f / pixelsPerPoint, 1f / pixelsPerPoint));
            _content.style.scale = new Scale(new Vector2(pixelsPerPoint, pixelsPerPoint));
        }
    }
}
