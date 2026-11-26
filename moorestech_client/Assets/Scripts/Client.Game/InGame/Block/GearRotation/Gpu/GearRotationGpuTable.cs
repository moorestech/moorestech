using UnityEngine;

namespace Client.Game.InGame.Block.GearRotation.Gpu
{
    /// <summary>
    /// シェーダーで回す歯車の、ブロックごとの回転状態。歯車が状態を受け取ったときに書き、GearRotationGpuUploaderがGPUへ送る
    /// 歯車はInstantiateで作られDIの注入を受けないので、GearRotationUpdateListと同じくstaticで持つ
    /// Per-block rotation state of shader-spun gears; gears write it on state reception and GearRotationGpuUploader sends it to the GPU
    /// Gears are created by Instantiate without DI injection, so this is static like GearRotationUpdateList
    /// </summary>
    public static class GearRotationGpuTable
    {
        private static readonly int TableId = Shader.PropertyToID("_GearRotationTable");
        private static readonly int MaskId = Shader.PropertyToID("_GearRotationTableMask");
        private static readonly int ProbeLimitId = Shader.PropertyToID("_GearRotationProbeLimit");
        private static readonly int TimeId = Shader.PropertyToID("_GearRotationTime");

        private static readonly GearRotationGpuHashTable Table = new();
        private static GraphicsBuffer _buffer;
        private static bool _dirty = true;

        public static void SetState(Vector3 blockOrigin, float rpm, bool isClockwise, float now)
        {
            Table.SetState(GearRotationGpuHashTable.ToKey(blockOrigin), rpm, isClockwise, now);
            _dirty = true;
        }

        public static void Remove(Vector3 blockOrigin)
        {
            Table.Remove(GearRotationGpuHashTable.ToKey(blockOrigin));
            _dirty = true;
        }

        // 時刻は毎回、表は変わったときだけ送る。行が無くても表を結び付け、シェーダーが未設定のバッファを読まないようにする
        // Time is sent every call and the table only when changed; the table is always bound so shaders never read an unset buffer
        public static void Upload(float now)
        {
            Shader.SetGlobalFloat(TimeId, now);
            if (!_dirty) return;

            var rows = Table.Rows;
            if (_buffer == null || _buffer.count != rows.Length)
            {
                _buffer?.Release();
                _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, rows.Length, GearRotationGpuRow.Stride);
                Shader.SetGlobalBuffer(TableId, _buffer);
            }
            _buffer.SetData(rows);
            Shader.SetGlobalInteger(MaskId, rows.Length - 1);
            Shader.SetGlobalInteger(ProbeLimitId, Table.ProbeLimit);
            _dirty = false;
        }
    }
}
