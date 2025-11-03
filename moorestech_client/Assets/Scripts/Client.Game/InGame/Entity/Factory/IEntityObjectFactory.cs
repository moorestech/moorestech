using Client.Network.API;

namespace Client.Game.InGame.Entity.Factory
{
    /// <summary>
    /// 特定のエンティティタイプに対応するIEntityObjectを生成するファクトリーのインターフェース
    /// Interface for factory that creates IEntityObject corresponding to specific entity type
    /// </summary>
    public interface IEntityObjectFactory
    {
        /// <summary>
        /// このファクトリーが対応するエンティティタイプ
        /// Entity type that this factory supports
        /// </summary>
        string SupportedEntityType { get; }
        
        /// <summary>
        /// EntityResponseからIEntityObjectを生成
        /// Create IEntityObject from EntityResponse
        /// </summary>
        IEntityObject CreateEntity(EntityResponse entity);
    }
}

