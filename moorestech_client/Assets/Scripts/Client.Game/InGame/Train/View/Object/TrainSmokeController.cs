using System;
using Client.Common.Asset;
using Client.Game.InGame.Train.Unit;
using UnityEngine;
using UnityEngine.VFX;

namespace Client.Game.InGame.Train.View.Object
{
    public sealed class TrainSmokeController : MonoBehaviour
    {
        private TrainCarEntityObject _trainCarEntity;
        private TrainUnitClientCache _trainCache;
        private VisualEffect _smokeEffect;
        private bool _isSmokeSupported;

        public void SetDependencies(TrainCarEntityObject trainCarEntity, TrainUnitClientCache trainCache, int tractionForce)
        {
            // 煙制御に必要な参照と対象判定を保持する
            // Store the references and support flag required for smoke control
            _trainCarEntity = trainCarEntity;
            _trainCache = trainCache;
            _isSmokeSupported = TrainSmokeLogic.IsSmokeSupported(tractionForce);
            if (!_isSmokeSupported)
            {
                return;
            }

            // 列車用黒煙 prefab を直接生成する
            // Create the train smoke prefab directly
            _smokeEffect = TrainSmokeEffectFactory.Create(transform);

            // 初期状態では煙を止めておく
            // Start with smoke disabled
            ApplySmokeParameters(0);
        }

        private void Update()
        {
            // 非対応車両は何もしない
            // Do nothing for unsupported cars
            if (!_isSmokeSupported)
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

            // 所属列車の snapshot から現在のマスコン段数を読む
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
            // 黒煙 VFX は Intensity だけを制御する
            // Control only the Intensity property for black smoke
            _smokeEffect.SetFloat(TrainSmokeProperty.Intensity, TrainSmokeLogic.ResolveIntensity(masconLevel));
        }

        #endregion
    }

    internal static class TrainSmokeEffectFactory
    {
        private const string SmokePrefabAddressablePath = "Assets/Asset/Block/Effect/Prefabs/FX_Smoke_Black.prefab";
        private static GameObject _cachedSmokeTemplate;

        public static VisualEffect Create(Transform parent)
        {
            var smokeTemplate = ResolveSmokeTemplate();

            // 列車ローカル座標に煙アンカーを作る
            // Create a smoke anchor in train local space
            var anchor = new GameObject("TrainSmokeAnchor");
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = TrainSmokeProfile.GetAnchorLocalPosition();
            anchor.transform.localRotation = Quaternion.identity;
            anchor.transform.localScale = Vector3.one;

            // 黒煙 prefab を生成して制御対象 VFX を取得する
            // Instantiate the black smoke prefab and capture the target VFX
            var smokeObject = UnityEngine.Object.Instantiate(smokeTemplate, anchor.transform);
            smokeObject.transform.localPosition = Vector3.zero;
            smokeObject.transform.localRotation = Quaternion.identity;
            smokeObject.transform.localScale = Vector3.one;
            var smokeEffect = smokeObject.GetComponentInChildren<VisualEffect>(true);
            if (smokeEffect != null && smokeEffect.HasFloat(TrainSmokeProperty.Intensity))
            {
                return smokeEffect;
            }

            // 制御不能な VFX はその場で失敗扱いにする
            // Treat uncontrollable VFX as an immediate failure
            UnityEngine.Object.Destroy(smokeObject);
            UnityEngine.Object.Destroy(anchor);
            throw new InvalidOperationException($"Train smoke VFX is missing controllable float. Property:{TrainSmokeProperty.Intensity}");
        }

        #region Internal

        private static GameObject ResolveSmokeTemplate()
        {
            // 一度見つけた template は使い回す
            // Reuse the cached template once resolved
            if (_cachedSmokeTemplate != null)
            {
                return _cachedSmokeTemplate;
            }

            // campfier 経由をやめて列車用黒煙 prefab を直接ロードする
            // Stop using the campfier child lookup and load the train smoke prefab directly
            var smokeSource = AddressableLoader.LoadDefault<GameObject>(SmokePrefabAddressablePath);
            if (smokeSource == null)
            {
                throw new InvalidOperationException($"Train smoke prefab load failed. AddressablePath:{SmokePrefabAddressablePath}");
            }

            _cachedSmokeTemplate = smokeSource;
            return _cachedSmokeTemplate;
        }

        #endregion
    }

    public static class TrainSmokeLogic
    {
        public static bool IsSmokeSupported(int tractionForce)
        {
            // 動力車だけを煙対象にする
            // Limit smoke support to powered cars
            return tractionForce > 0;
        }

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

    internal static class TrainSmokeProfile
    {
        private static readonly Vector3 SmokeAnchorLocalPosition = new(1.80f, 1.01f, 0.0f);

        public static Vector3 GetAnchorLocalPosition()
        {
            return SmokeAnchorLocalPosition;
        }
    }
}
