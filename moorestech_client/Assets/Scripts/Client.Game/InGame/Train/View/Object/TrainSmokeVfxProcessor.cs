using System;
using Client.Game.InGame.Train.View;
using UnityEngine;
using UnityEngine.VFX;

namespace Client.Game.InGame.Train.View.Object
{
    public class TrainSmokeVfxProcessor : MonoBehaviour, ITrainCarObjectProcessor
    {
        [SerializeField] private VisualEffect smokeVfx;

        public void Initialize(TrainCarEntityObject trainCarEntityObject)
        {
            // Prefab 側に置かれた VFX を解決して初期停止させる
            // Resolve the prefab-owned VFX and stop it initially
            smokeVfx ??= GetComponentInChildren<VisualEffect>(true);
            if (smokeVfx == null)
            {
                throw new InvalidOperationException("Train smoke VFX processor requires a VisualEffect in the prefab hierarchy.");
            }
            if (!smokeVfx.HasFloat(TrainSmokeVfxProperty.Intensity))
            {
                throw new InvalidOperationException($"Train smoke VFX is missing controllable float. Property:{TrainSmokeVfxProperty.Intensity}");
            }
            ApplyIntensity(0);
        }

        public void Update(TrainCarContext context)
        {
            // snapshot が無い時は煙を止める
            // Stop smoke when snapshot is unavailable
            var masconLevel = context.HasSnapshot ? context.MasconLevel : 0;
            ApplyIntensity(masconLevel);
        }

        #region Internal

        private void ApplyIntensity(int masconLevel)
        {
            // 黒煙 VFX は Intensity だけを制御する
            // Control only the Intensity property for the smoke VFX
            smokeVfx.SetFloat(TrainSmokeVfxProperty.Intensity, TrainSmokeVfxLogic.ResolveIntensity(masconLevel));
        }

        #endregion
    }

    public static class TrainSmokeVfxLogic
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
            // Emit smoke when the mascon is one or higher
            return 1f;
        }
    }

    internal static class TrainSmokeVfxProperty
    {
        public const string Intensity = "Intensity";
    }
}
