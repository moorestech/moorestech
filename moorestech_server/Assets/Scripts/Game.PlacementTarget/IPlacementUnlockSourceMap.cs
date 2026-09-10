using System;

namespace Game.PlacementTarget
{
    /// <summary>
    ///     設置対象Guidを「解放状態を決めるGuid」へ寄せる写像
    ///     Maps a placement target guid to the guid whose unlock state governs it
    ///     寄せ方はドメイン側の業務規則なので、カタログは規則を知らず結果だけ受け取る
    ///     The rule is domain business logic, so the catalog never knows it and only receives the result
    /// </summary>
    public interface IPlacementUnlockSourceMap
    {
        Guid ResolveUnlockSourceId(Guid targetId);
    }
}
