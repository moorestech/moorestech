#if ENABLE_COMMAND_FORGE_GENERATOR
namespace CommandForgeGenerator.Command
{
    public partial class CameraworkCommand : ICommandForgeCommand
    {
        public const string Type = "camerawork";
        public readonly CommandId CommandId;
        
        public readonly float Duration;
        public readonly string Easing;
        public readonly global::UnityEngine.Vector3 StartPosition;
        public readonly global::UnityEngine.Vector3 StartRotation;
        public readonly global::UnityEngine.Vector3 EndPosition;
        public readonly global::UnityEngine.Vector3 EndRotation;
        
  
        public static CameraworkCommand Create(int commandId, global::Newtonsoft.Json.Linq.JToken json)
        {
            
            var Duration = (float)json["duration"];
            var Easing = (string)json["easing"];
            var StartPositionArray = json["StartPosition"];
            var StartPosition = new global::UnityEngine.Vector3((float)StartPositionArray[0], (float)StartPositionArray[1], (float)StartPositionArray[2]);
            var StartRotationArray = json["StartRotation"];
            var StartRotation = new global::UnityEngine.Vector3((float)StartRotationArray[0], (float)StartRotationArray[1], (float)StartRotationArray[2]);
            var EndPositionArray = json["EndPosition"];
            var EndPosition = new global::UnityEngine.Vector3((float)EndPositionArray[0], (float)EndPositionArray[1], (float)EndPositionArray[2]);
            var EndRotationArray = json["EndRotation"];
            var EndRotation = new global::UnityEngine.Vector3((float)EndRotationArray[0], (float)EndRotationArray[1], (float)EndRotationArray[2]);
            
            
            return new CameraworkCommand(commandId, Duration, Easing, StartPosition, StartRotation, EndPosition, EndRotation);
        }
        
        public CameraworkCommand(int commandId, float Duration, string Easing, global::UnityEngine.Vector3 StartPosition, global::UnityEngine.Vector3 StartRotation, global::UnityEngine.Vector3 EndPosition, global::UnityEngine.Vector3 EndRotation)
        {
            CommandId = (CommandId)commandId;
            
        this.Duration = Duration;
        this.Easing = Easing;
        this.StartPosition = StartPosition;
        this.StartRotation = StartRotation;
        this.EndPosition = EndPosition;
        this.EndRotation = EndRotation;
        
        }
    }
}
#endif