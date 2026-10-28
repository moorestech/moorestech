using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var failures = 0;
var files = File.ReadAllLines(args[0]);
foreach (var path in files)
{
    var kind = path.Contains("/scenarios/") ? SourceCodeKind.Script : SourceCodeKind.Regular;
    var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), new CSharpParseOptions(LanguageVersion.Preview, kind: kind), path);
    foreach (var diagnostic in tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
    {
        Console.WriteLine(diagnostic);
        failures++;
    }
    if (kind == SourceCodeKind.Script)
    {
        foreach (var call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
                     .Where(call => call.Expression.ToString().StartsWith("ServerContext.GetService<")))
        {
            if (call.Ancestors().OfType<InvocationExpressionSyntax>().Any(parent => parent.Expression.ToString().EndsWith(".RunOnServer"))) continue;
            Console.WriteLine($"Service access outside tick command: {path}:{tree.GetLineSpan(call.Span).StartLinePosition.Line + 1}");
            failures++;
        }
    }
    foreach (var expression in tree.GetRoot().DescendantNodes().OfType<AwaitExpressionSyntax>())
    {
        var lambda = expression.Ancestors().OfType<LambdaExpressionSyntax>().FirstOrDefault();
        if (lambda != null && lambda.AsyncKeyword.IsKind(SyntaxKind.None))
        {
            Console.WriteLine($"Await inside synchronous lambda: {path}:{tree.GetLineSpan(expression.Span).StartLinePosition.Line + 1}");
            failures++;
        }
    }
}
Console.WriteLine($"Parsed {files.Length} files; failures={failures}");
return failures == 0 ? 0 : 1;
