using System;

namespace Game.Block.Interface
{
    public class BlockException
    {
        private const string IsDestroyed = "This component is already destroyed";
        public static readonly InvalidOperationException IsDestroyedException = new(IsDestroyed);
    }
}