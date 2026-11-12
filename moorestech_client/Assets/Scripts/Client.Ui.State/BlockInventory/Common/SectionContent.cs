using Client.Game.InGame.BlockInfoPolling;

namespace Client.Ui.State.BlockInventory.Common
{
    /// <summary>
    /// 区画（文脈の 1 欄を含む）の値の 3 状態: 無し・照会が取れていない（取得状態つき）・在る。無しは null にしない
    /// Three states of a section's value (context entries included): absent, unavailable (with the fetch state) or present; absence is never null
    /// </summary>
    public readonly struct SectionContent<T>
    {
        public readonly T Value;
        private readonly SectionContentKind _kind;
        private readonly BlockInfoFetchState _fetchState;

        public bool IsPresent => _kind == SectionContentKind.Present;

        // 欄ごと出さないか（「取得できません」の行も出さない）
        // Whether the whole entry is hidden (no unavailable line either)
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
