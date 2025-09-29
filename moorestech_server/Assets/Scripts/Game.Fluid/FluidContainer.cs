using System;
using System.Collections.Generic;
using Core.Master;

namespace Game.Fluid
{
    /// <summary>
    ///     ゲーム内における液体の容器をモデル化したクラス。
    ///     パイプ、タンク、機械の内部バッファなど、液体を保持できるあらゆる要素を表現する汎用的なデータコンテナ。
    /// </summary>
    /// <remarks>
    ///     速度の違うfluidStackはサポート外
    /// </remarks>
    public class FluidContainer
    {
        /// <summary>
        ///     空のコンテナを表すシングルトンインスタンス（Null Objectパターン）。
        ///     このインスタンスへのAddLiquidは常に液体を受け取らずに返す。
        /// </summary>
        public static readonly FluidContainer Empty = new();

        /// <summary>
        ///     このコンテナが保持できる液体の最大容量
        /// </summary>
        public readonly double Capacity;
        
        public double Amount;
        public FluidId FluidId;
        
        public readonly bool IsEmpty;

        /// <summary>
        ///     同一更新サイクル内での液体の逆流を防ぐための一時的な送信元記録。
        ///     パイプネットワークにおいて、液体がA→Bに流れた場合、同じ更新サイクル内でB→Aへの逆流を防ぐことで、
        ///     液体が無限ループに陥らず、一方向に正しく伝播するようになる。
        ///     このHashSetには、現在の更新サイクル内でこのコンテナに液体を送ってきたすべてのコンテナが記録される。
        ///     各更新サイクルの最後（例：FluidPipeComponent.Update）でクリアされる必要がある。
        /// </summary>
        public readonly HashSet<FluidContainer> PreviousSourceFluidContainers = new();
        
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
        
        public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source)
        {
            // パイプ内の液体IDがセットされていない場合は入ってきた液体のidをセットする
            if (FluidId == FluidMaster.EmptyFluidId)
                FluidId = fluidStack.FluidId;

            if (IsEmpty || fluidStack.FluidId != FluidId)
            {
                return fluidStack;
            }
            
            // Prevent immediate backflow within the same update cycle
            if (source != Empty && PreviousSourceFluidContainers.Contains(source))
            {
                return fluidStack;
            }

            if (Capacity - Amount < fluidStack.Amount)
            {
                var addingAmount = Capacity - Amount;
                Amount += addingAmount;
                // FluidContainer.Emptyは特別扱い（シングルトンなので追加しない）
                if (source != Empty)
                {
                    PreviousSourceFluidContainers.Add(source);
                }
                var guid = MasterHolder.FluidMaster.GetFluidMaster(FluidId).FluidGuid;
                return new FluidStack(fluidStack.Amount - addingAmount, fluidStack.FluidId);
            }

            Amount += fluidStack.Amount;
            // FluidContainer.Emptyは特別扱い（シングルトンなので追加しない）
            if (source != Empty)
            {
                PreviousSourceFluidContainers.Add(source);
            }
            return new FluidStack(0, fluidStack.FluidId);
        }
    }
}