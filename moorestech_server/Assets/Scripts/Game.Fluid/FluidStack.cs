using System;

namespace Game.Fluid
{
    public struct FluidStack
    {
        public readonly Guid FluidId;
        public readonly float Amount;
        public FluidMoveDirection FluidMoveDirection;
        
        public FluidStack(Guid fluidId, float amount, FluidMoveDirection fluidMoveDirection)
        {
            FluidId = fluidId;
            Amount = amount;
            FluidMoveDirection = fluidMoveDirection;
        }
    }
    
    /// <summary>
    ///     今後統一された向きを表すenumが現れたらそちらに統一してこれは消す
    /// </summary>
    public enum FluidMoveDirection
    {
        Up,
        Down,
        Right,
        Left,
        Forward,
        Back,
    }
}