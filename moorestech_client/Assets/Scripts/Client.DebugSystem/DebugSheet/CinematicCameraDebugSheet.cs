using System.Collections;
using Client.Common;
using Client.Game.InGame.Player;
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
                    CameraManager.RegisterCamera(_cinematicCamera);
                    
                    // プレイヤーの位置に合わせる
                    var playerObjectController = PlayerSystemContainer.Instance.PlayerObjectController;
                    var playerPosition = playerObjectController.Position;
                    // プレイヤーの回転は取得できないため、現在のカメラの回転を維持
                    _cinematicCamera.SetCameraTransform(playerPosition + new Vector3(0, 1, 0), _cinematicCamera.transform.rotation);
                    
                    playerObjectController.SetControllable(false);
                    
                    Debug.Log("Cinematic camera enabled");
                }
                else
                {
                    CameraManager.UnRegisterCamera(_cinematicCamera);
                    PlayerSystemContainer.Instance.PlayerObjectController.SetControllable(true);

                    Debug.Log("Cinematic camera disabled");
                }
            });
            
            // カメラ位置と回転のリセットボタン
            AddButton("Reset Camera Position", subText: "Reset to default position and rotation", clicked: () =>
            {
                _cinematicCamera.SetCameraTransform(Vector3.zero, Quaternion.identity);
                Debug.Log("Cinematic camera reset to default position and rotation");
            });
            
            // プレイヤー位置へ移動ボタン
            AddButton("Move to Player", subText: "Move camera to player position", clicked: () =>
            {
                var playerPosition = PlayerSystemContainer.Instance.PlayerObjectController.Position;
                _cinematicCamera.SetCameraTransform(playerPosition, _cinematicCamera.transform.rotation);
            });
            
            // カメラスピード調整
            AddSlider(0.05f, 0.01f, 0.2f, "Camera Move Speed", valueChanged: value =>
            {
                _cinematicCamera.SetCameraSpeed(value);
            });
            
            AddSlider(0.05f, 0.01f, 0.5f, "Position Move Speed", valueChanged: value =>
            {
                _cinematicCamera.SetPositionMoveSpeed(value);
            });
            
            AddSlider(1f, 0.1f, 5f, "Mouse Sensitivity", valueChanged: value =>
            {
                _cinematicCamera.SetMouseSpeed(value);
            });
            
            // FOV調整スライダー
            AddSlider(60f, 20f, 90f, "Field of View", valueChanged: value =>
            {
                _cinematicCamera.SetTargetFOV(value);
            });
            
            AddSlider(5f, 1f, 20f, "FOV Scroll Speed", valueChanged: value =>
            {
                _cinematicCamera.SetFOVScrollSpeed(value);
            });
            
            // 操作説明
            AddLabel("");
            AddLabel("Controls:");
            AddLabel("• WASD - Move horizontally");
            AddLabel("• Space/Shift - Move up/down");
            AddLabel("• Mouse - Look around");
            AddLabel("• Mouse Wheel - Zoom in/out (FOV)");
            
            yield break;
        }
    }
}