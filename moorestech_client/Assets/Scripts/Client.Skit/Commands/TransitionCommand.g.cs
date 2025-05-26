#if ENABLE_COMMAND_FORGE_GENERATOR
namespace CommandForgeGenerator.Command
{
    public partial class TransitionCommand : ICommandForgeCommand
    {
        public const string Type = "transition";
        public readonly CommandId CommandId;
        
        public readonly bool Enabled;
        public readonly float Duration;
        
  
        public static TransitionCommand Create(int commandId, global::Newtonsoft.Json.Linq.JToken json)
        {
            
            var Enabled = (bool)json["enabled"];
            var Duration = (float)json["duration"];
            
            
            return new TransitionCommand(commandId, Enabled, Duration);
        }
        
        public TransitionCommand(int commandId, bool Enabled, float Duration)
        {
            CommandId = (CommandId)commandId;
            
        this.Enabled = Enabled;
        this.Duration = Duration;
        
        }
    }
}
#endif