namespace Client.Skit.Skit
{
    public interface ISkitActionContext
    {
        public bool IsAuto { get; }
        public void SetAuto(bool isAuto);
        
        public void Skip();
    }
}