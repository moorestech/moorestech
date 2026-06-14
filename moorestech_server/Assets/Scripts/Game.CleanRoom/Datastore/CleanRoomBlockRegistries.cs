using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CleanRoom.Machine;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.CleanRoom
{
    // エアフィルターと状態受信ブロックの登録簿。設置/破壊イベントで自動登録し、純度tick後に部屋効果をプッシュする。
    // Registries for air filters and state-receiver blocks; auto-registered on place/remove, with room effects pushed after the purity tick.
    public class CleanRoomBlockRegistries
    {
        // エアフィルター登録（セル→フィルター）。1×1×1 なので MinPos がセルキー。
        // Air filter registry (cell -> filter); blocks are 1x1x1 so MinPos is the cell key.
        private readonly Dictionary<Vector3Int, ICleanRoomAirFilter> _airFilters = new();

        // 状態受信ブロック登録（BlockInstanceId→ブロック）。ブロック参照を持つことで占有セルの帰属判定を委譲できる。
        // State-receiver registry (instance id -> block); holding the block lets us reuse the multi-block membership check.
        private readonly Dictionary<BlockInstanceId, (IBlock block, ICleanRoomStateReceiver receiver)> _stateReceivers = new();

        public void AddAirFilter(Vector3Int cell, ICleanRoomAirFilter filter)
        {
            _airFilters[cell] = filter;
        }

        public void RemoveAirFilter(Vector3Int cell)
        {
            _airFilters.Remove(cell);
        }

        // 設置イベントで実エアフィルターをレジストリへ登録する。
        // Register a real air filter from the place event.
        public void RegisterAirFilterOnPlace(WorldBlockData blockData)
        {
            if (blockData.Block.TryGetComponent<ICleanRoomAirFilter>(out var filter))
                AddAirFilter(blockData.BlockPositionInfo.MinPos, filter);
        }

        // 破壊イベントでレジストリから解除する。
        // Unregister from the registry on the remove event.
        public void UnregisterAirFilterOnRemove(WorldBlockData blockData)
        {
            if (blockData.Block.TryGetComponent<ICleanRoomAirFilter>(out _))
                RemoveAirFilter(blockData.BlockPositionInfo.MinPos);
        }

        // 設置イベントで状態受信ブロックを登録する（multi-block 占有判定用にブロック参照ごと保持）。
        // Register a state-receiver block from the place event (hold the block ref for the membership check).
        public void RegisterStateReceiverOnPlace(WorldBlockData blockData)
        {
            if (blockData.Block.TryGetComponent<ICleanRoomStateReceiver>(out var receiver))
                _stateReceivers[blockData.Block.BlockInstanceId] = (blockData.Block, receiver);
        }

        // 破壊イベントで状態受信ブロックを解除する。
        // Unregister a state-receiver block on the remove event.
        public void UnregisterStateReceiverOnRemove(WorldBlockData blockData)
        {
            if (blockData.Block.TryGetComponent<ICleanRoomStateReceiver>(out _))
                _stateReceivers.Remove(blockData.Block.BlockInstanceId);
        }

        // 部屋に属するフィルターを集める（登録セルが部屋の Cells に含まれるもの）。
        // Collect filters whose registered cell lies in the room's Cells.
        public List<ICleanRoomAirFilter> CollectForRoom(CleanRoom room)
        {
            var result = new List<ICleanRoomAirFilter>();
            foreach (var kvp in _airFilters)
                if (room.Contains(kvp.Key)) result.Add(kvp.Value);
            return result;
        }

        // 登録済み受信ブロックへ、属する部屋の効果（無ければ最悪側）を毎tickプッシュする。
        // 同一部屋に全占有セルが含まれる時だけ部屋効果を、またがり/部屋外/無効化時は最悪側をプッシュ。
        // Push each registered receiver the effect of its owning room, or the worst case if it has none.
        public void PushCleanRoomEffects(CleanRoomWorld world)
        {
            foreach (var entry in _stateReceivers.Values)
            {
                if (world.TryGetCleanRoom(entry.block, out var room))
                    entry.receiver.SetCleanRoomEffect(CleanRoomEffectResolver.Resolve(room));
                else
                    entry.receiver.SetCleanRoomEffect(new CleanRoomEffect(false, 0, 0.0));
            }
        }
    }
}
