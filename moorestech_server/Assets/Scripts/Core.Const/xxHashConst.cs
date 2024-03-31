namespace Core.Const
{
    /// <summary>
    ///     鉱石生成に使うxxHashの定数
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class xxHashConst
    {
        /// <summary>
        ///     取りえあずのシード値
        ///     TODO 将来的には何か一つのシード値から取得するようにするが、今はこのままで
        /// </summary>
        public const ulong DefaultSeed = 1235131;

        public const int DefaultSize = 64;
    }
}