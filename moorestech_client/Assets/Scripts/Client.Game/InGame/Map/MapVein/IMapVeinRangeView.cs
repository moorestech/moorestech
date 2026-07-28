namespace Client.Game.InGame.Map.MapVein
{
    /// <summary>
    ///     鉱脈範囲表示の窓口。設置側は「プレビュー中か」をプッシュするだけで、veinの解決も描画も知らない
    ///     Entry point of the vein range view; the placement side only pushes "is previewing" and knows nothing of veins or rendering
    /// </summary>
    public interface IMapVeinRangeView
    {
        void ManualUpdate(bool isPlacementPreviewing);
    }
}
