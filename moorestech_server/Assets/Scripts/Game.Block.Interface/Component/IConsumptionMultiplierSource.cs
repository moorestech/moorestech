namespace Game.Block.Interface.Component
{
    /// <summary>
    ///     歯車の要求トルクや消費電力に乗じる消費倍率の供給源（機械のモジュール効果など）
    ///     Source of the consumption multiplier applied to gear torque demand and power consumption (e.g. machine module effects)
    /// </summary>
    public interface IConsumptionMultiplierSource
    {
        float ConsumptionMultiplier { get; }
    }
}
