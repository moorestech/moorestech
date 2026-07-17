# ユースケース: キー・マウス・uGUIを注入する / 入力が効かない

## 鉄則: 世界は2つあり両立しない。必ず(A)だけで通す

- **(A) InputSystem直接注入** — `InputSystem.QueueStateEvent(...)`。フォーカス不要・決定論的。
  DSLでは `Client.Playtest/Input/SemanticInput.cs` がこれを抽象化している（**第一選択**）
- **(B) OS入力** — `uloop simulate-keyboard` / `simulate-mouse-input` は**禁止**。
  Editorを前面化させ、実OSマウス状態が毎フレーム`Mouse.current`にforwardされて(A)の注入を上書き無効化する。
  一度汚染するとPlayMode再起動でしか戻らない（ESC1回のsimulateで以降全注入が死んだ実績あり）

さらに: **スニペットから`InputSystem.Update()`を呼ばない**。editor-update文脈になり
`WasPressedThisFrame`/`IsPressed`が発火しなくなる。queueだけして通常フレーム更新に処理させる。
「queue → フレームを進める（await/シェルsleep）→ 読む」が唯一機能する形。

## DSL内での注入（SemanticInput — シナリオからはDriver経由で使う）

| Driver API | 中身 |
|---|---|
| `PressKey(Key key)` | KeyDown→2フレーム→KeyUp（押下と解放を別フレームに分離。GetKeyDown/Up両方を確実に発火） |
| `SelectHotbar(slot)` | `Key.Digit1 + slot` のタップ（0始まり） |
| `AimAt(worldPos)` | `Camera.main.WorldToScreenPoint`→`MouseMoveTo`（**delta=0で注入**しカメラLook入力へ波及させない）→3フレーム待ち |
| `ClickPlace()` | 左ボタン押下→2フレーム→解放（設置はGetKeyUpで確定するため解放必須） |

低レベルには `SemanticInput.KeyDown/KeyUp/MouseMoveTo/MouseButtonDown/MouseButtonUp/Click/TapKey/EnsureDevices` がある。
KeyboardStateは**全量スナップショット**なので押下中キー集合を毎回詰め直す実装になっている（部分更新不可）。

## 注入が効かないとき最初に疑うこと: legacy Input直読み

`UnityEngine.Input.mousePosition` / `Input.GetKeyDown` 直読みのコードは**QueueStateEvent注入で駆動できない**。
主要経路（設置プレビューのレイキャスト・UIState遷移キーB/T/R/Tab/F3・右クリックカメラ・Q/E高さ・LeftShift回転）は
`Client.Input.HybridInput`（InputSystem優先＋legacyフォールバック）へ移行済み。

- 対象機能が反応しないときは、その機能の入力読み取りを**grepしてlegacy直読みか確認**:
  `grep -rn "UnityEngine.Input" --include="*.cs" <対象ディレクトリ>`
- legacyだったら `HybridInput.GetKeyDown(KeyCode.X)` / `HybridInput.GetMousePosition()` 等へ最小差分で移行する
  （プロダクト修正。操作感は変わらない。移行例はコミット a873f4d57）
- 移行できない事情がある場合のみ、リフレクションで状態遷移を直接叩く代替を検討

## uGUIボタン・スロットのクリック

2通りあり、**EventSystem直叩きが第一選択**（OSカーソル非依存・カメラ非干渉・レイアウト非依存）:

```csharp
var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
    { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
UnityEngine.EventSystems.ExecuteEvents.Execute(targetGo, eventData, UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
UnityEngine.EventSystems.ExecuteEvents.Execute(targetGo, eventData, UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
```

- ハンドラは`CommonSlotView`等の**コンポーネントが付いたGameObject**に対して実行する（親ではなく）
- 座標クリックで押す場合はEventSystemが`InputSystemUIInputModule`であること（本プロジェクトは該当）と、
  レイアウト確定後の座標であること（生成直後は1フレーム待って`RectTransformUtility.WorldToScreenPoint(null, rt.position)`）

## ワールドクリックの座標

- **`collider.bounds.center`を`WorldToScreenPoint`** が定石（論理原点や手書きoffsetはTerrainに当たってMISS）
- 設置原点を狙う場合は`AimAtPlaceOrigin`（CalcPlacePoint逆算＝接地面上のフットプリント中心）
- 詰まったら無マスク`Physics.Raycast`で当たっているlayerを可視化して切り分け

## ドラッグ・複合入力の規則

- ドラッグ=「始点で押下→（押下保持のまま）終点へ移動→解放」。DSLの`DragPlace`は
  押下と移動と解放を別フレームに分けて注入する
- **ドラッグ中にキーを注入するフレームでは、held状態のマウスstate（同座標・ボタン保持）を同時にre-queue**する。
  さもないとspurious press edgeで`WasPressedThisFrame`が再発火し選択リセット/誤確定が起きる
- レガシー環境（DSL無し）での生スニペット:
  ```csharp
  using UnityEngine.InputSystem; using UnityEngine.InputSystem.LowLevel;
  var st = new MouseState { position = new UnityEngine.Vector2(SX, SY) };
  st = st.WithButton(MouseButton.Left, HELD);        // HELD: true=押下中
  InputSystem.QueueStateEvent(Mouse.current, st);     // Update()は呼ばない
  ```
  キーは down と release を**別スニペット=別フレーム**で送る（間にシェルsleep 0.5）

## de-risk probe（長いシナリオ・録画前の必須ゲート）

本番前に「単体入力1つで期待状態が1つ成立するか」を確認する。
1点注入→0.5秒→内部状態read。ダメなら (a)`Mouse.current.position`が注入値か実OSカーソルか
(b)無マスクraycastの当たりlayer (c)狙い座標 (d)OS simulate-*汚染 の順で切り分け。

## カメラと視界（照準の前提）

- PlaceBlock遷移でカメラはトップダウンへ0.25秒tween（`OpenBuildMenuAndSelectBlock`が0.6秒待つ）。
  照準は**tween完了後に毎回WorldToScreenPointを取り直す**
- カメラcontrollerの`SetEnabled(false)`系は`Camera.main`をnull化しraycast/WorldToScreenPointが全滅する。切り離さない
- 録画の絵は「実プレイ視点」を守る（アバター・地面・HUDが映ること）。俯瞰直置きカメラは不合格
