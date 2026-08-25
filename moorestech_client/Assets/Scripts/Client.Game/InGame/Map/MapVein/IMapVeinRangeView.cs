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

        // カメラ追従の距離カリング用。表示中のフレーム駆動
        // Per-frame tick for camera-following distance culling while visible
        void ManualUpdate();
    }
}
