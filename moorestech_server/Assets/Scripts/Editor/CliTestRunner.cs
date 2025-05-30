// Assets/Editor/CliTestRunner.cs
// コマンド例:
//   unity -batchmode -projectPath <Project> -executeMethod CliTestRunner.Run -testRegex "Inventory$" -quit
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class CliTestRunner
{
    // ──────────────────────────────────────────────────────────────────────────
    //  テスト結果コールバック
    // ──────────────────────────────────────────────────────────────────────────
    private class ResultCallbacks : ICallbacks
    {
        private readonly Regex _regex;
        private int _failCount;

        public ResultCallbacks(Regex regex) => _regex = regex;

        public void RunStarted(ITestAdaptor _) { }
        public void TestStarted(ITestAdaptor _) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.Test.IsSuite) return;           // コンテナは除外

            string name = result.Test.FullName;
            if (!_regex.IsMatch(name)) return;         // 対象テストのみ

            bool passed = result.TestStatus == TestStatus.Passed;
            string icon  = passed ? "✅" : "❌";

            if (passed)
            {
                ExportLog($"{icon} {name}");
            }
            else
            {
                _failCount++;
                ExportLog($"{icon} {name}\n    {result.Message}\n    {result.StackTrace}");
            }
        }

        public void RunFinished(ITestResultAdaptor _)
        {
            // 失敗が 1 件でもあれば 1 で終了
            EditorApplication.Exit(_failCount == 0 ? 0 : 1);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  CLI エントリポイント
    // ──────────────────────────────────────────────────────────────────────────
    public static void Run()
    {
        ExportLog("🟡 Running tests...");
        // 1) コマンドラインから正規表現パターンを取得
        string pattern = ".*";
        var   args     = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-testRegex")
            {
                pattern = args[i + 1];
                break;
            }
        }
        var regex = new Regex(pattern);

        // 2) TestRunnerApi 初期化
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        var callbacks = new ResultCallbacks(regex);
        api.RegisterCallbacks(callbacks);

        // 3) EditMode テスト一覧を取得
        api.RetrieveTestList(
            TestMode.EditMode,                       // ← 第 1 引数: テストモード
            testRoot =>                              // ← 第 2 引数: コールバック
            {
                ExportLog($"🟡 {testRoot.FullName} Collecting tests matching /{pattern}/...");
                var matched = new List<string>();
                CollectMatchedTests(testRoot, matched, regex);

                if (matched.Count == 0)
                {
                    ExportLog($"🟡 No tests matched /{pattern}/");
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
                    filters = new[] { execFilter }
                });
            });
    }

    // 再帰的にテストケース（Suite ではないノード）を収集
    private static void CollectMatchedTests(ITestAdaptor node, List<string> list, Regex regex)
    {
        if (node.IsSuite)
        {
            foreach (var child in node.Children)
                CollectMatchedTests(child, list, regex);
        }
        else if (regex.IsMatch(node.FullName))
        {
            list.Add(node.FullName);
        }
    }
    
    private static void ExportLog(string log)
    {
        Debug.Log("[CliTest]" + log);
    }
}
