using System;
using Client.Game.Common;
using Common.Debug;
using UniRx;
using UnityEngine;

namespace Client.DebugSystem.Environment
{
    public static class DebugEnvironmentController
    {
        private static DebugEnvironmentObjectRoot _debugEnvironment;
        private static PureNatureEnvironmentObjectRoot _pureNatureEnvironment;
        private static OtherEnvironmentObjectRoot _otherEnvironment;

        private const string EnvironmentTypeKey = "DebugEnvironmentTypeKey";
        private static bool _isSubscribed;

        public static void SetEnvironment(DebugEnvironmentType environmentType)
        {
            TrySetEnvironment(environmentType);
        }

        public static bool TrySetEnvironment(DebugEnvironmentType environmentType)
        {
            // AddEnumPickerWithSaveの初期化時にこのメソッドが1回呼ばれるため、ここでイベント購読を行う
            // This method is called once during AddEnumPickerWithSave initialization, so we subscribe to the event here
            SubscribeGameInitializedEvent();

            // 非アクティブな環境ルートも探す
            // Search inactive environment roots too
            if (_debugEnvironment == null) _debugEnvironment = UnityEngine.Object.FindFirstObjectByType<DebugEnvironmentObjectRoot>(FindObjectsInactive.Include);
            if (_pureNatureEnvironment == null) _pureNatureEnvironment = UnityEngine.Object.FindFirstObjectByType<PureNatureEnvironmentObjectRoot>(FindObjectsInactive.Include);
            if (_otherEnvironment == null) _otherEnvironment = UnityEngine.Object.FindFirstObjectByType<OtherEnvironmentObjectRoot>(FindObjectsInactive.Include);

            // 全てnullなら環境オブジェクトが存在しないシーンなので処理を中止する
            // Abort only when every root is null, which means this scene has no environment objects
            if (_debugEnvironment == null && _pureNatureEnvironment == null && _otherEnvironment == null) return false;

            // 環境タイプに応じてアクティブ状態を切り替える
            // Switch active state based on environment type
            var isDebug = false;
            var isPureNature = false;
            var isOther = false;
            switch (environmentType)
            {
                case DebugEnvironmentType.Debug:
                    isDebug = true;
                    break;
                case DebugEnvironmentType.PureNature:
                    isPureNature = true;
                    break;
                case DebugEnvironmentType.Other:
                    isOther = true;
                    break;
                case DebugEnvironmentType.Runtime:
                    // ランタイム生成地形だけを使うため、オーサリング済み環境は全て無効のままにする
                    // Keep every authored environment disabled so only runtime-generated terrain remains
                    break;
            }

            SetRootActive(_debugEnvironment, isDebug);
            SetRootActive(_pureNatureEnvironment, isPureNature);
            SetRootActive(_otherEnvironment, isOther);
            return true;

            #region Internal

            // シーンに置かれていない環境ルートは対象外として読み飛ばす
            // Skip environment roots that are not placed in this scene
            static void SetRootActive(UnityEngine.Component environmentRoot, bool isActive)
            {
                if (environmentRoot == null) return;
                environmentRoot.gameObject.SetActive(isActive);
            }

            static void SubscribeGameInitializedEvent()
            {
                if (_isSubscribed) return;
                _isSubscribed = true;
                
                // ゲーム初期化完了時に保存済み環境設定を再適用する
                // Re-apply saved environment setting when game initialization completes
                GameInitializedEvent.OnGameInitialized.Subscribe(_ =>
                {
                    var savedValue = DebugParameters.GetValueOrDefaultInt(EnvironmentTypeKey, (int)DebugEnvironmentType.Debug);
                    SetEnvironment((DebugEnvironmentType)savedValue);
                });
            }
            
        #endregion
        }
    }

    public enum DebugEnvironmentType
    {
        Debug,
        PureNature,
        Other,
        Runtime,
    }
}
