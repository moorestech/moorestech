using Core.Update;

namespace Mod.Base
{
    public abstract class MoorestechServerModEntryPoint : IUpdatable
    {
        public MoorestechServerModEntryPoint()
        {
            GameUpdater.RegisterUpdater(this);
        }


        ///     。

        public void Update()
        {
        }


        ///     Mod

        /// <param name="serverModEntryInterface">DI。。</param>
        public abstract void OnLoad(ServerModEntryInterface serverModEntryInterface);
    }
}