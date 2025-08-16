// Assets/Editor/CliTestRunner.cs
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
// ★修正: NUnit/UnityEngine.TestRunner の ITestRunCallback を実装するために追加
using NUnit.Framework.Interfaces;
using UnityEngine.TestRunner;
using TestStatus = NUnit.Framework.Interfaces.TestStatus;

// ────────────────────────────────────────────────────────────────────────
//  永続コールバック（ドメインリロード後も自動検出される）
// ────────────────────────────────────────────────────────────────────────

[assembly:TestRunCallback(typeof(CliPersistentCallbacks))]
public class CliPersistentCallbacks : ITestRunCallback // ★修正: UnityEngine.TestRunner.ITestRunCallback を実装
{
    // ★追加: EditorPrefs フラグをチェック
    private static bool IsEnabled() => EditorPrefs.GetBool(CliTestRunner.PrefKey, false);

    // ★修正: シグネチャを ITest/ITestResult に変更
    public void RunStarted(ITest testsToRun) { }
    public void TestStarted(ITest test) { }

    public void TestFinished(ITestResult result)
    {
        // ★追加: フラグ未設定なら何もしない
        if (!IsEnabled()) return;

        if (result.Test.IsSuite) return;

        bool   passed = result.ResultState.Status == TestStatus.Passed; // ★修正
        string icon   = passed ? "✅" : "❌";
        string name = result.Test.FullName;
        
        if (passed)
            CliTestRunner.Export($" {icon} {name}");
        else
        {
            CliTestRunner.Export($" {icon} {name}\n    {result.Message}\n----- Log output -----\n{result.Output}\n----- Stack trace -----\n    {result.StackTrace}");
        }
    }

    public void RunFinished(ITestResult result)
    {
        // ★追加: フラグ未設定なら何もしない
        if (!IsEnabled()) return;

        // 失敗があれば 1、無ければ 0 で Unity を終了
        EditorApplication.Exit(result.FailCount == 0 ? 0 : 1);
    }
}

public static class CliTestRunner
{
    // ★追加: EditorPrefs 用のキー
    internal const string PrefKey = "CliPersistentCallbacks.Enabled";

    // ────────────────────────────────────────────────────────────────────────
    //  コンパイルエラー監視（既存のまま）
    // ────────────────────────────────────────────────────────────────────────
    private static int _compileErrors = 0;

    [InitializeOnLoadMethod] // ドメインロード時に 1 度だけ登録
    private static void RegisterCompileCallback()
    {
        CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;

        // ★追加: ドメインロード時にフラグを下げる（無効化）
        EditorPrefs.SetBool(PrefKey, false);
    }

    private static void OnAssemblyCompiled(string path, CompilerMessage[] msgs)
    {
        foreach (var m in msgs)
        {
            if (m.type != CompilerMessageType.Error) continue;

            _compileErrors++;
            Export($" ❌ Compile error in {System.IO.Path.GetFileName(path)}\n" +
                   $"    {m.message.Trim()}\n" +
                   $"    {m.file}:{m.line}");
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    //  CLI エントリポイント
    // ────────────────────────────────────────────────────────────────────────
    public static void Run()
    {
        // 1) 正規表現パラメータ取得
        string pattern = ".*";
        var    args    = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-testRegex") { pattern = args[i + 1]; break; }

        var regex = new Regex(pattern);

        // 2) TestRunnerApi 初期化（RegisterCallbacks は不要）
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();

        // 3) EditMode テスト一覧を取得して対象を選別（ゼロ件検出のため継続利用）
        api.RetrieveTestList(
            TestMode.EditMode,
            root =>
            {
                var matched = new List<string>();
                Collect(root, matched, regex);

                if (matched.Count == 0)
                {
                    Export($" 🟡 No tests matched /{pattern}/");
                    EditorApplication.Exit(0);
                    return;
                }

                // ★修正: 実行フィルタは groupNames ではなく testNames を使用
                var execFilter = new Filter
                {
                    testMode  = TestMode.EditMode,
                    testNames = matched.ToArray()
                };

                // ★追加: 実行直前にフラグを立てる（有効化）
                EditorPrefs.SetBool(PrefKey, true);

                api.Execute(new ExecutionSettings
                {
                    filters          = new[] { execFilter },
                    runSynchronously = false
                });
            });
    }

    // 再帰的にテストケースを収集（ゼロ件検出用）
    private static void Collect(ITestAdaptor node, List<string> list, Regex regex)
    {
        if (node.IsSuite)
        {
            foreach (var c in node.Children) Collect(c, list, regex);
        }
        else if (regex.IsMatch(node.FullName))
        {
            list.Add(node.FullName);
        }
    }

    // [CliTest]がついている行のみ実際のコンソールに出力されるので、全行に [CliTest] をつけて出力
    internal static void Export(string msg) // 永続コールバックから呼ぶため internal
    {
        var lines = msg.Split('\n');
        foreach (var line in lines) Debug.Log($"[CliTest] {line}");
    }
}
