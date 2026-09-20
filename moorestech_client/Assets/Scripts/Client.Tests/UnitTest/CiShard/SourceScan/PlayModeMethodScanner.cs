using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine.TestTools;

namespace Client.Tests.UnitTest.CiShard.SourceScan
{
    // EnterPlayModeを生成する箇所ごとに、それを包む型とメソッドを波括弧の範囲から特定する
    // 「直前に宣言された型・メソッド」ではなく実際に包んでいるスコープで帰属させるので、後続宣言やネスト型に取り違えない
    // For every EnterPlayMode construction, identifies the enclosing type and method from brace ranges
    // Ownership follows the scopes that actually enclose the token, not the last declaration before it, so later or nested declarations cannot steal it
    public static class PlayModeMethodScanner
    {
        // nameof経由にして本ファイル自身が走査に掛からないようにする
        // Built through nameof so this file itself never matches the scan
        private static readonly string EnterPlayModeToken = $"new {nameof(EnterPlayMode)}(";

        private static readonly Regex AttributePattern = new(@"\[[^\[\]]*\]");
        private static readonly Regex PreprocessorPattern = new(@"^\s*#.*$", RegexOptions.Multiline);
        private static readonly Regex NamespacePattern = new(@"^\s*namespace\s+([\w.]+)\s*$");
        private static readonly Regex TypePattern = new(@"\b(?:class|struct|interface|record)\s+(\w+)\s*(?:<([^<>]*)>)?");
        private static readonly Regex MethodPattern = new(@"(\w+)\s*(?:<[^<>()]*>)?\s*\(");

        private enum ScopeKind
        {
            Namespace,
            Type,
            Method,
            Other,
        }

        private class SourceScope
        {
            public ScopeKind Kind { get; }
            public string Name { get; }

            public SourceScope(ScopeKind kind, string name)
            {
                Kind = kind;
                Name = name;
            }
        }

        public static void ScanFile(string path, string source, PlayModeMethodScanResult result)
        {
            // コメントと文字列を塗ってから走査し、その中の括弧やトークンを数えない
            // Scan the masked source so brackets and tokens inside comments or strings are never counted
            var masked = CSharpSourceMasker.Mask(source);
            var scopes = new List<SourceScope>();
            var fileScopedNamespace = "";
            var headerStart = 0;
            var recordedOwners = new HashSet<string>();
            for (var index = 0; index < masked.Length; index++)
            {
                if (string.CompareOrdinal(masked, index, EnterPlayModeToken, 0, EnterPlayModeToken.Length) == 0) RecordOwner(index);

                // 波括弧の直前の宣言部（ヘッダ）でスコープの種類を決める
                // The declaration text right before a brace (its header) decides the scope kind
                var current = masked[index];
                if (current == '{') scopes.Add(ClassifyScope(masked.Substring(headerStart, index - headerStart)));
                if (current == '}' && 0 < scopes.Count) scopes.RemoveAt(scopes.Count - 1);
                if (current == ';') ReadFileScopedNamespace(masked.Substring(headerStart, index - headerStart));
                if (current == '{' || current == '}' || current == ';') headerStart = index + 1;
            }

            #region Internal

            void RecordOwner(int tokenIndex)
            {
                // 最も内側の型の直下にあるメソッドが持ち主。ローカル関数やラムダの中でも外側のメンバーメソッドへ帰属する
                // The owner is the method directly under the innermost type, so local functions and lambdas resolve to their member method
                var line = masked.Take(tokenIndex).Count(character => character == '\n') + 1;
                var typeIndex = scopes.FindLastIndex(scope => scope.Kind == ScopeKind.Type);
                if (typeIndex < 0)
                {
                    result.Misreads.Add(new MisreadPlayModeToken(path, line, "token is outside any type declaration"));
                    return;
                }
                if (scopes.Count <= typeIndex + 1 || scopes[typeIndex + 1].Kind != ScopeKind.Method)
                {
                    result.Misreads.Add(new MisreadPlayModeToken(path, line, "token is not inside a method body"));
                    return;
                }

                var typeFullName = BuildTypeFullName(typeIndex);
                var methodName = scopes[typeIndex + 1].Name;
                if (recordedOwners.Add(typeFullName + "." + methodName)) result.ResolvedMethods.Add(new ResolvedPlayModeMethod(path, typeFullName, methodName));
            }

            string BuildTypeFullName(int typeIndex)
            {
                var enclosing = scopes.Take(typeIndex + 1).ToList();
                var namespaces = enclosing.Where(scope => scope.Kind == ScopeKind.Namespace).Select(scope => scope.Name).ToList();
                if (fileScopedNamespace != "") namespaces.Insert(0, fileScopedNamespace);
                var types = string.Join("+", enclosing.Where(scope => scope.Kind == ScopeKind.Type).Select(scope => scope.Name));
                return namespaces.Count == 0 ? types : string.Join(".", namespaces) + "." + types;
            }

            // 属性の括弧（[Category(...)]等）を引数括弧と取り違えず、#if行を宣言に混ぜない
            // Drop attributes so their parentheses are not taken for a parameter list, and keep #if lines out of declarations
            string StripNonDeclarationText(string header)
            {
                return AttributePattern.Replace(PreprocessorPattern.Replace(header, ""), "");
            }

            void ReadFileScopedNamespace(string header)
            {
                var match = NamespacePattern.Match(StripNonDeclarationText(header));
                if (match.Success) fileScopedNamespace = match.Groups[1].Value;
            }

            SourceScope ClassifyScope(string header)
            {
                var declaration = StripNonDeclarationText(header);

                var namespaceMatch = NamespacePattern.Match(declaration);
                if (namespaceMatch.Success) return new SourceScope(ScopeKind.Namespace, namespaceMatch.Groups[1].Value);

                // ジェネリック型はAssembly.GetTypeが引ける Name`Arity の形で持つ
                // Generic types are stored as Name`Arity, the form Assembly.GetType resolves
                var typeMatch = TypePattern.Match(declaration);
                if (typeMatch.Success)
                {
                    var typeParameters = typeMatch.Groups[2];
                    var name = typeParameters.Success ? $"{typeMatch.Groups[1].Value}`{typeParameters.Value.Split(',').Length}" : typeMatch.Groups[1].Value;
                    return new SourceScope(ScopeKind.Type, name);
                }

                // 型の直下で引数括弧を持ち、代入（フィールド初期化子のラムダ等）でないものだけをメソッドとみなす
                // Only a parameter list directly under a type, not an assignment such as a field-initializer lambda, counts as a method
                var isDirectlyUnderType = 0 < scopes.Count && scopes[^1].Kind == ScopeKind.Type;
                var methodMatch = MethodPattern.Match(declaration);
                var isAssignment = methodMatch.Success && 0 <= declaration.IndexOf('=', 0, methodMatch.Index);
                if (isDirectlyUnderType && methodMatch.Success && !isAssignment) return new SourceScope(ScopeKind.Method, methodMatch.Groups[1].Value);

                return new SourceScope(ScopeKind.Other, "");
            }

            #endregion
        }
    }
}
