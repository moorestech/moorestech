using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Pruning.Items
{
    /// <summary>除去したアイテムがどこにあったか。後日の返金を元の持ち主・格納先・スロットへ戻すための住所</summary>
    /// <summary>Where a pruned item sat; the address a later refund uses to return it to its owner, container and slot</summary>
    public sealed class PrunedItemOrigin
    {
        private readonly string _section;
        private readonly int? _playerId;
        private readonly int? _blockInstanceId;
        private readonly long? _trainCarInstanceId;
        private readonly string _stateKey;
        private readonly string _container;
        private readonly int? _slot;
        private readonly string _path;
        private readonly string _field;

        private PrunedItemOrigin(string section, int? playerId, int? blockInstanceId, long? trainCarInstanceId, string stateKey,
            string container, int? slot, string path, string field)
        {
            _section = section;
            _playerId = playerId;
            _blockInstanceId = blockInstanceId;
            _trainCarInstanceId = trainCarInstanceId;
            _stateKey = stateKey;
            _container = container;
            _slot = slot;
            _path = path;
            _field = field;
        }

        public static PrunedItemOrigin PlayerInventorySlot(string section, int? playerId, string container, int slot)
        {
            return new PrunedItemOrigin(section, playerId, null, null, null, container, slot, null, null);
        }

        // ブロックのstateの1キー（コンポーネントのSaveKey）を持ち主とする。中の位置はAtで足す
        // Owned by one key of a block's state (a component SaveKey); the position inside is added with At
        public static PrunedItemOrigin BlockState(string section, int? blockInstanceId, string stateKey)
        {
            return new PrunedItemOrigin(section, null, blockInstanceId, null, stateKey, null, null, null, null);
        }

        public static PrunedItemOrigin TrainCarContainer(string section, long? trainCarInstanceId)
        {
            return new PrunedItemOrigin(section, null, null, trainCarInstanceId, null, null, null, null, null);
        }

        // 持ち主の中での位置を足す。埋め込みJSONの内側に位置が無ければ外側の格納先・スロットを引き継ぐ
        // Adds the position inside the owner; inside embedded JSON, a missing inner container or slot inherits the outer one
        public PrunedItemOrigin At(string container, int? slot, string path)
        {
            var joinedPath = string.IsNullOrEmpty(_path) ? path : string.IsNullOrEmpty(path) ? _path : $"{_path}>{path}";
            return new PrunedItemOrigin(_section, _playerId, _blockInstanceId, _trainCarInstanceId, _stateKey,
                container ?? _container, slot ?? _slot, joinedPath, _field);
        }

        // 裸guidの項目名。在庫スタックではなく項目そのものが1件の参照であることを示す
        // The bare-guid field name, marking that the field itself rather than a stack is the reference
        public PrunedItemOrigin WithField(string field)
        {
            return new PrunedItemOrigin(_section, _playerId, _blockInstanceId, _trainCarInstanceId, _stateKey, _container, _slot, _path, field);
        }

        // 節の構造から取れない値は書かない。nullを並べると「0番の持ち主」等と読み違える
        // Values the section cannot provide are omitted; spelling nulls invites misreading them as "owner 0" and the like
        public JObject ToJson()
        {
            var json = new JObject { ["section"] = _section };
            if (_playerId.HasValue) json["playerId"] = _playerId.Value;
            if (_blockInstanceId.HasValue) json["blockInstanceId"] = _blockInstanceId.Value;
            if (_trainCarInstanceId.HasValue) json["trainCarInstanceId"] = _trainCarInstanceId.Value;
            if (_stateKey != null) json["stateKey"] = _stateKey;
            if (_container != null) json["container"] = _container;
            if (_slot.HasValue) json["slot"] = _slot.Value;
            if (!string.IsNullOrEmpty(_path)) json["path"] = _path;
            if (_field != null) json["field"] = _field;
            return json;
        }
    }
}
