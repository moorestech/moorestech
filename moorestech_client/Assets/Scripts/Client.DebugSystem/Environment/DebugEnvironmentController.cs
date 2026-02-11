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
            // AddEnumPickerWithSaveの初期化時にこのメソッドが1回呼ばれるため、ここでイベント購読を行う
            // This method is called once during AddEnumPickerWithSave initialization, so we subscribe to the event here
            SubscribeGameInitializedEvent();

            // nullの場合はFindObjectOfTypeで取得を試みる
            // If null, try to find by FindObjectOfType
            if (_debugEnvironment == null) _debugEnvironment = UnityEngine.Object.FindObjectOfType<DebugEnvironmentObjectRoot>(true);
            if (_pureNatureEnvironment == null) _pureNatureEnvironment = UnityEngine.Object.FindObjectOfType<PureNatureEnvironmentObjectRoot>(true);
            if (_otherEnvironment == null) _otherEnvironment = UnityEngine.Object.FindObjectOfType<OtherEnvironmentObjectRoot>(true);

            // nullだった場合は環境オブジェクトが存在しないシーンなので処理を中止する
            // If any are still null, environment objects don't exist in this scene - abort silently
            if (_debugEnvironment == null || _pureNatureEnvironment == null || _otherEnvironment == null) return;

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
            }

            _debugEnvironment.gameObject.SetActive(isDebug);
            _pureNatureEnvironment.gameObject.SetActive(isPureNature);
            _otherEnvironment.gameObject.SetActive(isOther);
            
            #region Internal
            
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
    }
}
