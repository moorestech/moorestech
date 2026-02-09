using UnityEngine;

namespace Client.DebugSystem.Environment
{
    public static class DebugEnvironmentController
    {
        private static DebugEnvironmentObjectRoot _debugEnvironment;
        private static PureNatureEnvironmentObjectRoot _pureNatureEnvironment;
        private static OtherEnvironmentObjectRoot _otherEnvironment;

        public static void SetEnvironment(DebugEnvironmentType environmentType)
        {
            // nullの場合はFindObjectOfTypeで取得を試みる
            // If null, try to find by FindObjectOfType
            if (_debugEnvironment == null) _debugEnvironment = Object.FindObjectOfType<DebugEnvironmentObjectRoot>(true);
            if (_pureNatureEnvironment == null) _pureNatureEnvironment = Object.FindObjectOfType<PureNatureEnvironmentObjectRoot>(true);
            if (_otherEnvironment == null) _otherEnvironment = Object.FindObjectOfType<OtherEnvironmentObjectRoot>(true);

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
        }
    }

    public enum DebugEnvironmentType
    {
        Debug,
        PureNature,
        Other,
    }
}
