#if ENABLE_COMMAND_FORGE_GENERATOR
namespace CommandForgeGenerator.Command
{
    public partial class Group_startCommand : ICommandForgeCommand
    {
        public const string Type = "group_start";
        public readonly CommandId CommandId;
        
        public readonly string GroupName;
        public readonly bool IsCollapsed;
        
  
        public static Group_startCommand Create(int commandId, global::Newtonsoft.Json.Linq.JToken json)
        {
            
            var GroupName = (string)json["groupName"];
            var IsCollapsed = (bool)json["isCollapsed"];
            
            
            return new Group_startCommand(commandId, GroupName, IsCollapsed);
        }
        
        public Group_startCommand(int commandId, string GroupName, bool IsCollapsed)
        {
            CommandId = (CommandId)commandId;
            
        this.GroupName = GroupName;
        this.IsCollapsed = IsCollapsed;
        
        }
    }
}
#endif