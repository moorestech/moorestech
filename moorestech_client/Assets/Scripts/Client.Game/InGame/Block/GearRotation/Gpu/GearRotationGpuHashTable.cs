using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.Block.GearRotation.Gpu
{
    /// <summary>
    /// ブロック原点をキーにした、開番地法(線形探索)のハッシュ表。シェーダーが同じ式で引く
    /// 消した行は削除済みの印を残し、印が増えたら作り直す
    /// Open-addressing (linear probing) hash table keyed by block origin; the shader probes it the same way
    /// Removed rows keep a tombstone, and the table is rebuilt once tombstones pile up
    /// </summary>
    public sealed class GearRotationGpuHashTable
    {
        // 角度をこの周期で畳む。速さの倍率が0.1の倍数なら畳んでも見た目が飛ばない
        // Angles wrap at this period; it causes no visible jump for speed multipliers that are multiples of 0.1
        private const float AngleWrapDegrees = 3600f;
        private const int MinCapacity = 1024;

        public GearRotationGpuRow[] Rows { get; private set; } = new GearRotationGpuRow[MinCapacity];
        public int ProbeLimit { get; private set; } = 1;
        private readonly Dictionary<Vector3Int, int> _slotByKey = new();
        private int _removedCount;

        public static Vector3Int ToKey(Vector3 blockOrigin)
        {
            return Vector3Int.RoundToInt(blockOrigin * 2f);
        }

        // GearRotation.hlslのGearRotationHashと同じ式
        // Same formula as GearRotationHash in GearRotation.hlsl
        public static uint Hash(Vector3Int key)
        {
            return unchecked(((uint)key.x * 73856093u) ^ ((uint)key.y * 19349663u) ^ ((uint)key.z * 83492791u));
        }

        public void SetState(Vector3Int key, float rpm, bool isClockwise, float now)
        {
            var degreesPerSecond = rpm * 6f * (isClockwise ? 1f : -1f);
            if (_slotByKey.TryGetValue(key, out var slot))
            {
                // 今の角度まで進めてから速さを替え、回転を途切れさせない
                // Advance to the current angle before switching speed so the spin stays continuous
                ref var row = ref Rows[slot];
                var elapsed = now - row.BaseTime;
                row.SignedAngle = Mathf.Repeat(row.SignedAngle + row.SignedDegreesPerSecond * elapsed, AngleWrapDegrees);
                row.AbsoluteAngle = Mathf.Repeat(row.AbsoluteAngle + Mathf.Abs(row.SignedDegreesPerSecond) * elapsed, AngleWrapDegrees);
                row.SignedDegreesPerSecond = degreesPerSecond;
                row.BaseTime = now;
                return;
            }

            if ((_slotByKey.Count + _removedCount + 1) * 2 > Rows.Length) Rebuild(_slotByKey.Count + 1);
            Insert(new GearRotationGpuRow { Key = key, SlotState = GearRotationGpuRow.UsedState, SignedDegreesPerSecond = degreesPerSecond, BaseTime = now });
        }

        public void Remove(Vector3Int key)
        {
            if (!_slotByKey.Remove(key, out var slot)) return; // silent-ok: 状態を受け取る前に消えたブロックは行が無い / GearRotationGpuHashTableTestで確認する
            Rows[slot].SlotState = GearRotationGpuRow.RemovedState;
            _removedCount++;
        }

        public bool TryFind(Vector3Int key, out GearRotationGpuRow found)
        {
            // シェーダーと同じ探し方で引く。テストでシェーダーの探索を再現するのに使う
            // Probe exactly like the shader; used by tests to reproduce the shader lookup
            var mask = (uint)Rows.Length - 1;
            var hash = Hash(key);
            for (uint i = 0; i < ProbeLimit; i++)
            {
                var row = Rows[(hash + i) & mask];
                if (row.SlotState == GearRotationGpuRow.EmptyState) break;
                if (row.SlotState == GearRotationGpuRow.UsedState && row.Key == key)
                {
                    found = row;
                    return true;
                }
            }
            found = default;
            return false;
        }

        private void Insert(GearRotationGpuRow row)
        {
            var mask = (uint)Rows.Length - 1;
            var hash = Hash(row.Key);
            for (uint i = 0; ; i++)
            {
                var slot = (int)((hash + i) & mask);
                if (Rows[slot].SlotState != GearRotationGpuRow.EmptyState) continue;
                Rows[slot] = row;
                _slotByKey[row.Key] = slot;
                ProbeLimit = Mathf.Max(ProbeLimit, (int)i + 1);
                return;
            }
        }

        private void Rebuild(int requiredCount)
        {
            // 使用中の行だけを、埋まり方が半分以下になる大きさの表へ入れ直す
            // Reinsert only used rows into a table large enough to stay at most half full
            var capacity = Mathf.Max(MinCapacity, Rows.Length);
            while (requiredCount * 2 > capacity) capacity *= 2;
            var used = new List<GearRotationGpuRow>(_slotByKey.Count);
            foreach (var slot in _slotByKey.Values) used.Add(Rows[slot]);

            Rows = new GearRotationGpuRow[capacity];
            _slotByKey.Clear();
            _removedCount = 0;
            ProbeLimit = 1;
            foreach (var row in used) Insert(row);
        }
    }
}
