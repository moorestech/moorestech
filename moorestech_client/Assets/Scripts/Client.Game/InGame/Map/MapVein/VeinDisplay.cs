using System;

namespace Client.Game.InGame.Map.MapVein
{
    /// <summary>
    ///     鉱脈範囲表示が取りうる3つの状態。非表示・kindまとめ表示・種別GUID指定表示を1つの値で表す
    ///     The three states of the vein range view — hidden, a whole kind, or one vein type — carried as one value
    ///     優先規則を値の作り方で表現し、実装側に「どちらが勝つか」を持たせない
    ///     The precedence rule lives in how the value is built, so no implementation decides which side wins
    /// </summary>
    public readonly struct VeinDisplay
    {
        // kind表示のときだけ値を持つ。種別GUID表示・非表示ではnull
        // Carries a value only in kind mode; null for vein-type and hidden
        public readonly MapVeinKind? Kind;

        // 種別GUID表示のときだけ値を持つ。指定中はkindを問わずその種別の鉱脈すべてを描く
        // Carries a value only in vein-type mode; while set, every vein of that type is drawn regardless of kind
        public readonly Guid? VeinTypeGuid;

        private VeinDisplay(MapVeinKind? kind, Guid? veinTypeGuid)
        {
            Kind = kind;
            VeinTypeGuid = veinTypeGuid;
        }

        public static VeinDisplay Hidden => new(null, null);

        public static VeinDisplay OfKind(MapVeinKind kind)
        {
            return new VeinDisplay(kind, null);
        }

        public static VeinDisplay OfVeinType(Guid veinTypeGuid)
        {
            return new VeinDisplay(null, veinTypeGuid);
        }
    }
}
