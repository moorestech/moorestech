using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public class FindMissingScriptsUnder : EditorWindow
{
    [Serializable]
    private struct ResultItem
    {
        public GameObject go;
        public int componentIndex; // そのGameObject上のMissingのスロット番号
        public string pathFromRoot; // ルートからの階層パス
    }

    [SerializeField] private GameObject _root;
    [SerializeField] private List<ResultItem> _results = new List<ResultItem>();
    [SerializeField] private int _scannedCount;
    [SerializeField] private Vector2 _scroll;

    private const string WindowTitle = "Find Missing Scripts Under";

    [MenuItem("moorestech/" + WindowTitle)]
    public static void Open()
    {
        var win = GetWindow<FindMissingScriptsUnder>(false, WindowTitle, true);
        win.Show();
    }

    // ヒエラルキーの右クリック / GameObject メニューから選択中を Root にして起動
    [MenuItem("GameObject/" + WindowTitle, false, 49)]
    private static void OpenFromSelection(MenuCommand cmd)
    {
        var win = GetWindow<FindMissingScriptsUnder>(false, WindowTitle, true);
        win._root = Selection.activeGameObject;
        win._results.Clear();
        win._scannedCount = 0;
        win.Show();
    }

    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Root GameObject", GUILayout.Width(120));
            _root = (GameObject)EditorGUILayout.ObjectField(_root, typeof(GameObject), true);
        }

        EditorGUILayout.Space(4);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Scan", GUILayout.Height(24)))
            {
                Scan();
            }

            using (new EditorGUI.DisabledScope(_results.Count == 0))
            {
                if (GUILayout.Button("Copy report to clipboard", GUILayout.Height(24)))
                {
                    EditorGUIUtility.systemCopyBuffer = BuildReportText();
                    ShowNotification(new GUIContent("Copied report"));
                }
            }

            GUILayout.FlexibleSpace();
        }

        EditorGUILayout.Space(6);

        // 概要
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Scanned Objects: {_scannedCount}");
            EditorGUILayout.LabelField($"Missing Scripts Found: {_results.Count}");
        }

        EditorGUILayout.Space(6);

        DrawResultList();
    }

    private void DrawResultList()
    {
        EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);

        using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
        {
            _scroll = scroll.scrollPosition;

            if (_results.Count == 0)
            {
                EditorGUILayout.HelpBox("No Missing (Script) found.（未検出）", MessageType.Info);
                return;
            }

            for (int i = 0; i < _results.Count; i++)
            {
                var r = _results[i];

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField(r.go, typeof(GameObject), true);
                        if (GUILayout.Button("Select", GUILayout.Width(70)))
                        {
                            Selection.activeObject = r.go;
                        }
                        if (GUILayout.Button("Ping", GUILayout.Width(60)))
                        {
                            EditorGUIUtility.PingObject(r.go);
                        }
                    }

                    EditorGUILayout.LabelField($"Component Slot Index: {r.componentIndex}");
                    EditorGUILayout.LabelField("Path", EditorStyles.boldLabel);
                    EditorGUILayout.SelectableLabel(r.pathFromRoot, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Copy Path", GUILayout.Width(90)))
                        {
                            EditorGUIUtility.systemCopyBuffer = r.pathFromRoot;
                            ShowNotification(new GUIContent("Copied path"));
                        }
                    }
                }
            }
        }
    }

    private void Scan()
    {
        _results.Clear();
        _scannedCount = 0;

        if (_root == null)
        {
            EditorUtility.DisplayDialog(WindowTitle, "Root GameObject を指定してください。", "OK");
            return;
        }

        try
        {
            var rootTr = _root.transform;
            var all = rootTr.GetComponentsInChildren<Transform>(true); // 非アクティブ含む
            _scannedCount = all.Length;

            for (int tIdx = 0; tIdx < all.Length; tIdx++)
            {
                var tr = all[tIdx];
                var comps = tr.gameObject.GetComponents<Component>();
                for (int cIdx = 0; cIdx < comps.Length; cIdx++)
                {
                    if (comps[cIdx] == null)
                    {
                        _results.Add(new ResultItem
                        {
                            go = tr.gameObject,
                            componentIndex = cIdx,
                            pathFromRoot = BuildHierarchyPath(_root.transform, tr)
                        });
                    }
                }
            }

            Repaint();

            if (_results.Count == 0)
            {
                ShowNotification(new GUIContent("No Missing (Script)"));
            }
            else
            {
                ShowNotification(new GUIContent($"Found: {_results.Count}"));
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog(WindowTitle, "スキャン中に例外が発生しました。Console を確認してください。", "OK");
        }
    }

    private static string BuildHierarchyPath(Transform root, Transform target)
    {
        // root から target までのパス（root含む）を "Root/Child/GrandChild" 形式で返す
        var stack = new Stack<string>();
        var cur = target;
        while (cur != null)
        {
            stack.Push(cur.name);
            if (cur == root) break;
            cur = cur.parent;
        }

        var sb = new StringBuilder(128);
        bool first = true;
        foreach (var seg in stack)
        {
            if (!first) sb.Append('/');
            sb.Append(seg);
            first = false;
        }
        return sb.ToString();
    }

    private string BuildReportText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{WindowTitle}] {DateTime.Now}");
        sb.AppendLine($"Root: {_root?.name}");
        sb.AppendLine($"Scanned Objects: {_scannedCount}");
        sb.AppendLine($"Missing Scripts Found: {_results.Count}");
        sb.AppendLine();

        for (int i = 0; i < _results.Count; i++)
        {
            var r = _results[i];
            sb.AppendLine($"{i + 1}. {r.pathFromRoot}  (Component Slot Index: {r.componentIndex})");
        }

        return sb.ToString();
    }
}
