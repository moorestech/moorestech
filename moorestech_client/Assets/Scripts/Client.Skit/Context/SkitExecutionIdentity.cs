namespace Client.Skit.Context
{
    public sealed class SkitExecutionIdentity
    {
        public readonly string SkitTitle;

        public SkitExecutionIdentity(string skitTitle)
        {
            SkitTitle = skitTitle;
        }
    }
}
