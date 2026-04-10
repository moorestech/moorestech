using Client.Game.InGame.Train.View;
using UnityEngine;
using UnityEngine.VFX;

namespace Client.Game.InGame.Train.View.Object
{
    public sealed class TrainSmokeVfxProcessor : MonoBehaviour, ITrainCarObjectProcessor
    {
        [SerializeField] private VisualEffect smokeEffect;

        private bool _isReady;

        public void Initialize(TrainCarEntityObject trainCarEntityObject)
        {
            // 初期状態では煙を止めておく
            // Start with smoke disabled
            ApplySmokeParameters(0);
            _isReady = true;
        }

        public void Update(TrainCarContext context)
        {
            // 初期化が終わった後だけ煙を更新する
            // Update smoke only after initialization completes
            if (!_isReady)
            {
                return;
            }

            // context から渡されたマスコン段数だけで煙量を決める
            // Drive smoke intensity only from the shared context
            ApplySmokeParameters(context.HasSnapshot ? context.MasconLevel : 0);
        }

        #region Internal

        private void ApplySmokeParameters(int masconLevel)
        {
            // 黒煙VFXは Intensity だけを制御する
            // Control only the Intensity property for black smoke
            smokeEffect.SetFloat(TrainSmokeProperty.Intensity, TrainSmokeLogic.ResolveIntensity(masconLevel));
        }

        #endregion
    }

    public static class TrainSmokeLogic
    {
        public static float ResolveIntensity(int masconLevel)
        {
            // マスコン 0 以下では煙を止める
            // Stop smoke when the mascon is zero or lower
            if (masconLevel <= 0)
            {
                return 0f;
            }

            // マスコン 1 以上では黒煙を出す
            // Emit black smoke when the mascon is one or higher
            return 1f;
        }
    }

    internal static class TrainSmokeProperty
    {
        public const string Intensity = "Intensity";
    }
}
