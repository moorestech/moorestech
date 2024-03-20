using System;

namespace Game.Block.Base
{
    public interface IBlockComponent
    {
        public bool IsDestroy { get; }

        public void Destroy();
    }
}