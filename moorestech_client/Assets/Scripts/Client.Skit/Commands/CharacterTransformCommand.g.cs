#if ENABLE_COMMAND_FORGE_GENERATOR
namespace CommandForgeGenerator.Command
{
    public partial class CharacterTransformCommand : ICommandForgeCommand
    {
        public const string Type = "characterTransform";
        public readonly CommandId CommandId;
        
        public readonly string Character;
        public readonly global::UnityEngine.Vector3 Position;
        public readonly global::UnityEngine.Vector3 Rotation;
        
  
        public static CharacterTransformCommand Create(int commandId, global::Newtonsoft.Json.Linq.JToken json)
        {
            
            var Character = (string)json["character"];
            var PositionArray = json["Position"];
            var Position = new global::UnityEngine.Vector3((float)PositionArray[0], (float)PositionArray[1], (float)PositionArray[2]);
            var RotationArray = json["Rotation"];
            var Rotation = new global::UnityEngine.Vector3((float)RotationArray[0], (float)RotationArray[1], (float)RotationArray[2]);
            
            
            return new CharacterTransformCommand(commandId, Character, Position, Rotation);
        }
        
        public CharacterTransformCommand(int commandId, string Character, global::UnityEngine.Vector3 Position, global::UnityEngine.Vector3 Rotation)
        {
            CommandId = (CommandId)commandId;
            
        this.Character = Character;
        this.Position = Position;
        this.Rotation = Rotation;
        
        }
    }
}
#endif