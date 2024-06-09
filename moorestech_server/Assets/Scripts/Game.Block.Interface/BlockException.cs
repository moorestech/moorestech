using System;
using Game.Block.Interface.Component;

namespace Game.Block.Interface
{
    public class BlockException
    {
        private const string IsDestroyed = "This component is already destroyed";
        public static readonly InvalidOperationException IsDestroyedException = new(IsDestroyed);
        
        public static void CheckDestroy(IBlockComponent blockComponent)
        {
            if (blockComponent.IsDestroy)
            {
                throw IsDestroyedException;
            }
        }
    }
}