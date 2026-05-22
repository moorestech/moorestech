using UnitGenerator;

namespace Game.PlayerRiding.Interface
{
    /// <summary>
    /// 乗り物の種類を表す判別子。string ベースの UnitOf 型で、型安全と mod による種別追加の両立を図る。
    /// mod は独自の文字列値で <c>new RidableType("MyMod.MyVehicle")</c> のように新種別を構築できる。
    /// Discriminator for a ridable kind. A string-based UnitOf type: type-safe yet extensible by mods.
    /// </summary>
    [UnitOf(typeof(string))]
    public readonly partial struct RidableType
    {
        // ビルトインの乗り物種別。
        // Built-in ridable kinds.
        public static readonly RidableType TrainCar = new("TrainCar");
    }
}
