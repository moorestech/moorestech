using Core.Master;

namespace Game.Fluid
{
    /// <summary>
    ///     ゲーム内における液体の容器をモデル化したクラス。タンク、機械の内部バッファなど、液体を保持できる境界要素を表現する。
    ///     パイプはFluidSimNode（速度モデル）で表現されるため、このクラスはパイプには使われない。
    ///     旧実装にあった同tick逆流防止のソース記録は、速度モデルへの刷新で不要になったため存在しない。
    ///
    ///     A class modeling an in-game liquid container: tanks, machine buffers and other boundary elements that hold fluids.
    ///     Pipes are represented by FluidSimNode (the velocity model), so this class is no longer used for pipes.
    ///     The per-tick source tracking that prevented same-tick backflow in the old implementation is gone; the velocity model made it unnecessary.
    /// </summary>
    public class FluidContainer
    {
        /// <summary>
        ///     空のコンテナを表すシングルトンインスタンス（Null Objectパターン）。このインスタンスへのAddLiquidは常に液体を受け取らずに返す。
        ///     A singleton instance representing an empty container (Null Object Pattern). AddLiquid to this instance always returns the liquid without accepting it.
        /// </summary>
        public static readonly FluidContainer Empty = new();

        /// <summary>
        ///     このコンテナが保持できる液体の最大容量
        ///     The maximum capacity of liquid this container can hold.
        /// </summary>
        public readonly double Capacity;

        public double Amount;
        public FluidId FluidId;

        public readonly bool IsEmpty;

        /// <param name="capacity">液体の許容量</param>
        public FluidContainer(double capacity)
        {
            Capacity = capacity;
            FluidId = FluidMaster.EmptyFluidId;
        }

        /// <summary>
        ///     Create empty container.
        /// </summary>
        private FluidContainer()
        {
            FluidId = FluidMaster.EmptyFluidId;
            Capacity = 0;
            IsEmpty = true;
        }

        // 液体を追加し、受け入れられなかった残量を返す。異種流体は拒否し、空コンテナは流入流体のIDを引き継ぐ
        // Add liquid and return the unaccepted remainder; mismatched fluids are rejected and an empty container adopts the incoming id
        public FluidStack AddLiquid(FluidStack fluidStack)
        {
            if (IsEmpty || fluidStack.Amount <= 0) return fluidStack;

            if (FluidId == FluidMaster.EmptyFluidId) FluidId = fluidStack.FluidId;
            if (fluidStack.FluidId != FluidId) return fluidStack;

            var freeCapacity = Capacity - Amount;
            if (freeCapacity < fluidStack.Amount)
            {
                Amount = Capacity;
                return new FluidStack(fluidStack.Amount - freeCapacity, fluidStack.FluidId);
            }

            Amount += fluidStack.Amount;
            return new FluidStack(0, fluidStack.FluidId);
        }
    }
}
