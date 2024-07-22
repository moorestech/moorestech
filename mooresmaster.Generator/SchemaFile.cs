using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator;

public record SchemaFile(string Path, Schema Schema)
{
    public string Path = Path;
    public Schema Schema = Schema;
}
