using System;
using UnityEngine;

namespace Client.Game.InGame.Tutorial
{
    /// <summary>
    ///     ピンの表示要求とスキット抑止の深さを合成して実表示を決める
    ///     Combines the pin's requested visibility with the skit suppression depth to decide the actual visibility
    /// </summary>
    public class TutorialWorldPinVisibility
    {
        private readonly GameObject _pinObject;
        private readonly string _ownerName;

        private bool _desiredActive;
        private int _skitSuppressDepth;
        private bool _desiredActiveInitialized;

        public TutorialWorldPinVisibility(GameObject pinObject, string ownerName)
        {
            _pinObject = pinObject;
            _ownerName = ownerName;
        }

        public void SetActive(bool active)
        {
            _desiredActive = active;
            _desiredActiveInitialized = true;
            Apply();
        }

        public void BeginSkitSuppress()
        {
            // 表示要求が未設定のまま抑止が始まると、解除時に無条件で消えてしまう
            // Starting suppression before any visibility request would blank the pin on release
            if (!_desiredActiveInitialized)
            {
                _desiredActive = _pinObject.activeSelf;
                _desiredActiveInitialized = true;
            }

            _skitSuppressDepth++;
            Apply();
        }

        public void EndSkitSuppress()
        {
            // 開始より多い解除は抑止が漏れた合図なので、0で止めず不整合として顕在化させる
            // More ends than begins signals a leaked suppression, so surface it instead of clamping at zero
            if (_skitSuppressDepth == 0)
                throw new InvalidOperationException($"[{_ownerName}] BeginSkitSuppressより多くEndSkitSuppressが呼ばれました");

            _skitSuppressDepth--;
            Apply();
        }

        private void Apply()
        {
            _pinObject.SetActive(_desiredActive && _skitSuppressDepth == 0);
        }
    }
}
