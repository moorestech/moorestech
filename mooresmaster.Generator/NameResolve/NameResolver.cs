using System;
using System.Collections.Generic;
using System.Linq;
using mooresmaster.Generator.Semantic;

namespace mooresmaster.Generator.NameResolve;

public class NameTable(Dictionary<Guid, string> names)
{
    public readonly Dictionary<string, Guid> Ids = names.ToDictionary(x => x.Value, x => x.Key);
    public readonly Dictionary<Guid, string> Names = names;
}

public static class NameResolver
{
    public static NameTable Resolve(Semantics semantics)
    {
        var names = new Dictionary<Guid, string>();

        return new NameTable(names);
    }
}
