using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;

namespace Game.Construction
{
    /// <summary>
    /// ブロックごとの課金元プレイヤー。撤去時に「設置して支払った人」の財布へ戻すために持つ
    /// The paying player per block, kept so a removal returns to the wallet of whoever placed and paid for it
    /// </summary>
    public class ConstructionPayerDataStore
    {
        private readonly Dictionary<BlockInstanceId, int> _payers = new();

        public void SetPayer(BlockInstanceId blockInstanceId, int playerId)
        {
            _payers[blockInstanceId] = playerId;
        }

        // 記録が無いブロックは撤去要求者を課金元とみなす（財布を通らずに置かれたブロック）
        // A block with no record falls back to the remover, which is what a block placed outside the wallet means
        public int GetPayer(BlockInstanceId blockInstanceId, int fallbackPlayerId)
        {
            return _payers.TryGetValue(blockInstanceId, out var playerId) ? playerId : fallbackPlayerId;
        }

        public void RemovePayer(BlockInstanceId blockInstanceId)
        {
            _payers.Remove(blockInstanceId);
        }

        public List<ConstructionPayerSaveJsonObject> GetSaveJsonObject()
        {
            return _payers.Select(payer => new ConstructionPayerSaveJsonObject(payer.Key.AsPrimitive(), payer.Value)).ToList();
        }

        public void LoadPayers(List<ConstructionPayerSaveJsonObject> saveData)
        {
            _payers.Clear();
            foreach (var payer in saveData) _payers[new BlockInstanceId(payer.BlockInstanceId)] = payer.PlayerId;
        }
    }
}
