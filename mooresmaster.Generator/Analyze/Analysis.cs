using System;
using System.Collections.Generic;
using System.Linq;
using mooresmaster.Generator.Json;

namespace mooresmaster.Generator.Analyze;

public class Analysis
{
    public List<IDiagnostics> DiagnosticsList = new();
    
    public void ReportDiagnostics(IDiagnostics analysis)
    {
        DiagnosticsList.Add(analysis);
    }
    
    /// <summary>
    ///     Diagnosticsがあった場合はthrowする
    /// </summary>
    public void ThrowDiagnostics()
    {
        if (!DiagnosticsList.Any()) return;
        throw new AnalyzeException(DiagnosticsList.ToArray());
    }
}

public interface IDiagnostics
{
    string Message { get; }
    public Location Location { get; }
}

public class AnalyzeException : Exception
{
    public readonly IDiagnostics[] DiagnosticsArray;
    
    public AnalyzeException(IDiagnostics[] diagnosticsArray)
    {
        DiagnosticsArray = diagnosticsArray;
        var messages = new List<string>();
        foreach (var diagnostics in diagnosticsArray)
            messages.Add(
                $"""
                 type: {diagnostics.GetType().Name}
                     location: {diagnostics.Location}
                     {diagnostics.Message.Replace("\n", "\n    ")}
                 """
            );
        
        Message = string.Join("\n", messages);
    }
    
    public override string Message { get; }
}
