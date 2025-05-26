#if ENABLE_COMMAND_FORGE_GENERATOR
namespace CommandForgeGenerator.Command
{
    public partial class Group_endCommand : ICommandForgeCommand
    {
        public const string Type = "group_end";
        public readonly CommandId CommandId;
        
        
  
        public static Group_endCommand Create(int commandId, global::Newtonsoft.Json.Linq.JToken json)
        {
            
            
            
            return new Group_endCommand(commandId);
        }
        
        public Group_endCommand(int commandId)
        {
            CommandId = (CommandId)commandId;
            
        
        }
    }
}
#endif