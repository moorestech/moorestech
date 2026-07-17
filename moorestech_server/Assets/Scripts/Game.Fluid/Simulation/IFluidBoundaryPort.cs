namespace Game.Fluid.Simulation
{
    /// <summary>
    ///     パイプノードから非パイプ流体インベントリ（機械・発電機・列車プラットフォーム等）への一方向流出ポート。
    ///     逆方向（境界→パイプ）は境界側コンポーネントが自身のUpdateでpushするため、このポートは吸い戻しを行わない。
    ///
    ///     One-way outflow port from a pipe node to a non-pipe fluid inventory (machine, generator, train platform, ...).
    ///     The reverse direction (boundary→pipe) is pushed by the boundary component itself, so this port never sucks fluid back.
    /// </summary>
    public interface IFluidBoundaryPort
    {
        FluidSimNode PipeNode { get; }

        // このポートを1tickに通過できる最大量
        // Maximum amount this port can pass per tick
        double FlowCapacityPerTick { get; }

        // ポート速度（量/tick、常に0以上）。Stepperが更新する
        // Port velocity (amount per tick, always >= 0), updated by the stepper
        double Velocity { get; set; }

        // 流体を境界へ届け、受け取られなかった残量を返す
        // Deliver fluid to the boundary and return the unaccepted remainder
        FluidStack Deliver(FluidStack fluidStack);
    }
}
