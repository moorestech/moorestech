using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using mooresmaster.Generator.Json;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator.Analyze.Analyzers;

public class SwitchPathAnalyzer : IPostJsonSchemaLayerAnalyzer
{
    public void PostJsonSchemaLayerAnalyze(Analysis analysis, ImmutableArray<SchemaFile> schemaFiles, SchemaTable schemaTable)
    {
        // 各スキーマファイルのルートSchemaIdをセットに追加
        var rootSchemaIds = new HashSet<SchemaId>();
        foreach (var schemaFile in schemaFiles)
        {
            rootSchemaIds.Add(schemaFile.Schema.InnerSchema);
        }

        foreach (var kvp in schemaTable.Table)
        {
            var schema = kvp.Value;
            if (!(schema is SwitchSchema switchSchema)) continue;
            if (switchSchema.IfThenArray.Length == 0) continue;

            var switchPath = switchSchema.IfThenArray[0].SwitchReferencePath;

            // このSwitchSchemaのルートを親を辿って見つける
            var rootSchemaId = FindRootSchemaId(switchSchema, schemaTable, rootSchemaIds);
            if (rootSchemaId == null) continue;

            ValidateSwitchPath(analysis, schemaTable, switchSchema, switchPath, rootSchemaId.Value);
        }
    }

    private SchemaId? FindRootSchemaId(ISchema schema, SchemaTable schemaTable, HashSet<SchemaId> rootSchemaIds)
    {
        var current = schema;
        while (current.Parent.HasValue)
        {
            var parentId = current.Parent.Value;
            if (rootSchemaIds.Contains(parentId))
            {
                return parentId;
            }
            current = schemaTable.Table[parentId];
        }

        // currentがルートかどうかをチェック
        foreach (var kvp in schemaTable.Table)
        {
            if (kvp.Value == current && rootSchemaIds.Contains(kvp.Key))
            {
                return kvp.Key;
            }
        }

        return null;
    }

    private void ValidateSwitchPath(Analysis analysis, SchemaTable schemaTable, SwitchSchema switchSchema, SwitchPath switchPath, SchemaId rootSchemaId)
    {
        // Switchの親ObjectSchemaを取得
        if (!switchSchema.Parent.HasValue) return;

        var parentSchema = schemaTable.Table[switchSchema.Parent.Value];
        if (!(parentSchema is ObjectSchema parentObjectSchema)) return;

        SchemaId currentSchemaId;
        ISchema currentSchema;

        switch (switchPath.Type)
        {
            case SwitchPathType.Absolute:
                // 絶対パス: ルートから辿る
                currentSchemaId = rootSchemaId;
                currentSchema = schemaTable.Table[rootSchemaId];
                break;
            case SwitchPathType.Relative:
                // 相対パス: 親ObjectSchemaから開始
                currentSchemaId = switchSchema.Parent.Value;
                currentSchema = parentObjectSchema;
                break;
            default:
                return;
        }

        foreach (var element in switchPath.Elements)
        {
            if (element is ParentSwitchPathElement)
            {
                // 親に遡る
                if (currentSchema.Parent == null)
                {
                    analysis.ReportDiagnostics(new SwitchPathCannotNavigateToParentDiagnostics(
                        switchPath,
                        switchSchema.SwitchPathLocation
                    ));
                    return;
                }
                currentSchemaId = currentSchema.Parent.Value;
                currentSchema = schemaTable.Table[currentSchemaId];
            }
            else if (element is NormalSwitchPathElement normalElement)
            {
                // プロパティ名で辿る
                if (!(currentSchema is ObjectSchema objectSchema))
                {
                    analysis.ReportDiagnostics(new SwitchPathNotAnObjectDiagnostics(
                        switchPath,
                        normalElement.Path,
                        switchSchema.SwitchPathLocation
                    ));
                    return;
                }

                if (!objectSchema.Properties.ContainsKey(normalElement.Path))
                {
                    analysis.ReportDiagnostics(new SwitchPathPropertyNotFoundDiagnostics(
                        switchPath,
                        normalElement.Path,
                        objectSchema.Properties.Keys.ToArray(),
                        switchSchema.SwitchPathLocation
                    ));
                    return;
                }

                currentSchemaId = objectSchema.Properties[normalElement.Path];
                currentSchema = schemaTable.Table[currentSchemaId];
            }
        }
    }

    public static string FormatSwitchPath(SwitchPath path)
    {
        var prefix = path.Type == SwitchPathType.Absolute ? "/" : "./";
        var elements = string.Join("/", path.Elements.Select(e =>
        {
            if (e is NormalSwitchPathElement normal) return normal.Path;
            if (e is ParentSwitchPathElement) return "..";
            return "?";
        }));
        return prefix + elements;
    }
}

public class SwitchPathPropertyNotFoundDiagnostics : IDiagnostics
{
    public SwitchPathPropertyNotFoundDiagnostics(SwitchPath switchPath, string propertyName, string[] availableProperties, Location location)
    {
        SwitchPath = switchPath;
        PropertyName = propertyName;
        AvailableProperties = availableProperties;
        Location = location;
    }

    public SwitchPath SwitchPath { get; }
    public string PropertyName { get; }
    public string[] AvailableProperties { get; }
    public Location Location { get; }

    public string Message => $"Invalid switch path '{SwitchPathAnalyzer.FormatSwitchPath(SwitchPath)}'. Property '{PropertyName}' not found. Available properties: [{string.Join(", ", AvailableProperties)}]";
}

public class SwitchPathNotAnObjectDiagnostics : IDiagnostics
{
    public SwitchPathNotAnObjectDiagnostics(SwitchPath switchPath, string propertyName, Location location)
    {
        SwitchPath = switchPath;
        PropertyName = propertyName;
        Location = location;
    }

    public SwitchPath SwitchPath { get; }
    public string PropertyName { get; }
    public Location Location { get; }

    public string Message => $"Invalid switch path '{SwitchPathAnalyzer.FormatSwitchPath(SwitchPath)}'. Cannot access property '{PropertyName}': current schema is not an object";
}

public class SwitchPathCannotNavigateToParentDiagnostics : IDiagnostics
{
    public SwitchPathCannotNavigateToParentDiagnostics(SwitchPath switchPath, Location location)
    {
        SwitchPath = switchPath;
        Location = location;
    }

    public SwitchPath SwitchPath { get; }
    public Location Location { get; }

    public string Message => $"Invalid switch path '{SwitchPathAnalyzer.FormatSwitchPath(SwitchPath)}'. Cannot navigate to parent: already at root";
}
