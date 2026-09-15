using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Pruning
{
    /// <summary>1節の除去器が取り除いた実体。種別ごとに別の配列へ積み、件数の意味を混ぜない</summary>
    /// <summary>What one section pruner removed, kept in a separate array per kind so the counts never mix meanings</summary>
    public sealed class MissingMasterSectionPruneResult
    {
        public JArray RemovedBlocks { get; } = new();

        // 在庫・機械・貨車の中身と燃焼中の燃料。通知の「取り除いたアイテム件数」はこの件数
        // Inventory, machine and train car contents plus burning fuel; the notice's "items removed" count is this array's size
        public JArray EmptiedItemStacks { get; } = new();

        // 接続コスト素材は在庫ではないので別に積む。混ぜると後日の返金が接続コストまで払い戻す
        // Connection cost materials are not inventory and stay apart; mixing them in would let a later refund pay back connection costs
        public JArray NeutralizedConnectionMaterials { get; } = new();

        public JArray RemovedResearchGuids { get; } = new();

        // 解放状態と列車はプレイヤーへの通知件数に入れない。ファイルにだけ残す
        // Unlock states and trains do not count toward the player notice; they are only kept in the file
        public JArray RemovedUnlockStates { get; } = new();
        public JArray RemovedTrainUnits { get; } = new();
    }
}
