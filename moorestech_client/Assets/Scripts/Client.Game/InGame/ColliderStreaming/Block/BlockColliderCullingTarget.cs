using UnityEngine;

namespace Client.Game.InGame.ColliderStreaming.Block
{
    /// <summary>
    /// ブロック配下のコライダーをまとめてオンオフする距離カリング対象
    /// A distance-culling target that toggles all colliders under one block
    /// </summary>
    public sealed class BlockColliderCullingTarget : IColliderDistanceCullingTarget
    {
        private readonly Collider[] _colliders;
        private readonly bool[] _savedEnabled;

        public BlockColliderCullingTarget(Collider[] colliders)
        {
            _colliders = colliders;
            _savedEnabled = new bool[colliders.Length];
        }

        // 無効化する瞬間に現在の有効状態を保存し、有効化時はそれを復元する（実行時トグルを尊重）
        // On disable save the current enabled state, on enable restore it (respects runtime toggles)
        public void SetCollider(bool on)
        {
            for (var i = 0; i < _colliders.Length; i++)
            {
                var collider = _colliders[i];
                if (collider == null) continue;

                if (on)
                {
                    collider.enabled = _savedEnabled[i];
                }
                else
                {
                    _savedEnabled[i] = collider.enabled;
                    collider.enabled = false;
                }
            }
        }
    }
}
