using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AltoEditor
{
    public class UIDesignUtilWindow : AltoEditorWindow
    {
        [MenuItem(AltoMenuPath.DevTools + "UI Design Util")]
        static void ShowWindow()
        {
            var window = CreateInstance<UIDesignUtilWindow>();
            window.titleContent = new GUIContent("UDUtil");
            window.Show();
        }

        float _moveAmount = 1f;
        bool _showDebugGuide = true;

        //----------------------------------------------------------------------
        // Unity event hook
        //----------------------------------------------------------------------

        void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Selection.selectionChanged += OnSelectionChanged;
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Selection.selectionChanged -= OnSelectionChanged;
        }

        void OnSelectionChanged()
        {
            if (_showDebugGuide)
            {
                _lastSelectionInfo = MakeSelectionInfo();
                SceneView.RepaintAll();
            }
        }

        //----------------------------------------------------------------------
        // Data structure
        //----------------------------------------------------------------------

        class SelectionInfo
        {
            public bool isValid;
            public List<RectTransform> objs;
            public Rect bounds;
        }
        SelectionInfo _lastSelectionInfo;

        //----------------------------------------------------------------------
        // Draw GUI
        //----------------------------------------------------------------------

        Vector2 _scrollView;

        void OnGUI()
        {
            _scrollView = GUILayout.BeginScrollView(_scrollView);
            {
                DrawNudgeTool();
                DrawArrangementTool();
            }
            GUILayout.EndScrollView();
        }

        void DrawNudgeTool()
        {
            Header("Nudge : UI 要素の位置微調整", DarkOrange);

            this._defaultButtonMinWidth = 100f;
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Space(53f);
                if (Button("▲")) { Nudge(Vector2.up); }
            }
            using (new GUILayout.HorizontalScope())
            {
                if (Button("◀")) { Nudge(Vector2.left); }
                if (Button("▶")) { Nudge(Vector2.right); }
            }
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Space(53f);
                if (Button("▼")) { Nudge(Vector2.down); }
            }

            BR();
            _moveAmount = EditorGUILayout.FloatField("移動量", _moveAmount, GUILayout.MaxWidth(300f));
            BR();
        }

        void DrawArrangementTool()
        {
            Header("Arrange Tools : 配置ツール", DarkOrange);
            _showDebugGuide = GUILayout.Toggle(_showDebugGuide, "Show Selection Area Guide");

            var info = MakeSelectionInfo();
            BR();
            Header("Alignment : 位置揃え", DarkAqua, FontStyle.Normal);
            using (new GUILayout.HorizontalScope())
            {
                if (Button("｜◀◀\n｜◀　", 80f, 35f, "左揃え"      )) { Align(AlignType.Left,    info); }
                if (Button("◆◆◆\n　◆　", 80f, 35f, "水平中央揃え")) { Align(AlignType.CenterX, info); }
                if (Button("▶▶｜\n　▶｜", 80f, 35f, "右揃え"      )) { Align(AlignType.Right,   info); }
            }
            using (new GUILayout.HorizontalScope())
            {
                if (Button("−−−−\n▲▲\n　▲", 80f, 50f, "上揃え"      )) { Align(AlignType.Top,     info); }
                if (Button("−◆−◉−",           80f, 50f, "垂直中央揃え")) { Align(AlignType.CenterY, info); }
                if (Button("　▼\n▼▼\n−−−−", 80f, 50f, "下揃え"      )) { Align(AlignType.Bottom,  info); }
            }

            BR();
            Header("Distribute : 等間隔に配置", DarkAqua, FontStyle.Normal);
            if (Button("◀…▶ 水平方向に等間隔", 200f, 30f))
            {
                DistributeEvenly(DistributeType.Horizontal, info);
            }
            if (Button("▲ ⋮ ▼ 垂直方向に等間隔", 200f, 30f))
            {
                DistributeEvenly(DistributeType.Vertical, info);
            }

            BR();
            if (Button("選択 obj の先頭 2 つ\nを基準に等間隔に整列", 200f, 40f))
            {
                ArrangeByFirstTwo();
            }
            BR();

            if (!info.isValid)
            {
                EditorGUILayout.HelpBox("配置ツールは Canvas 配下の RectTransform を 2 つ以上選択時に利用できます", MessageType.Info);
            }

            _lastSelectionInfo = info;
        }

        //----------------------------------------------------------------------
        // Arrangement Logic
        //----------------------------------------------------------------------

        SelectionInfo MakeSelectionInfo()
        {
            var info = new SelectionInfo()
            {
                objs = GetSelectedRectTransforms()
            };
            info.isValid = (info.objs.Count >= 2);

            if (info.isValid)
            {
                (info.isValid, info.bounds) = GetBounds(info.objs);
            }
            return info;
        }

        bool ValidateSelectionInfo(SelectionInfo info)
        {
            if (!info.isValid)
            {
                Debug.LogWarning("配置ツールは Canvas 配下の RectTransform を 2 つ以上選択時に利用できます");
                return false;
            }
            return true;
        }

        List<RectTransform> GetSelectedRectTransforms()
        {
            return Selection.GetTransforms(SelectionMode.TopLevel | SelectionMode.ExcludePrefab | SelectionMode.Editable)
                            .Where(_ => _ is RectTransform)
                            .Select(_ => _ as RectTransform)
                            .OrderBy(_ => _.transform.GetSiblingIndex())
                            .ToList();
        }

        /// <summary>
        /// 選択中の全ての RectTransform を包括する Rect を返す
        /// </summary>
        (bool, Rect) GetBounds(List<RectTransform> objs)
        {
            bool succeeded;
            Rect bounds;
            (succeeded, bounds) = GetGlobalRect(objs[0]);
            if (!succeeded) { return (false, bounds); }

            for (int i = 1; i < objs.Count; ++i)
            {
                Rect rect;
                (succeeded, rect) = GetGlobalRect(objs[i]);
                if (!succeeded) { return (false, bounds); }

                bounds = MergeRect(bounds, rect);
            }
            return (true, bounds);
        }

        (bool, Rect) GetGlobalRect(RectTransform obj)
        {
            Vector3[] corners = new Vector3[4];
            obj.GetWorldCorners(corners);

            // 親 RectTransform のローカル座標に変換
            RectTransform parentRect = obj.parent.GetComponent<RectTransform>();
            parentRect.InverseTransformPoints(corners);

            float minX = corners[0].x;
            float maxX = corners[0].x;
            float minY = corners[0].y;
            float maxY = corners[0].y;

            for (int i = 1; i < 4; ++i)
            {
                minX = Mathf.Min(minX, corners[i].x);
                maxX = Mathf.Max(maxX, corners[i].x);
                minY = Mathf.Min(minY, corners[i].y);
                maxY = Mathf.Max(maxY, corners[i].y);
            }
            return (true, new Rect(minX, minY, maxX - minX, maxY - minY));
        }

        Rect MergeRect(Rect a, Rect b)
        {
            float minX = Mathf.Min(a.xMin, b.xMin);
            float maxX = Mathf.Max(a.xMax, b.xMax);
            float minY = Mathf.Min(a.yMin, b.yMin);
            float maxY = Mathf.Max(a.yMax, b.yMax);
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        //----------------------------------------------------------------------
        // 位置の微調整
        //----------------------------------------------------------------------

        void Nudge(Vector2 direction)
        {
            foreach (var obj in Selection.gameObjects)
            {
                var rect = obj.GetComponent<RectTransform>();
                if (rect == null) { continue; }

                Undo.RecordObject(rect, "Nudge UI Element");
                rect.anchoredPosition += direction * _moveAmount;
                EditorUtility.SetDirty(rect);
            }
        }

        //----------------------------------------------------------------------
        // 位置揃え
        //----------------------------------------------------------------------

        enum AlignType { Left, CenterX, Right, Top, CenterY, Bottom }

        void Align(AlignType alignType, SelectionInfo info)
        {
            if (!ValidateSelectionInfo(info)) { return; }

            Undo.RecordObjects(info.objs.ToArray(), "Alignment");

            foreach (var rect in info.objs)
            {
                var (succeeded, gRect) = GetGlobalRect(rect);
                if (!succeeded) { continue; }

                Vector2 pos = rect.anchoredPosition;

                switch (alignType)
                {
                    case AlignType.Left:
                        pos.x += (info.bounds.xMin - gRect.xMin);
                        break;
                    case AlignType.CenterX:
                        pos.x += (info.bounds.center.x - gRect.center.x);
                        break;
                    case AlignType.Right:
                        pos.x += (info.bounds.xMax - gRect.xMax);
                        break;
                    case AlignType.Top:
                        pos.y += (info.bounds.yMax - gRect.yMax);
                        break;
                    case AlignType.CenterY:
                        pos.y += (info.bounds.center.y - gRect.center.y);
                        break;
                    case AlignType.Bottom:
                        pos.y += (info.bounds.yMin - gRect.yMin);
                        break;
                }
                rect.anchoredPosition = pos;
            }

            EditorUtility.SetDirty(info.objs[0].gameObject);
        }

        //----------------------------------------------------------------------
        // 等間隔配置
        //----------------------------------------------------------------------

        enum DistributeType { Horizontal, Vertical }

        void DistributeEvenly(DistributeType distributeType, SelectionInfo info)
        {
            if (!ValidateSelectionInfo(info)) { return; }

            Undo.RecordObjects(info.objs.ToArray(), "Distribute Evenly");

            switch (distributeType)
            {
                case DistributeType.Horizontal:
                    DistributeEvenly_Horizontal(info);
                    break;
                case DistributeType.Vertical:
                    DistributeEvenly_Vertical(info);
                    break;
            }

            EditorUtility.SetDirty(info.objs[0].gameObject);
        }

        void DistributeEvenly_Horizontal(SelectionInfo info)
        {
            float minX = info.bounds.xMin;
            float maxX = info.bounds.xMax;
            float sumWidth = info.objs.Sum(_ =>
            {
                var (succeeded, rect) = GetGlobalRect(_);
                return rect.width;
            });
            float totalSpace = (maxX - minX) - sumWidth;
            float interval = totalSpace / (info.objs.Count - 1);

            // Hierarchy 上の並び順に、左から右に配置
            float currentX = minX;
            foreach (var obj in info.objs)
            {
                var (succeeded, rect) = GetGlobalRect(obj);
                Vector2 pos = obj.anchoredPosition;
                pos.x += (currentX - rect.xMin);
                obj.anchoredPosition = pos;

                currentX += (rect.width + interval);
            }
        }

        void DistributeEvenly_Vertical(SelectionInfo info)
        {
            float minY = info.bounds.yMin;
            float maxY = info.bounds.yMax;
            float sumHeight = info.objs.Sum(_ =>
            {
                var (succeeded, rect) = GetGlobalRect(_);
                return rect.height;
            });
            float totalSpace = (maxY - minY) - sumHeight;
            float interval = totalSpace / (info.objs.Count - 1);

            // Hierarchy 上の並び順に、上から下に配置
            float currentY = maxY;
            foreach (var obj in info.objs)
            {
                var (succeeded, rect) = GetGlobalRect(obj);
                Vector2 pos = obj.anchoredPosition;
                pos.y += (currentY - rect.yMax);
                obj.anchoredPosition = pos;

                currentY -= (rect.height + interval);
            }
        }

        /// <summary>
        /// 選択したオブジェクトを、選択したもののうち Hierarchy の index の若い 2 つの座標
        /// を基準にして等間隔に並べる。（選択順が取得できなかったのでこんな仕様）
        /// </summary>
        void ArrangeByFirstTwo()
        {
            var objs = Selection.gameObjects.OrderBy(go => go.transform.GetSiblingIndex()).ToList();
            if (objs.Count <= 2)
            {
                Debug.LogError("3 つ以上のオブジェクトを選択してください");
                return;
            }

            var first  = objs[0];
            var second = objs[1];
            var firstRect = first.GetComponent<RectTransform>();
            var secondRect = second.GetComponent<RectTransform>();
            if (firstRect == null || secondRect == null)
            {
                Debug.LogError("選択対象に RectTransform がアタッチされていません");
                return;
            }

            Vector2 diff = secondRect.anchoredPosition - firstRect.anchoredPosition;
            for (int i = 2; i < objs.Count; ++i)
            {
                var rect = objs[i].GetComponent<RectTransform>();
                if (rect == null) { continue; }

                Vector2 pos = firstRect.anchoredPosition + (i * diff);
                Undo.RecordObject(rect, "ArrangeByFirstTwo");
                rect.anchoredPosition = pos;
            }
        }

        //----------------------------------------------------------------------
        // Debug Draw
        //----------------------------------------------------------------------

        void OnSceneGUI(SceneView sceneView)
        {
            DrawBoundsDebug(_lastSelectionInfo);
        }

        void DrawBoundsDebug(SelectionInfo info)
        {
            if (!_showDebugGuide) { return; }
            if (info == null || !info.isValid) { return; }
            if (info.objs.Count == 0 || info.objs[0] == null) { return; }

            var parent = info.objs[0].parent;
            if (parent == null) { return; }

            var parentRect = parent.GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            parentRect.GetWorldCorners(corners);

            Vector3 center = (corners[0] + corners[1] + corners[2] + corners[3]) / 4;
            Vector3[] boundsCorners = new Vector3[4];
            Vector3 scale = parent.transform.lossyScale;
            boundsCorners[0] = center + new Vector3(info.bounds.x,    info.bounds.y,    0) * scale.x;
            boundsCorners[1] = center + new Vector3(info.bounds.xMax, info.bounds.y,    0) * scale.x;
            boundsCorners[2] = center + new Vector3(info.bounds.xMax, info.bounds.yMax, 0) * scale.x;
            boundsCorners[3] = center + new Vector3(info.bounds.x,    info.bounds.yMax, 0) * scale.x;

            Handles.color = Color.green;
            Handles.DrawLine(boundsCorners[0], boundsCorners[1]);
            Handles.DrawLine(boundsCorners[1], boundsCorners[2]);
            Handles.DrawLine(boundsCorners[2], boundsCorners[3]);
            Handles.DrawLine(boundsCorners[3], boundsCorners[0]);
        }
    }
}
