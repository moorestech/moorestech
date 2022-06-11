namespace Game.Entity.Interface
{
    public interface IEntitiesDatastore
    {
        public void Add(IEntity entity);
        public bool Exists(long instanceId);
        
        public void SetPosition(long instanceId,ServerVector3 position);

        public ServerVector3 GetPosition(long instanceId);
    }
}