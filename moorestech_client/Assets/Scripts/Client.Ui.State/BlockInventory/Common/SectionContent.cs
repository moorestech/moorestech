namespace Client.Ui.State.BlockInventory.Common
{
    /// <summary>
    /// 区画の値の有無。無しを null で表さず、出し入れはこの真偽で決める
    /// Whether a section has a value; absence is never null, and showing follows this flag
    /// </summary>
    public readonly struct SectionContent<T>
    {
        public readonly bool IsPresent;
        public readonly T Value;

        private SectionContent(bool isPresent, T value)
        {
            IsPresent = isPresent;
            Value = value;
        }

        public static SectionContent<T> Absent => new(false, default);
        public static SectionContent<T> Present(T value) => new(true, value);
    }
}
