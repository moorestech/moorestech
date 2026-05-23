using UnitGenerator;

namespace Game.PlayerRiding.Interface
{
    /// <summary>
    /// 乗り物タイプを表す識別子。拡張性のためにstringベース
    /// A string-based identifier representing the type of ridable, designed for extensibility.
    /// </summary>
    [UnitOf(typeof(string), UnitGenerateOptions.MessagePackFormatter )]
    public readonly partial struct RidableType
    {
        // ビルトインの乗り物種別。
        // Built-in ridable kinds.
        public static readonly RidableType TrainCar = new("TrainCar");
    }
}
