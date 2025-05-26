#if ENABLE_COMMAND_FORGE_GENERATOR
namespace CommandForgeGenerator.Command
{
    public partial class MotionCommand : ICommandForgeCommand
    {
        public const string Type = "motion";
        public readonly CommandId CommandId;
        
        public readonly string Character;
        public readonly string MotionName;
        
  
        public static MotionCommand Create(int commandId, global::Newtonsoft.Json.Linq.JToken json)
        {
            
            var Character = (string)json["character"];
            var MotionName = (string)json["motionName"];
            
            
            return new MotionCommand(commandId, Character, MotionName);
        }
        
        public MotionCommand(int commandId, string Character, string MotionName)
        {
            CommandId = (CommandId)commandId;
            
        this.Character = Character;
        this.MotionName = MotionName;
        
        }
    }
}
#endif