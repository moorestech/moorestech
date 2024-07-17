namespace mooresmaster
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class MoorestechJsonSchemaAttribute : Attribute
    {
        public readonly string Path;
        
        public MoorestechJsonSchemaAttribute(string path)
        {
            Path = path;
        }
    }
}
