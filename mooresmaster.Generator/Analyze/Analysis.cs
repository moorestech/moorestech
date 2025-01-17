using System;
using System.Collections.Generic;
using System.Linq;

namespace mooresmaster.Generator.Analyze;

public class Analysis
{
    public List<IDiagnostics> DiagnosticsList = new();
    
    public void ReportDiagnostics(IDiagnostics analysis)
    {
        DiagnosticsList.Add(analysis);
    }
    
    public void ThrowDiagnostics()
    {
        if (!DiagnosticsList.Any()) return;
        
        var messages = new List<string>();
        foreach (var diagnostics in DiagnosticsList) messages.Add($"type: {diagnostics.GetType().Name}\n    {diagnostics.Message.Replace("\n", "\n    ")}");
        
        var message = string.Join("\n", messages);
        throw new Exception(message);
    }
}

public interface IDiagnostics
{
    string Message { get; }
}
