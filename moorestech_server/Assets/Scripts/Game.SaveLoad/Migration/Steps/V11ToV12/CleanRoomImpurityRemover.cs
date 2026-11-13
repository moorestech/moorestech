using System.Collections.Generic;
using Game.SaveLoad.Migration.Steps.Shared;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.V11ToV12
{
    /// <summary>
    /// クリーンルームの部屋レコードから不純物量を消す（冪等）。版12で濃度が廃止され、クラスは換気回数だけで決まる
    /// Remove the impurity count from clean-room room records (idempotent); version 12 dropped concentration and decides the class by air change rate alone
    /// </summary>
    internal sealed class CleanRoomImpurityRemover : ISaveMigrationPart
    {
        private const string RoomsKey = "cleanRoomRooms";
        private const string ImpurityCountKey = "impurityCount";

        private readonly List<JObject> _targets = new();

        public bool TryPrepare(JObject save, out string failureReason)
        {
            // 部屋キーが無い・明示nullのセーブはローダーが空リストとして受けるので対象なし
            // A save without the rooms key or with an explicit null is read as an empty list by the loader, so nothing to do
            var roomsToken = save[RoomsKey];
            failureReason = null;
            if (MigrationSaveJson.IsAbsentOrNull(roomsToken)) return true;
            if (!(roomsToken is JArray rooms)) return MigrationSaveJson.Reject($"{RoomsKey} が配列ではありません。 type={roomsToken.Type}", out failureReason);

            // 形を検査しながら不純物量を持つ部屋だけ集める
            // Collect only rooms holding an impurity count while checking shapes
            foreach (var roomToken in rooms)
            {
                if (!(roomToken is JObject room)) return MigrationSaveJson.Reject($"{RoomsKey} の要素がオブジェクトではありません。 type={roomToken.Type}", out failureReason);
                if (room.ContainsKey(ImpurityCountKey)) _targets.Add(room);
            }

            return true;
        }

        public string ApplyAndSummarize()
        {
            foreach (var room in _targets) room.Remove(ImpurityCountKey);
            return $"不純物量を消した部屋={_targets.Count}件";
        }
    }
}
