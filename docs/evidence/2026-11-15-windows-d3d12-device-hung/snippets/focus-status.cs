// Editor が前面にあり、Game ビューにフォーカスがあるかを返す（計測の直前に確かめる。前面に無いと PlayMode の更新が間引かれる）
// Return whether the Editor is in front and the Game view has focus (checked right before measuring; PlayMode updates are throttled otherwise)
using UnityEditor;
using UnityEngine;

var gameView = EditorWindow.focusedWindow;
return "focused=" + Application.isFocused + " focusedWindow=" + (gameView != null ? gameView.GetType().Name : "none");
