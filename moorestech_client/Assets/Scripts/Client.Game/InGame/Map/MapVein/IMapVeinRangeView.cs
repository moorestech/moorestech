namespace Client.Game.InGame.Map.MapVein
{
    /// <summary>
    ///     鉱脈範囲表示の窓口。設置側は表示状態の変化と毎フレームの駆動だけを渡す
    ///     Entry point of the vein range view; the placement side pushes visibility changes and per-frame ticks only
    /// </summary>
    public interface IMapVeinRangeView
    {
        // 表示状態の変化時にだけ呼ぶ（OnEnter/OnExit）
        // Called only when visibility changes (OnEnter/OnExit)
        void Show(bool isVisible);

        // カメラ追従の距離カリング用。表示中のフレーム駆動
        // Per-frame tick for camera-following distance culling while visible
        void ManualUpdate();
    }
}
