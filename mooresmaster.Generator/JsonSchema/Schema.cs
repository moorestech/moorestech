using System.Collections.Generic;
using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.JsonSchema;

public interface ISchema;

public record Schema(string Id, ISchema InnerSchema)
{
    public string Id = Id;
    public ISchema InnerSchema = InnerSchema;
}

public record ObjectSchema(Dictionary<string, ISchema> Properties, string[] Required) : ISchema
{
    public Dictionary<string, ISchema> Properties = Properties;
    public string[] Required = Required;
}

public record ArraySchema(ISchema Items) : ISchema
{
    public ISchema Items = Items;
}

public record OneOfSchema(IfThenSchema[] IfThenArray) : ISchema
{
    public IfThenSchema[] IfThenArray = IfThenArray;
}

public record IfThenSchema(JsonObject If, ISchema Then)
{
    public JsonObject If = If;
    public ISchema Then = Then;
}

public record StringSchema : ISchema;

public record NumberSchema : ISchema;

public record IntegerSchema : ISchema;

public record BooleanSchema : ISchema;

public record RefSchema(string Ref) : ISchema
{
    public string Ref = Ref;
}
