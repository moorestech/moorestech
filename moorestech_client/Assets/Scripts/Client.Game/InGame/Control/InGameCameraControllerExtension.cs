using Client.Game.InGame.UI.UIState.Input;

namespace Client.Game.InGame.Control
{
    public static class TweenCameraInfoExtension
    {
        public const float TopDownTargetCameraDistance = 9;
        
        public static TweenCameraInfo CreateTopDownTweenCameraInfo(this InGameCameraController self)
        {
            var currentRotation = self.CameraEulerAngle;
            var targetCameraRotation = currentRotation;
            
            targetCameraRotation.x = 70f;
            targetCameraRotation.y = currentRotation.y switch
            {
                var y when y < 45 => 0,
                var y when y < 135 => 90,
                var y when y < 225 => 180,
                var y when y < 315 => 270,
                _ => 0
            };
            
            return new TweenCameraInfo(targetCameraRotation, TopDownTargetCameraDistance);
        }
        
        public static TweenCameraInfo CreateCurrentCameraTweenCameraInfo(this InGameCameraController self)
        {
            return new TweenCameraInfo(self.CameraEulerAngle, self.CameraDistance);
        }
    }
}