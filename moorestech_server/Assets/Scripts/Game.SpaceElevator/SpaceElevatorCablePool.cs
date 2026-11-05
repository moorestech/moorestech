using System;
using System.Collections.Generic;
using Core.Master;
using Game.Context;
using Game.Orbit;
using Game.SpaceElevator.Save;
using UniRx;
using UnityEngine;

namespace Game.SpaceElevator
{
    /// <summary>
    ///     ワールドに1つのケーブルプール。ケーブルの軌道到着で増え、接続で減り、接続済み拠点の撤去で戻る（ADR 0092）
    ///     The world's single cable pool: grows on cable orbital arrivals, shrinks on connections, refills when a connected base is removed (ADR 0092)
    /// </summary>
    public class SpaceElevatorCablePool : IBootInitializable
    {
        public int Amount { get; private set; }
        public IObservable<int> OnAmountChanged => _onAmountChanged;

        private readonly Subject<int> _onAmountChanged = new();
        private readonly IOrbitalArrivalService _orbitalArrivalService;

        // 拠点のマスタが数えるケーブル。検証で高々1種に揃う。拠点の無いマスタでは空で何も数えない
        // Cable items the base masters count; validation keeps it to at most one, and a master without bases leaves it empty
        private readonly HashSet<ItemId> _cableItemIds = new();

        public SpaceElevatorCablePool(IOrbitalArrivalService orbitalArrivalService)
        {
            _orbitalArrivalService = orbitalArrivalService;
        }

        public void Load()
        {
            // ケーブル収集（整合は読込時に検証済み）
            // Collect the cable items base masters count; table consistency was validated during master loading
            _cableItemIds.UnionWith(SpaceElevatorBaseMasters.CableItemIds());

            // ケーブルの到着だけを数える。どの打ち上げ口から来たかは見ない
            // Count only cable arrivals, regardless of which launch site sent them
            _orbitalArrivalService.OnOrbitalArrival.Subscribe(arrival =>
            {
                if (!_cableItemIds.Contains(arrival.PayloadItemId)) return;
                Add(arrival.PayloadCount);
            });
        }

        // 1エレベーター分が残っているか。接続できるかの判定はここだけが持つ
        // Whether one elevator's worth remains; this is the only place that judges whether a base can connect
        internal bool HasEnough(int count)
        {
            return count <= Amount;
        }

        // 足りれば引いてtrue。足りなければ何もしない（接続サービスだけが使う）
        // Take the count and return true when enough remains; otherwise change nothing (only the connection service uses this)
        internal bool TryTake(int count)
        {
            if (!HasEnough(count)) return false;
            SetAmount(Amount - count);
            return true;
        }

        internal void Add(int count)
        {
            // 到着と撤去返却の確定前に検査し、失敗時は残量も通知も変えない
            // Validate before committing an arrival or removal refund, leaving amount and notifications unchanged on failure
            if (count < 0 || int.MaxValue - Amount < count)
            {
                var reason = $"ケーブルプールの加算が整数範囲を超えます amount:{Amount} count:{count} max:{int.MaxValue}";
                Debug.LogError(reason);
                throw new InvalidOperationException(reason);
            }
            SetAmount(Amount + count);
        }

        public bool CanAcceptArrival(ItemId itemId, int count)
        {
            return !_cableItemIds.Contains(itemId) || (0 <= count && count <= int.MaxValue - Amount);
        }

        public SpaceElevatorCablePoolSaveData GetSaveData()
        {
            return new SpaceElevatorCablePoolSaveData { CableCount = Amount };
        }

        public void Restore(SpaceElevatorCablePoolSaveData saveData)
        {
            // 負の残量は手編集による破損。黙って0にせず止める
            // A negative amount means a hand-edited save; stop instead of silently clamping to zero
            if (saveData.CableCount < 0)
            {
                var reason = $"セーブの spaceElevatorCablePool.cableCount が負です value:{saveData.CableCount}";
                Debug.LogError(reason);
                throw new InvalidOperationException(reason);
            }
            SetAmount(saveData.CableCount);
        }

        private void SetAmount(int amount)
        {
            Amount = amount;
            _onAmountChanged.OnNext(amount);
        }
    }
}
