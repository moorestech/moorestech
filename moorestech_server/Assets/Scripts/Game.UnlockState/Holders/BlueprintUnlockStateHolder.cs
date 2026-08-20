using System;
using Core.Master;
using Game.UnlockState.States;
using UniRx;

namespace Game.UnlockState.Holders
{
    public class BlueprintUnlockStateHolder
    {
        public IObservable<Unit> OnUnlock => _onUnlock;
        public bool IsUnlocked { get; private set; }

        private readonly Subject<Unit> _onUnlock = new();

        public BlueprintUnlockStateHolder()
        {
            // 機能全体の単一フラグ。buildMenuルートのblueprintInitialUnlockedからシードする（ADR 0015）
            // Single feature-wide flag seeded from buildMenu's root blueprintInitialUnlocked (ADR 0015)
            IsUnlocked = MasterHolder.BuildToolMaster.BlueprintInitialUnlocked;
        }

        public void Unlock()
        {
            // 複数の研究/チャレンジから重複解放されてもイベントは一度だけ
            // Unlocks from multiple researches/challenges fire the event only once
            if (IsUnlocked) return;
            IsUnlocked = true;
            _onUnlock.OnNext(Unit.Default);
        }

        public void Load(BlueprintUnlockStateInfoJsonObject jsonObject)
        {
            // 旧セーブは項目欠損＝シード値（未解放）のまま
            // Old saves lack this field, so the seed value (locked) stays
            if (jsonObject == null) return;
            IsUnlocked = jsonObject.IsUnlocked;
        }

        public BlueprintUnlockStateInfoJsonObject GetSaveJsonObject()
        {
            return new BlueprintUnlockStateInfoJsonObject(IsUnlocked);
        }
    }
}
