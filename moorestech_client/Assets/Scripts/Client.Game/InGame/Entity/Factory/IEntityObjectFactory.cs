using Client.Network.API;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.Entity.Factory
{
    /// <summary>
    /// 特定のエンティティタイプに対応するIEntityObjectを生成するファクトリーのインターフェース
    /// Interface for factory that creates IEntityObject corresponding to specific entity type
    /// </summary>
    public interface IEntityObjectFactory
    {
        UniTask<IEntityObject> CreateEntity(Transform parent, EntityResponse entity);
    }
}

