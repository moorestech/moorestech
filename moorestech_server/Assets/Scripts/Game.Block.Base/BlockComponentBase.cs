using System;

namespace Game.Block.Base
{
    public abstract class BlockComponentBase : IDisposable
    {
        public bool IsDestroy { get; private set; }

        public void Dispose()
        {
            IsDestroy = true;
        }
    }
}