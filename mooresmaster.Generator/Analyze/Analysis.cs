using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Location = mooresmaster.Generator.Json.Location;

namespace mooresmaster.Generator.Analyze;

public class Analysis
{
    private static readonly DiagnosticDescriptor DiagnosticDescriptor = new(
        "MOORES001",
        "Mooresmaster Schema Validation Error",
        "Schema YAML validation error: {0}",
        "Mooresmaster",
        DiagnosticSeverity.Error,
        true
    );
    
    public readonly List<IDiagnostics> DiagnosticsList = new();
    
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
    
    public void ReportCsDiagnostics(SourceProductionContext context)
    {
        foreach (var diagnostics in DiagnosticsList)
        {
            var locations = string.Join(", ", diagnostics.Locations.Select(l => l.ToString()));
            var message = $"{locations}: {diagnostics.Message}";
            var csDiagnostic = Diagnostic.Create(DiagnosticDescriptor, Microsoft.CodeAnalysis.Location.None, message);
            context.ReportDiagnostic(csDiagnostic);
        }
    }

    public override string ToString()
    {
        return $"{string.Join("\n\n", DiagnosticsList.Select(d => $"{string.Join(", ", d.Locations.Select(l => l.ToString()))}: {d.Message}"))}";
    }
}

public interface IDiagnostics
{
    string Message { get; }
    Location[] Locations { get; }
}

public class AnalyzeException : Exception
{
    public readonly IDiagnostics[] DiagnosticsArray;

    public AnalyzeException(IDiagnostics[] diagnosticsArray)
    {
        DiagnosticsArray = diagnosticsArray;
        var messages = new List<string>();
        foreach (var diagnostics in diagnosticsArray)
        {
            var locations = string.Join(", ", diagnostics.Locations.Select(l => l.ToString()));
            messages.Add(
                $"""
                 type: {diagnostics.GetType().Name}
                     locations: {locations}
                     {diagnostics.Message.Replace("\n", "\n    ")}
                 """
            );
        }

        Message = string.Join("\n", messages);
    }

    public override string Message { get; }
}