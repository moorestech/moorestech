using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.UnlockState.States;
using UniRx;

namespace Game.UnlockState.Holders
{
    public class ItemUnlockStateHolder
    {
        public IObservable<ItemId> OnUnlock => _onUnlock;
        public IReadOnlyDictionary<ItemId, ItemUnlockStateInfo> Infos => _infos;

        private readonly Subject<ItemId> _onUnlock = new();
        private readonly Dictionary<ItemId, ItemUnlockStateInfo> _infos = new();

        public ItemUnlockStateHolder()
        {
            foreach (var itemId in MasterHolder.ItemMaster.GetItemAllIds())
            {
                if (_infos.ContainsKey(itemId)) continue;
                var itemMaster = MasterHolder.ItemMaster.GetItemMaster(itemId);
                _infos.Add(itemId, new ItemUnlockStateInfo(itemId, itemMaster.InitialUnlocked));
            }
        }

        public void Unlock(ItemId itemId)
        {
            // 複数の研究から重複解放されても通知は一度だけ（前例 BlueprintUnlockStateHolder.Unlock）
            // Unlocks from multiple researches fire the notification only once (precedent BlueprintUnlockStateHolder.Unlock)
            // silent-ok: 解放済みは状態が変わらず、知らせることが無い / 最初の解放の通知と研究画面の解放一覧でプレイヤーは既に知っている
            if (_infos[itemId].IsUnlocked) return;
            _infos[itemId].Unlock();
            _onUnlock.OnNext(itemId);
        }

        public void Load(List<ItemUnlockStateInfoJsonObject> jsonObjects)
        {
            if (jsonObjects == null) return;
            foreach (var jsonObject in jsonObjects)
            {
                // マスタに存在しないアイテムはスキップ
                // Skip items that don't exist in master
                if (!MasterHolder.ItemMaster.ExistItemId(Guid.Parse(jsonObject.ItemGuid))) continue;
                var state = new ItemUnlockStateInfo(jsonObject);
                _infos[state.ItemId] = state;
            }
        }

        public List<ItemUnlockStateInfoJsonObject> GetSaveJsonObject()
        {
            return _infos.Values.Select(i => new ItemUnlockStateInfoJsonObject(i)).ToList();
        }
    }
}
