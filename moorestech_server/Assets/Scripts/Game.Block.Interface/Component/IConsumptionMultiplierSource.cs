namespace Game.Block.Interface.Component
{
    /// <summary>
    ///     要求トルク・消費電力に乗じる消費倍率の供給源
    ///     Source of the multiplier applied to torque demand and power consumption
    /// </summary>
    public interface IConsumptionMultiplierSource
    {
        float ConsumptionMultiplier { get; }
    }
}
