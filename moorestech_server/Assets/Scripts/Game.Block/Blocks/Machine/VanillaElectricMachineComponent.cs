using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Item.Interface;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Context;
using Game.EnergySystem;

namespace Game.Block.Blocks.Machine
{
    /// <summary>
    ///     機械を表すクラス
    /// </summary>
    public class VanillaElectricMachineComponent : IElectricConsumer
    {
        public int EntityId { get; }
        
        private readonly VanillaMachineProcessorComponent _vanillaMachineProcessorComponent;
        
        public VanillaElectricMachineComponent(int entityId, VanillaMachineProcessorComponent vanillaMachineProcessorComponent)
        {
            _vanillaMachineProcessorComponent = vanillaMachineProcessorComponent;
            EntityId = entityId;
        }
        
        public bool IsDestroy { get; private set; }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        #region IBlockElectric implementation
        
        public int RequestEnergy => _vanillaMachineProcessorComponent.RequestPower;
        
        public void SupplyEnergy(int power)
        {
            if (IsDestroy) throw BlockException.IsDestroyedException;
            
            _vanillaMachineProcessorComponent.SupplyPower(power);
        }
        
        #endregion
    }
}