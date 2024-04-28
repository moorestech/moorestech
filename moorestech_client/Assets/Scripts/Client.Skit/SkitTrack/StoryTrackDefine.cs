namespace Client.Skit.SkitTrack
{
    public class StoryTrackDefine
    {
        public static IStoryTrack GetStoryTrack(string key)
        {
            return key switch
            {
                "Camerawork" => new CameraworkTrack(),
                "CameraWarp" => new CameraWarpTrack(),
                "Text" => new TextTrack(),
                "CharacterTransform" => new CharacterTransformTrack(),
                "CharacterMotion" => new CharacterMotionTrack(),
                "CharacterEmotion" => new CharacterEmotionTrack(),
                "WaitTime" => new WaitTimeTrack(),
                "Transition" => new TransitionTrack(),
                "SelectionTrack" => new SelectionTrack(),
                "Jump" => new JumpTrack(),
                _ => null,
            };
        }
    }
}