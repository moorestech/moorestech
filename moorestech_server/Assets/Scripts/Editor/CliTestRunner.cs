// Assets/Editor/CliTestRunner.cs
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class CliTestRunner
{
    // ────────────────────────────────────────────────────────────────────────
    //  テスト結果コールバック
    // ────────────────────────────────────────────────────────────────────────
    private class ResultCallbacks : ICallbacks
    {
        private readonly Regex _regex;
        private int _failCount;

        public ResultCallbacks(Regex regex) => _regex = regex;

        public void RunStarted(ITestAdaptor _) { }
        public void TestStarted(ITestAdaptor _) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.Test.IsSuite) return;                 // Suite は除外
            string name = result.Test.FullName;
            if (!_regex.IsMatch(name)) return;               // 対象のみ

            bool   passed = result.TestStatus == TestStatus.Passed;
            string icon   = passed ? "✅" : "❌";

            if (passed)
                Export($" {icon} {name}");
            else
            {
                _failCount++;
                Export($" {icon} {name}\n    {result.Message}\n----- Log output -----\n{result.Output}\n----- Stack trace -----\n    {result.StackTrace}");
            }
        }

        public void RunFinished(ITestResultAdaptor _)
        {
            // 失敗があれば 1、無ければ 0 で Unity を終了
            EditorApplication.Exit(_failCount == 0 ? 0 : 1);
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

        // 2) TestRunnerApi 初期化
        var api       = ScriptableObject.CreateInstance<TestRunnerApi>();
        var callbacks = new ResultCallbacks(regex);
        api.RegisterCallbacks(callbacks);

        // 3) EditMode テスト一覧を取得して対象を選別
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

                var execFilter = new Filter
                {
                    testMode  = TestMode.EditMode,
                    testNames = matched.ToArray()
                };

                api.Execute(new ExecutionSettings
                {
                    filters          = new[] { execFilter },
                    runSynchronously = true        // ★ ここがポイント！
                });
            });
    }

    // 再帰的にテストケースを収集
    private static void Collect(ITestAdaptor node, List<string> list, Regex regex)
    {
        if (node.IsSuite)
            foreach (var c in node.Children) Collect(c, list, regex);
        else if (regex.IsMatch(node.FullName))
            list.Add(node.FullName);
    }

    // 全行に [CliTest] をつけて出力 
    private static void Export(string msg)
    {
        var lines = msg.Split('\n');
        foreach (var line in lines) Debug.Log($"[CliTest] {line}");
    }
}
