using UnityEngine;

namespace Client.Game.InGame.Train
{
    public sealed class RailObjectIdCarrier : MonoBehaviour
    {
        private ulong _railObjectId;

        public void SetRailObjectId(ulong railObjectId)
        {
            // レールオブジェクトIDを保持する
            // Store the rail object id for raycast lookup
            _railObjectId = railObjectId;
        }

        public ulong GetRailObjectId()
        {
            // レールID参照用の取得口
            // Read-only accessor for the rail object id
            return _railObjectId;
        }
    }
}
