using UnityEngine;

namespace Client.Game.InGame.Entity
{
    public interface IEntityObject
    {
        long EntityId { get; }
        
        /// <summary>
        ///     true の場合、クライアント側で「1秒間更新が無ければ削除する」挙動の対象となる。
        ///     false の場合、更新が途切れても自動削除しない（例: 差分通信の列車など）。
        /// </summary>
        bool DestroyFlagIfNoUpdate { get; }
        void Initialize(long entityId);
        void SetDirectPosition(Vector3 position);
        void SetPositionWithLerp(Vector3 position);
        void Destroy();
        void SetEntityData(byte[] entityEntityData);
    }
}