using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using mooresmaster.Generator.Analyze.Diagnostics;
using mooresmaster.Generator.JsonSchema;

namespace mooresmaster.Generator.Analyze.Analyzers;

public class RefAnalyzer : IPostJsonSchemaLayerAnalyzer
{
    public void PostJsonSchemaLayerAnalyze(Analysis analysis, ImmutableArray<SchemaFile> schemaFiles, SchemaTable schemaTable)
    {
        // 利用可能なスキーマIDを収集
        var availableSchemaIds = schemaFiles
            .Select(sf => sf.Schema.SchemaId)
            .ToArray();
        
        var availableSchemaIdSet = new HashSet<string>(availableSchemaIds);
        
        // スキーマIDからSchemaFileへのマッピングを作成
        var schemaIdToFile = schemaFiles.ToDictionary(sf => sf.Schema.SchemaId, sf => sf);
        
        // 各RefSchemaを検証
        foreach (var kvp in schemaTable.Table)
        {
            if (kvp.Value is not RefSchema refSchema) continue;
            
            // 参照先が存在するかチェック
            if (!availableSchemaIdSet.Contains(refSchema.Ref)) analysis.ReportDiagnostics(new RefNotFoundDiagnostics(refSchema, availableSchemaIds));
        }
        
        // 循環参照をチェック
        CheckCircularReferences(analysis, schemaFiles, schemaTable, schemaIdToFile);
    }
    
    private void CheckCircularReferences(
        Analysis analysis,
        ImmutableArray<SchemaFile> schemaFiles,
        SchemaTable schemaTable,
        Dictionary<string, SchemaFile> schemaIdToFile)
    {
        // 各スキーマファイルから参照しているRefスキーマを収集
        var schemaRefDependencies = new Dictionary<string, List<RefSchema>>();
        
        foreach (var schemaFile in schemaFiles)
        {
            var schemaId = schemaFile.Schema.SchemaId;
            schemaRefDependencies[schemaId] = new List<RefSchema>();
            
            // このスキーマファイルに含まれる全てのRefSchemaを収集
            CollectRefSchemas(schemaFile.Schema.InnerSchema, schemaTable, schemaRefDependencies[schemaId]);
        }
        
        // 各スキーマから循環参照を検出
        foreach (var schemaFile in schemaFiles)
        {
            var startSchemaId = schemaFile.Schema.SchemaId;
            var visited = new HashSet<string>();
            var path = new List<string>();
            
            DetectCircularRef(
                analysis,
                startSchemaId,
                schemaRefDependencies,
                schemaIdToFile,
                visited,
                path,
                null
            );
        }
    }
    
    private void CollectRefSchemas(SchemaId schemaId, SchemaTable schemaTable, List<RefSchema> refSchemas)
    {
        if (!schemaTable.Table.TryGetValue(schemaId, out var schema)) return;
        
        switch (schema)
        {
            case RefSchema refSchema:
                refSchemas.Add(refSchema);
                break;
            case ObjectSchema objectSchema:
                foreach (var property in objectSchema.Properties.Values) CollectRefSchemas(property, schemaTable, refSchemas);
                break;
            case ArraySchema arraySchema:
                if (arraySchema.Items.IsValid) CollectRefSchemas(arraySchema.Items.Value!, schemaTable, refSchemas);
                break;
            case SwitchSchema switchSchema:
                if (switchSchema.IfThenArray.IsValid)
                    foreach (var caseSchema in switchSchema.IfThenArray.Value!)
                        CollectRefSchemas(caseSchema.Schema, schemaTable, refSchemas);
                
                break;
        }
    }
    
    private void DetectCircularRef(
        Analysis analysis,
        string currentSchemaId,
        Dictionary<string, List<RefSchema>> schemaRefDependencies,
        Dictionary<string, SchemaFile> schemaIdToFile,
        HashSet<string> visited,
        List<string> path,
        RefSchema? triggeringRef)
    {
        // 現在のパスにすでに存在する場合は循環参照
        if (path.Contains(currentSchemaId))
        {
            if (triggeringRef != null)
            {
                var circularPath = path.SkipWhile(p => p != currentSchemaId).ToList();
                circularPath.Add(currentSchemaId);
                analysis.ReportDiagnostics(new CircularRefDiagnostics(triggeringRef, circularPath.ToArray()));
            }
            
            return;
        }
        
        // すでに完全に訪問済みのノードはスキップ（循環がないことが確認済み）
        if (visited.Contains(currentSchemaId)) return;
        
        // このスキーマIDの依存関係が存在しない場合はスキップ
        if (!schemaRefDependencies.TryGetValue(currentSchemaId, out var refs)) return;
        
        path.Add(currentSchemaId);
        
        foreach (var refSchema in refs)
            // 参照先が存在する場合のみチェック
            if (schemaIdToFile.ContainsKey(refSchema.Ref))
                DetectCircularRef(
                    analysis,
                    refSchema.Ref,
                    schemaRefDependencies,
                    schemaIdToFile,
                    visited,
                    path,
                    refSchema
                );
        
        path.RemoveAt(path.Count - 1);
        visited.Add(currentSchemaId);
    }
}