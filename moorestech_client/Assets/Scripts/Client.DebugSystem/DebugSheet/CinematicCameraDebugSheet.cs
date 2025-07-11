using System.Collections;
using Client.Common;
using UnityDebugSheet.Runtime.Core.Scripts;
using UnityEngine;

namespace Client.DebugSystem
{
    public class CinematicCameraDebugSheet : DefaultDebugPageBase
    {
        protected override string Title => "Cinematic Camera";
        
        private CinematicCameraController _cinematicCamera;
        private bool _isCinematicCameraActive;
        
        public override IEnumerator Initialize()
        {
            // シネマティックカメラを探す
            _cinematicCamera = Object.FindObjectOfType<CinematicCameraController>(true);
            if (_cinematicCamera == null)
            {
                AddLabel("CinematicCameraController not found in scene");
                yield break;
            }
            
            // トグルスイッチを追加
            AddSwitch(_isCinematicCameraActive, "Enable Cinematic Camera", valueChanged: isActive =>
            {
                _isCinematicCameraActive = isActive;
                
                if (_isCinematicCameraActive)
                {
                    CameraManager.Instance.RegisterCamera(_cinematicCamera);
                    Debug.Log("Cinematic camera enabled");
                }
                else
                {
                    CameraManager.Instance.UnRegisterCamera(_cinematicCamera);
                    Debug.Log("Cinematic camera disabled");
                }
            });
            
            yield break;
        }
    }
}