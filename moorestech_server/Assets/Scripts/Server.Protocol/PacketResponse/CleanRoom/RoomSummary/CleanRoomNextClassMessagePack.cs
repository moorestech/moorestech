using MessagePack;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     次のクラスまでの台数。組み立ては種別ごとの static factory、台数の読み出しは TryGetUnitsToNextClass に閉じる
    ///     Units to the next class; built only through per-kind static factories and read only through TryGetUnitsToNextClass
    /// </summary>
    [MessagePackObject]
    public class CleanRoomNextClassMessagePack
    {
        [Key(0)] public CleanRoomNextClassResponseKind Kind { get; }

        // NextClassName は UnitsToNextClass・Unreachable、Units は UnitsToNextClass のときだけ書かれる
        // NextClassName is written only for UnitsToNextClass and Unreachable, Units only for UnitsToNextClass
        [Key(1)] public string NextClassName { get; }
        [Key(2)] public int Units { get; }

        [SerializationConstructor]
        public CleanRoomNextClassMessagePack(CleanRoomNextClassResponseKind kind, string nextClassName, int units)
        {
            Kind = kind;
            NextClassName = nextClassName;
            Units = units;
        }

        public static CleanRoomNextClassMessagePack CreateAtBestClass()
        {
            return new CleanRoomNextClassMessagePack(CleanRoomNextClassResponseKind.AtBestClass, null, 0);
        }

        public static CleanRoomNextClassMessagePack CreateUnitsToNextClass(string nextClassName, int units)
        {
            return new CleanRoomNextClassMessagePack(CleanRoomNextClassResponseKind.UnitsToNextClass, nextClassName, units);
        }

        public static CleanRoomNextClassMessagePack CreateUnreachable(string nextClassName)
        {
            return new CleanRoomNextClassMessagePack(CleanRoomNextClassResponseKind.Unreachable, nextClassName, 0);
        }

        public static CleanRoomNextClassMessagePack CreateNotCounted()
        {
            return new CleanRoomNextClassMessagePack(CleanRoomNextClassResponseKind.NotCounted, null, 0);
        }

        // 「（クラスBまであと5台）」を出せるときだけ true
        // True only when "(5 more to class B)" can be shown
        public bool TryGetUnitsToNextClass(out string nextClassName, out int units)
        {
            var hasUnits = Kind == CleanRoomNextClassResponseKind.UnitsToNextClass;
            nextClassName = hasUnits ? NextClassName : default;
            units = hasUnits ? Units : default;
            return hasUnits;
        }

        // 外から届いた値が読めるか。未知の種別と、種別に要る値の欠けを弾く
        // Whether a value from outside is readable, rejecting unknown kinds and values a kind needs but lacks
        public bool IsReadable()
        {
            switch (Kind)
            {
                case CleanRoomNextClassResponseKind.AtBestClass:
                case CleanRoomNextClassResponseKind.NotCounted:
                    return true;
                case CleanRoomNextClassResponseKind.UnitsToNextClass:
                    return NextClassName != null && 0 < Units;
                case CleanRoomNextClassResponseKind.Unreachable:
                    return NextClassName != null;
                default:
                    return false;
            }
        }
    }

    public enum CleanRoomNextClassResponseKind
    {
        AtBestClass,
        UnitsToNextClass,
        Unreachable,
        NotCounted,
    }
}
