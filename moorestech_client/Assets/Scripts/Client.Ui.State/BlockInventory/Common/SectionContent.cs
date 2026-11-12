using Client.Game.InGame.BlockInfoPolling;

namespace Client.Ui.State.BlockInventory.Common
{
    /// <summary>
    /// 区画の値の 3 状態: 無し・取得不可・在る
    /// Section value: absent, unavailable or present (absent is never null)
    /// </summary>
    public readonly struct SectionContent<T>
    {
        public readonly T Value;
        private readonly SectionContentKind _kind;
        private readonly BlockInfoFetchState _fetchState;

        public bool IsPresent => _kind == SectionContentKind.Present;

        // 欄ごと隠すか（取得不可の行も出さない）
        // Whether the whole entry is hidden
        public bool IsAbsent => _kind == SectionContentKind.Absent;

        private SectionContent(SectionContentKind kind, T value, BlockInfoFetchState fetchState)
        {
            _kind = kind;
            Value = value;
            _fetchState = fetchState;
        }

        public static SectionContent<T> Absent => new(SectionContentKind.Absent, default, default);
        public static SectionContent<T> Present(T value) => new(SectionContentKind.Present, value, default);
        public static SectionContent<T> Unavailable(BlockInfoFetchState fetchState) => new(SectionContentKind.Unavailable, default, fetchState);

        // 照会が取れていないなら取得状態を返す（「取得できません」の行の文を選ぶため）
        // Returns the fetch state when the query is unavailable (to pick the unavailable line's text)
        public bool TryGetUnavailable(out BlockInfoFetchState fetchState)
        {
            fetchState = _fetchState;
            return _kind == SectionContentKind.Unavailable;
        }
    }

    internal enum SectionContentKind
    {
        Absent,
        Unavailable,
        Present,
    }
}
