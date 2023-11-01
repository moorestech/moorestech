using Core.Update;

namespace Mod.Base
{
    public abstract class MoorestechServerModEntryPoint : IUpdatable
    {
        public MoorestechServerModEntryPoint()
        {
            GameUpdater.RegisterUpdater(this);
        }

        /// <summary>
        ///     ゲームがアップデートされるたびに呼ばれます。
        /// </summary>
        public void Update()
        {
        }

        /// <summary>
        ///     Modがロードされた時に呼ばれます
        /// </summary>
        /// <param name="serverModEntryInterface">DIコンテナを利用して、各種サービスを提供します。比較的よく使うものは直接アクセスできる様にしています。</param>
        public abstract void OnLoad(ServerModEntryInterface serverModEntryInterface);
    }
}