using Client.Game.InGame.Train.Unit;
using UnityEngine;
using UnityEngine.VFX;

namespace Client.Game.InGame.Train.View.Object
{
    public sealed class TrainSmokeController : MonoBehaviour
    {
        [SerializeField] private VisualEffect smokeEffect;

        private TrainCarEntityObject _trainCarEntity;
        private TrainUnitClientCache _trainCache;
        private bool _isReady;

        public void SetDependencies(TrainCarEntityObject trainCarEntity, TrainUnitClientCache trainCache)
        {
            // 依存参照を保持し、Prefab設定済みVFXを使う
            // Store dependencies and use the prefab-assigned VFX
            _trainCarEntity = trainCarEntity;
            _trainCache = trainCache;

            // 初期状態では煙を止めておく
            // Start with smoke disabled
            ApplySmokeParameters(0);
            _isReady = true;
        }

        private void Update()
        {
            // 初期化が終わった後だけ煙を更新する
            // Update smoke only after initialization completes
            if (!_isReady)
            {
                return;
            }

            // 現在のマスコン段数から煙量を更新する
            // Update smoke intensity from the current mascon level
            var masconLevel = TryResolveMasconLevel(out var resolvedMasconLevel) ? resolvedMasconLevel : 0;
            ApplySmokeParameters(masconLevel);
        }

        #region Internal

        private bool TryResolveMasconLevel(out int masconLevel)
        {
            // 出力値を先に初期化する
            // Initialize the output value first
            masconLevel = 0;
            if (_trainCache == null || _trainCarEntity == null)
            {
                return false;
            }

            // 所属列車のsnapshotから現在のマスコン段数を読む
            // Read the current level from the owning train snapshot
            if (!_trainCache.TryGetCarSnapshot(_trainCarEntity.TrainCarInstanceId, out var unit, out _, out _, out _))
            {
                return false;
            }

            masconLevel = unit.MasconLevel;
            return true;
        }

        private void ApplySmokeParameters(int masconLevel)
        {
            // 黒煙VFXはIntensityだけを制御する
            // Control only the Intensity property for black smoke
            smokeEffect.SetFloat(TrainSmokeProperty.Intensity, TrainSmokeLogic.ResolveIntensity(masconLevel));
        }

        #endregion
    }

    public static class TrainSmokeLogic
    {
        public static float ResolveIntensity(int masconLevel)
        {
            // マスコン0以下では煙を止める
            // Stop smoke when the mascon is zero or lower
            if (masconLevel <= 0)
            {
                return 0f;
            }

            // マスコン1以上では黒煙を出す
            // Emit black smoke when the mascon is one or higher
            return 1f;
        }
    }

    internal static class TrainSmokeProperty
    {
        public const string Intensity = "Intensity";
    }
}
