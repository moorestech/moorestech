using Core.Master;

namespace Game.Fluid.Simulation
{
    /// <summary>
    ///     パイプノードから非パイプ流体インベントリ（機械・発電機・列車プラットフォーム等）への一方向流出ポート。
    ///     逆方向（境界→パイプ）は境界側コンポーネントが自身のUpdateでpushするため、このポートは吸い戻しを行わない。
    ///     量は FluidUnits の整数単位。機械側の double との変換は実装(サーバーのポート)が行う。
    ///
    ///     One-way outflow port from a pipe node to a non-pipe fluid inventory (machine, generator, train platform, ...).
    ///     The reverse direction (boundary→pipe) is pushed by the boundary component itself, so this port never sucks fluid back.
    ///     Amounts are FluidUnits integers; the server-side implementation converts to and from the machine's double.
    /// </summary>
    public interface IFluidBoundaryPort
    {
        // 繋がるパイプのノード番号(網の配列の添字)
        // Index of the connected pipe's node in the network arrays
        int NodeIndex { get; }

        // このポートを1tickに通過できる最大量(単位)
        // Maximum amount (units) this port can pass per tick
        long FlowCapacityPerTick { get; }

        // ポート速度（単位/tick、常に0以上）。Stepperが更新する
        // Port velocity (units per tick, always >= 0), updated by the stepper
        long Velocity { get; set; }

        // 流体を境界へ届け、受け取られた量(単位、0〜attemptedUnits)を返す
        // Deliver fluid to the boundary and return the accepted amount (units, 0..attemptedUnits)
        long Deliver(long attemptedUnits, FluidId fluidId);
    }
}
