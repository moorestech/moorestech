using Core.Update;
using UniRx;

namespace Mod.Base
{
    public abstract class MoorestechServerModEntryPoint
    {
        public MoorestechServerModEntryPoint()
        {
            GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }

        /// <summary>
        ///     ゲームがアップデートされるたびに呼ばれます。
        /// </summary>
        private void Update()
        {
        }

        /// <summary>
        ///     Modがロードされた時に呼ばれます
        /// </summary>
        /// <param name="serverModEntryInterface">DIコンテナを利用して、各種サービスを提供します。比較的よく使うものは直接アクセスできる様にしています。</param>
        public abstract void OnLoad(ServerModEntryInterface serverModEntryInterface);
    }
}