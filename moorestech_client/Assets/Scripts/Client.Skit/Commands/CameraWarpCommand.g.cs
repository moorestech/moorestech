#if ENABLE_COMMAND_FORGE_GENERATOR
namespace CommandForgeGenerator.Command
{
    public partial class CameraWarpCommand : ICommandForgeCommand
    {
        public const string Type = "cameraWarp";
        public readonly CommandId CommandId;
        
        public readonly global::UnityEngine.Vector3 Position;
        public readonly global::UnityEngine.Vector3 Rotation;
        
  
        public static CameraWarpCommand Create(int commandId, global::Newtonsoft.Json.Linq.JToken json)
        {
            
            var PositionArray = json["Position"];
            var Position = new global::UnityEngine.Vector3((float)PositionArray[0], (float)PositionArray[1], (float)PositionArray[2]);
            var RotationArray = json["Rotation"];
            var Rotation = new global::UnityEngine.Vector3((float)RotationArray[0], (float)RotationArray[1], (float)RotationArray[2]);
            
            
            return new CameraWarpCommand(commandId, Position, Rotation);
        }
        
        public CameraWarpCommand(int commandId, global::UnityEngine.Vector3 Position, global::UnityEngine.Vector3 Rotation)
        {
            CommandId = (CommandId)commandId;
            
        this.Position = Position;
        this.Rotation = Rotation;
        
        }
    }
}
#endif