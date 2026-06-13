using Core.Master;
using Game.Block.Interface.Component;

namespace Game.CleanRoom.Machine
{
    // 部屋の状態（Status＋現在の閾値行）から、機械へプッシュする効果値を算出する純関数。
    // ヒステリシス（閾値行の維持）はデータストアの tick が ThresholdIndex を更新する側で担う。
    // Pure mapping from room state (Status + threshold row) to the pushed effect.
    public static class CleanRoomEffectResolver
    {
        public static CleanRoomEffect Resolve(CleanRoom room)
        {
            // Invalid は稼働不可。Valid/Degraded（猶予中）は稼働可（設計書§8）
            // Invalid -> cannot operate; Valid/Degraded (grace) -> can operate (spec §8)
            var inValidRoom = room.Status != CleanRoomRoomStatus.Invalid;

            // Out は行として持たない（フェーズ2契約: ThresholdIndex=行数）。行参照前に分岐し範囲外アクセスを防ぐ
            // Out is not a master row (phase-2 contract: ThresholdIndex == row count); branch before indexing
            var rows = MasterHolder.CleanRoomThresholdMaster.Rows;
            if (room.ThresholdIndex >= rows.Count)
                return new CleanRoomEffect(inValidRoom, 0, 0.0);

            // 閾値行（cleanRoomThresholds マスタ）から MaxGrade / DownBinRate を引く
            // Resolve MaxGrade / DownBinRate from the thresholds master row
            var row = rows[room.ThresholdIndex];
            return new CleanRoomEffect(inValidRoom, row.MaxGrade, row.DownBinRate);
        }
    }
}
