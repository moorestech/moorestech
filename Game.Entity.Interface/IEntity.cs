namespace Game.Entity.Interface
{
    public interface IEntity
    {
        long InstanceId { get; }
        string EntityType { get; }
        
        ServerVector3 Position { get; }
        
        string State { get; }

        void SetPosition(ServerVector3 serverVector3);
    }
}