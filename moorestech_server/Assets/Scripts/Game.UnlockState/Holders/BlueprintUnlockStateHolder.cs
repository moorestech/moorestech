using System;
using System.Linq;
using Core.Master;
using Game.UnlockState.States;
using UniRx;

namespace Game.UnlockState.Holders
{
    public class BlueprintUnlockStateHolder
    {
        public IObservable<Unit> OnUnlock => _onUnlock;
        public bool IsUnlocked => _info.IsUnlocked;

        private readonly Subject<Unit> _onUnlock = new();
        private BlueprintUnlockStateInfo _info;

        public BlueprintUnlockStateHolder()
        {
            // 機能全体の単一フラグ。buildToolsのinitialUnlockedからシードする（ADR 0015）
            // Single feature-wide flag seeded from buildTools initialUnlocked (ADR 0015)
            var initialUnlocked = MasterHolder.BuildToolMaster.All.Any(tool => tool.InitialUnlocked);
            _info = new BlueprintUnlockStateInfo(initialUnlocked);
        }

        public void Unlock()
        {
            // 複数の研究/チャレンジから重複解放されてもイベントは一度だけ
            // Unlocks from multiple researches/challenges fire the event only once
            if (_info.IsUnlocked) return;
            _info.Unlock();
            _onUnlock.OnNext(Unit.Default);
        }

        public void Load(BlueprintUnlockStateInfoJsonObject jsonObject)
        {
            // 旧セーブは項目欠損＝シード値（未解放）のまま
            // Old saves lack this field, so the seed value (locked) stays
            if (jsonObject == null) return;
            _info = new BlueprintUnlockStateInfo(jsonObject);
        }

        public BlueprintUnlockStateInfoJsonObject GetSaveJsonObject()
        {
            return new BlueprintUnlockStateInfoJsonObject(_info);
        }
    }
}
