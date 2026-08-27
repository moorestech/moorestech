using System;

namespace Client.Game.InGame.Map.MapVein
{
    /// <summary>
    ///     鉱脈範囲表示の窓口。設置側は表示したい鉱脈種別の変化と毎フレームの駆動だけを渡す
    ///     Entry point of the vein range view; the placement side pushes the wanted vein kind and per-frame ticks only
    /// </summary>
    public interface IMapVeinRangeView
    {
        // 表示したい鉱脈種別の変化時にだけ呼ぶ。nullで非表示
        // Called only when the wanted vein kind changes; null hides the view
        void SetVisibleVeinKind(MapVeinKind? veinKind);

        // 強調したい鉱脈の変化時にだけ呼ぶ。指定中は種別を問わずその鉱脈だけを描く。nullで種別表示へ戻る
        // Called only when the highlighted vein changes; while set, only that vein is drawn regardless of kind; null returns to kind view
        void SetHighlightedVein(Guid? veinGuid);

        // カメラ追従の距離カリング用。表示中のフレーム駆動
        // Per-frame tick for camera-following distance culling while visible
        void ManualUpdate();
    }
}
