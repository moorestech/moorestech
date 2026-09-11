using Core.Master;

namespace Game.Construction
{
    /// <summary>
    ///     既設ブロックを別ブロックへ差し替える設置。どのブロックを張替え対象と見なすかは実装側の業務規則
    ///     Replace placement that swaps an existing block for another; which blocks count as replace targets is the implementation's business rule
    ///     共通の設置プロトコルは対象の種類を知らず、CanReplaceの答えとReplaceの結果だけを受け取る
    ///     The shared placement protocol never knows the target kind and only receives CanReplace's answer and Replace's result
    /// </summary>
    public interface IReplacePlacementService
    {
        bool CanReplace(BlockId existingBlockId, BlockId newBlockId);

        ReplacePlacementResult Replace(ReplacePlacementRequest request);
    }
}
