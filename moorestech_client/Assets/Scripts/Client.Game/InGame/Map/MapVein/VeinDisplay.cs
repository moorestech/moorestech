using System;

namespace Client.Game.InGame.Map.MapVein
{
    /// <summary>
    ///     鉱脈範囲表示が取りうる3つの状態。非表示・種別まとめ表示・単一鉱脈だけの表示を1つの値で表す
    ///     The three states of the vein range view — hidden, a whole kind, or a single vein — carried as one value
    ///     優先規則を値の作り方で表現し、実装側に「どちらが勝つか」を持たせない
    ///     The precedence rule lives in how the value is built, so no implementation decides which side wins
    /// </summary>
    public readonly struct VeinDisplay
    {
        // 種別表示のときだけ値を持つ。単一表示・非表示ではnull
        // Carries a value only in kind mode; null for single and hidden
        public readonly MapVeinKind? Kind;

        // 単一表示のときだけ値を持つ。指定中は種別を問わずその鉱脈だけを描く
        // Carries a value only in single mode; while set, only that vein is drawn regardless of kind
        public readonly Guid? SingleVeinGuid;

        private VeinDisplay(MapVeinKind? kind, Guid? singleVeinGuid)
        {
            Kind = kind;
            SingleVeinGuid = singleVeinGuid;
        }

        public static VeinDisplay Hidden => new(null, null);

        public static VeinDisplay OfKind(MapVeinKind kind)
        {
            return new VeinDisplay(kind, null);
        }

        public static VeinDisplay Single(Guid veinGuid)
        {
            return new VeinDisplay(null, veinGuid);
        }
    }
}
