using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Abelia.Editor
{
    public static class HierarchyExtensionDrawer
    {
        private const float IconSize = 16.0f;

        private static readonly Color _disabledColor = new(1, 1, 1, 0.3f);
        private static readonly Color _hasMissingColor = Color.red;
        private static readonly Texture _errorIconTexture = EditorGUIUtility.IconContent("Warning").image;
        private static readonly Texture _backTexture = EditorGUIUtility.IconContent("sv_icon_dot0_pix16_gizmo").image;
        private static readonly List<Component> _components = new();
        private static readonly Dictionary<(int textureId, bool disabled, bool hasMissingProperty), (int count, Rect firstRect)> _appearanceTable = new();

        [InitializeOnLoadMethod]
        private static void RegisterCallback()
        {
            EditorApplication.hierarchyWindowItemOnGUI -= OnDrawHierarchy;
            EditorApplication.hierarchyWindowItemOnGUI += OnDrawHierarchy;
        }

        private static void OnDrawHierarchy(int instanceID, Rect selectionRect)
        {
            _appearanceTable.Clear();

            var gameObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;

            if (gameObject == null)
            {
                return;
            }

            var rowRect = new Rect(selectionRect)
            {
                xMin = 0
            };

            var isMouseHover = rowRect.Contains(Event.current.mousePosition);
            DrawLockIcon(selectionRect, gameObject, isMouseHover);
            DrawComponentIconsAndChildCount(selectionRect, gameObject);
            DrawActiveToggle(selectionRect, gameObject);
        }

        private static void DrawLockIcon(Rect selectionRect, GameObject gameObject, bool isMouseHover)
        {
            var isLocked = IsLocked(gameObject);
            if (!isMouseHover && !isLocked)
            {
                return;
            }

            var iconRect = new Rect(selectionRect)
            {
                xMin = IconSize * 2,
                width = IconSize,
                height = IconSize
            };

            using (var scope = new EditorGUI.ChangeCheckScope())
            {
                var tmpBackgroundColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1, 1, 1, isLocked ? 0.8f : 0.5f);
                isLocked = GUI.Toggle(iconRect, isLocked, GUIContent.none, "IN LockButton");
                GUI.backgroundColor = tmpBackgroundColor;

                if (scope.changed)
                {
                    SetLock(gameObject, isLocked);
                }
            }
        }

        private static bool IsLocked(Object obj)
        {
            return obj.hideFlags.HasFlagFast(HideFlags.NotEditable);
        }

        private static void SetLock(Object obj, bool isLocked)
        {
            if (isLocked)
            {
                Undo.RegisterCompleteObjectUndo(obj, "Lock");
                obj.hideFlags |= HideFlags.NotEditable;
            }
            else
            {
                Undo.RegisterCompleteObjectUndo(obj, "Unlock");
                obj.hideFlags &= ~HideFlags.NotEditable;
            }
            EditorUtility.SetDirty(obj);
        }

        private static void DrawComponentIconsAndChildCount(Rect selectionRect, GameObject gameObject)
        {
            var isPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(gameObject);
            var iconRect = new Rect(selectionRect);
            iconRect.x = iconRect.xMax - IconSize * (isPrefab ? 2 : 1);
            iconRect.width = IconSize;
            iconRect.height = IconSize;

            gameObject.GetComponents(_components);

            // 各コンポーネントのアイコンを表示
            for (var i = _components.Count - 1; i >= 0; i--)
            {
                var component = _components[i];
                if (component is Transform) { continue; }

                Texture iconTexture = AssetPreview.GetMiniThumbnail(component);

                var isComponentMissing = iconTexture == null;
                if (isComponentMissing)
                {
                    iconTexture = _errorIconTexture;
                }

                var textureId = iconTexture.GetHashCode();
                var disabled = !IsEnabled(component);
                var hasMissingProperty = !isComponentMissing && HasMissingProperty(component);
                var key = (textureId, disabled, hasMissingProperty);

                // 初めて出てきたアイコンの場合のみ表示
                if (!_appearanceTable.ContainsKey(key))
                {
                    _appearanceTable[key] = (0, new Rect(iconRect));

                    var tmpColor = GUI.color;
                    GUI.color = disabled ? _disabledColor : hasMissingProperty ? _hasMissingColor : tmpColor;
                    GUI.DrawTexture(iconRect, iconTexture, ScaleMode.ScaleToFit);
                    GUI.color = tmpColor;

                    iconRect.x -= iconRect.width;
                }
                _appearanceTable[key] = (_appearanceTable[key].count + 1, _appearanceTable[key].firstRect);
            }

            // 2つ以上あるアイコンの個数を表示
            foreach (var (count, firstRect) in _appearanceTable.Values)
            {
                if (count <= 1) { continue; }

                var backRect = new Rect(firstRect);
                backRect.xMin += 0.1875f * IconSize;
                backRect.yMin += 0.25f * IconSize;
                backRect.xMax += 0.0625f * IconSize;
                backRect.yMax += 0.125f * IconSize;

                var labelRect = new Rect(firstRect);
                labelRect.xMin += 0.3125f * IconSize;
                labelRect.yMin += 0.3125f * IconSize;

                var style = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.RoundToInt(0.5625f * IconSize),
                    normal = new GUIStyleState
                    {
                        textColor = Color.black
                    }
                };

                GUI.DrawTexture(backRect, _backTexture);
                GUI.Label(labelRect, count.ToString(), style);
            }

            DrawChildCount(iconRect, gameObject);
        }

        private static bool HasMissingProperty(Component component)
        {
            if (component == null)
            {
                return false;
            }

            try
            {
                var so = new SerializedObject(component);
                var iter = so.GetIterator();
                while (iter.NextVisible(true))
                {
                    if (iter.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (iter.objectReferenceValue == null && iter.objectReferenceInstanceIDValue != 0)
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        private static bool IsEnabled(Component component)
        {
            return component switch
            {
                Behaviour behaviour => behaviour.enabled,
                Renderer renderer => renderer.enabled,
                _ => true
            };
        }

        private static void DrawChildCount(Rect rect, GameObject gameObject)
        {
            var childCount = gameObject.GetComponentsInChildren<Transform>(includeInactive: true).Length - 1;
            if (childCount <= 1)
            {
                return;
            }

            var countRect = new Rect(rect)
            {
                height = IconSize,
                xMin = 0
            };
            var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleRight };
            GUI.Label(countRect, childCount.ToString(), style);
        }

        private static void DrawActiveToggle(Rect rect, GameObject gameObject)
        {
            var isPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(gameObject);
            var toggleRect = new Rect(rect)
            {
                xMin = rect.xMax - IconSize * (isPrefab ? 1 : 0),
                width = IconSize,
                height = IconSize
            };

            using (var scope = new EditorGUI.ChangeCheckScope())
            {
                // 切り替えトグル
                var isActive = GUI.Toggle(toggleRect, gameObject.activeSelf, GUIContent.none);
                if (scope.changed)
                {
                    Undo.RegisterCompleteObjectUndo(gameObject, "Toggle Active");
                    gameObject.SetActive(isActive);
                }
            }
        }
    }
}
