using System;
using System.Collections.Generic;
using Client.Network.API;
using Client.Ui.State.Recipe.Data;
using Game.Block.Interface.MachineUpgrade;
using Game.UnlockState;
using Server.Event.EventReceive;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Recipe.Source
{
    /// <summary>
    /// 解放済みの機械レシピ一覧の源。解放イベントと製作上限の解放で作り直す（Web MachineRecipesTopic の契機と同じ）
    /// Source of the unlocked machine-recipe list; rebuilt on unlock events and craft-cap unlocks (same triggers as Web MachineRecipesTopic)
    /// </summary>
    public sealed class UnlockedMachineRecipeSource : IInitializable
    {
        private readonly IGameUnlockStateData _unlockState;
        private readonly ILevelInheritanceCraftCapLookup _craftCapLookup;
        private readonly IVanillaApiEvent _vanillaApiEvent;
        private readonly ReactiveProperty<IReadOnlyList<MachineRecipeData>> _current = new(Array.Empty<MachineRecipeData>());

        public IReadOnlyReactiveProperty<IReadOnlyList<MachineRecipeData>> Current => _current;

        // 解放状態を ctor で受けるので、その購読（ClientGameUnlockStateData）が先に張られ更新後の表を読める
        // Taking the unlock state in the ctor makes its subscription (ClientGameUnlockStateData) register first, so the updated table is read
        public UnlockedMachineRecipeSource(IGameUnlockStateData unlockState, ILevelInheritanceCraftCapLookup craftCapLookup, IVanillaApiEvent vanillaApiEvent)
        {
            _unlockState = unlockState;
            _craftCapLookup = craftCapLookup;
            _vanillaApiEvent = vanillaApiEvent;
        }

        public void Initialize()
        {
            Rebuild();
            _vanillaApiEvent.SubscribeEventResponse(UnlockedEventPacket.EventTag, _ => Rebuild());
            _craftCapLookup.OnCraftCapUnlocked.Subscribe(_ => Rebuild());

            #region Internal

            void Rebuild()
            {
                _current.Value = UnlockedMachineRecipeListBuilder.Build(_unlockState, _craftCapLookup);
            }

            #endregion
        }
    }
}
