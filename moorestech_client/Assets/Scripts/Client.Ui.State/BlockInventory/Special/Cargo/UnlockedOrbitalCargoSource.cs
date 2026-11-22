using System;
using System.Collections.Generic;
using Client.Network.API;
using Core.Master;
using Game.UnlockState;
using Server.Event.EventReceive;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.BlockInventory.Special.Cargo
{
    /// <summary>
    /// 解放済みの積荷の集合。アイテムの解放イベントで作り直す（前例 UnlockedMachineRecipeSource と同じ契機）
    /// The set of unlocked cargo kinds, rebuilt on item unlock events (same trigger as UnlockedMachineRecipeSource)
    /// </summary>
    public sealed class UnlockedOrbitalCargoSource : IUnlockedOrbitalCargoSource, IInitializable
    {
        private readonly IGameUnlockStateData _unlockState;
        private readonly IVanillaApiEvent _vanillaApiEvent;
        private readonly ReactiveProperty<IReadOnlyCollection<ItemId>> _unlocked = new(Array.Empty<ItemId>());

        public IReadOnlyReactiveProperty<IReadOnlyCollection<ItemId>> UnlockedPayloadItemIds => _unlocked;

        // 解放状態を ctor で受けるので、その購読（ClientGameUnlockStateData）が先に張られ更新後の表を読める
        // Taking the unlock state in the ctor makes its subscription (ClientGameUnlockStateData) register first, so the updated table is read
        public UnlockedOrbitalCargoSource(IGameUnlockStateData unlockState, IVanillaApiEvent vanillaApiEvent)
        {
            _unlockState = unlockState;
            _vanillaApiEvent = vanillaApiEvent;
        }

        public void Initialize()
        {
            Rebuild();
            _vanillaApiEvent.SubscribeEventResponse(UnlockedEventPacket.EventTag, _ => Rebuild());

            #region Internal

            void Rebuild()
            {
                // 積荷表の種類のうちアイテムが解放済みのもの（ADR 0106 の 3）
                // Payload-table kinds whose item is unlocked (ADR 0106 §3)
                var unlocked = new HashSet<ItemId>();
                foreach (var payload in MasterHolder.ItemMaster.Items.OrbitalPayloads)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(payload.PayloadItemGuid);
                    if (_unlockState.ItemUnlockStateInfos[itemId].IsUnlocked) unlocked.Add(itemId);
                }
                _unlocked.Value = unlocked;
            }

            #endregion
        }
    }
}
