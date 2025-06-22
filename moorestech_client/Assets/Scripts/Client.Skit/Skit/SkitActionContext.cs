namespace Client.Skit.Skit
{
    public class SkitActionContext : ISkitActionContext
    {
        public bool IsAuto { get; private set; }
        public void SetAuto(bool isAuto)
        {
            IsAuto = isAuto;
        }
        public void Skip()
        {
        }
    }
}