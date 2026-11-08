namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route
{
    /// <summary>
    ///     経路の1セルに置くベルトの種類。値は状態の圧縮添字に使うので0..2で固定
    ///     The kind of belt placed on one route cell; values are fixed to 0..2 because they index packed states
    /// </summary>
    public enum BeltRouteCellKind
    {
        Straight = 0,
        Up = 1,
        Down = 2,
    }

    /// <summary>
    ///     セル種別ごとの探索コストと、サーバー接続規則に由来する高さの出入り
    ///     Search cost per cell kind, plus the entry/exit heights the server's connection rule implies
    /// </summary>
    public static class BeltRouteCellKindExtension
    {
        public const int StraightCost = 5;
        public const int SlopeCost = 7;
        public const int KindCount = 3;

        public static int Cost(this BeltRouteCellKind kind)
        {
            return kind == BeltRouteCellKind.Straight ? StraightCost : SlopeCost;
        }

        public static bool IsSlope(this BeltRouteCellKind kind)
        {
            return kind != BeltRouteCellKind.Straight;
        }

        // 上りはセル上端、下りはセル下端から出る。入口はその逆
        // An up slope exits at its top and a down slope at its bottom; entries are the reverse
        public static int ExitHeightOffset(this BeltRouteCellKind kind)
        {
            return kind == BeltRouteCellKind.Up ? 1 : 0;
        }

        public static int EntryHeightOffset(this BeltRouteCellKind kind)
        {
            return kind == BeltRouteCellKind.Down ? 1 : 0;
        }

        // 山（上り→下り）と谷（下り→上り）は搬送面が切れるため隣接させない
        // A peak (up then down) or a valley (down then up) breaks the transport surface, so they never touch
        public static bool CanFollow(this BeltRouteCellKind next, BeltRouteCellKind previous)
        {
            if (previous == BeltRouteCellKind.Up && next == BeltRouteCellKind.Down) return false;
            if (previous == BeltRouteCellKind.Down && next == BeltRouteCellKind.Up) return false;
            return true;
        }
    }
}
