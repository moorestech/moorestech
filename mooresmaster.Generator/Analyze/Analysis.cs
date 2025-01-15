using System;
using System.Collections.Generic;

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
        foreach (var diagnostics in DiagnosticsList) throw new Exception(diagnostics.GetType().Name);
    }
}

public interface IDiagnostics;
