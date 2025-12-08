namespace Core.Master
{
    public interface IMasterValidator
    {
        public bool Validate(out string errorLogs);
        public void Initialize();
    }
}