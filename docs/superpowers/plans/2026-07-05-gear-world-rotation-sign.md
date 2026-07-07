# 歯車回転ワールド符号規約 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 歯車・シャフトの見た目回転に「ワールド軸正方向から見て時計回り」規約を導入し、北向き/南向き等の設置方向によらず同一ネットワークの回転方向が視覚的に一致するようにする。

**Architecture:** サーバーの `IsClockwise`（グラフ上の抽象符号）は一切変更しない。クライアントの `TransformRotationInfo` が回転符号を「ローカル軸」ではなく「ワールド軸の支配成分の符号」で決めるようにする。ベルト等の方向固定パーツ用に `AlwaysForward` モードを追加。Animator駆動はSpeed Multiplierパラメータで逆回転再生をサポートし、デフォルト再生方向（+/-）を選択可能にする。既存の歯車・シャフトPrefabは `uloop execute-dynamic-code` で一括変換（機械系は仮モデルのため対象外）。

**Tech Stack:** Unity (Client.Game asmdef), NUnit EditModeテスト (Client.Tests), uloop CLI

## Global Constraints

AGENTS.md より（全タスクに適用）:
- 作業開始時に必ず `pwd` を実行し `/Users/katsumi/moorestech-worktrees/tree3` にいることを確認する
- .cs ファイルを変更したら必ず `uloop compile --project-path ./moorestech_client` を実行する
- `.meta` ファイルは絶対に手動作成しない。`uloop compile` 後にUnityが生成した `.meta` をコミットする
- Prefab をテキストエディタ/Write/Edit で直接編集することは禁止。変更は `uloop execute-dynamic-code` 経由のみ
- `partial` は如何なる条件でも絶対禁止
- 1ファイル200行以下、1ディレクトリ10コードファイルまで
- 主要な処理セクションに日本語・英語の2行セットコメント（// 日本語 → // English）、各1行に収める。自明なコメントは書かない
- デフォルト引数禁止。引数追加は呼び出し側を全て変更する
- try-catch 禁止
- `#region Internal` はメソッド内ローカル関数をまとめる用途に限定
- 単純な setter プロパティ禁止（値のセットは `SetHoge` メソッド）
- 各タスク終了時に必ずコミットする（worktree運用のため作業消失防止）
- uloop で「Unity is reloading (Domain Reload in progress)」エラーが出たら45秒待機してリトライ

**前提:** このworktree用のUnity Editorが起動していること（起動していなければ `uloop-launch` スキル参照。`uloop compile` がタイムアウト/接続エラーになる場合はUnity未起動を疑う）。

## 配置と前例（spec-architecture-review 済み）

| 項目 | 配置先 | 根拠・前例 |
|---|---|---|
| `GearWorldRotationSign`（純ロジックstatic util） | `Client.Game/InGame/BlockSystem/StateProcessor/Gear/` | 見た目符号の解決はクライアントの責務。サーバー/Coreには一切追加しない（設計合意済み） |
| `RotationInfo` / `TransformRotationInfo` / `AnimatorRotationInfo` のファイル分割 | 同上 `Gear/` サブフォルダ | 200行制約対応。**namespace `Client.Game.InGame.BlockSystem.StateProcessor` とアセンブリ `Client.Game` は絶対に変更しない**（Prefabの `SerializeReference` データが `ns:`/`asm:` を記録しているため。変更するとデシリアライズ不能） |
| 新enum（`GearRotationDirectionMode` / `AnimationPlayDirection`） | `RotationInfo.cs` 内 | serializedフィールドの型。既存データはフィールド欠落→既定値0（=NetworkSigned/Positive）で後方互換 |
| Prefab一括変換 | `uloop execute-dynamic-code`（コード資産を残さない一回限りの変換） | AGENTS.mdの正規ルート。`SerializedObject`/`PrefabUtility` 経由でUnityがシリアライズ |
| テスト | `Client.Tests/Gear/`（EditMode） | 前例: `Client.Tests/PlaceSystem/`, `Client.Tests/ColliderStreaming/` の純ロジックEditModeテスト。asmdefは既に `Client.Game`, `Game.Gear` を参照済みで変更不要 |

**新規パターン（ユーザーレビュー注目点）:**
1. `RotationInfo.Rotate` のシグネチャを `Rotate(GearStateDetail, float deltaTime)` に変更（テスト可能性・エディタシミュレータの正確化のため。呼び出し側は `GearStateChangeProcessor.Update` と `GearStateChangeProcessorSimulator` の2箇所のみ）
2. Animator逆回転再生の規約: AnimatorControllerにfloatパラメータ **`GearRotationDirection`**（既定値1）を宣言しステートのSpeed Multiplierに割り当てると逆再生が効く。パラメータが無いコントローラーは正転フォールバック（既存機械プレハブは無変更で動き続ける）

## 対象Prefab（Task 5/6で使用する確定リスト）

一括変換対象（歯車・シャフトのみ。機械系は仮モデルのため対象外）:

```
Assets/AddressableResources/Block/Shaft.prefab
Assets/AddressableResources/Block/Shaft Iron.prefab
Assets/AddressableResources/Block/Shaft Vertical.prefab
Assets/AddressableResources/Block/Shaft Vertical Iron.prefab
Assets/AddressableResources/Block/GearBeltConveyor Shaft.prefab
Assets/AddressableResources/Block/BigGear.prefab
Assets/AddressableResources/Block/SmallGear.prefab
Assets/AddressableResources/Block/GearChainPole.prefab
Assets/AddressableResources/Block/CompactGearChainPole.prefab
Assets/AddressableResources/Item/Iron Gear.prefab
```

（現状確認済み: Shaft/BigGear/SmallGear は `rotationAxis: 2 (Z)`, `isReverse: 0`, rotationTransform は無回転の子。変換はほぼno-opの見込みだが、安全パスとして全件実行しレポートを確認する）

---

### Task 1: GearWorldRotationSign 純ロジッククラス

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/Gear/GearWorldRotationSign.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Gear/GearWorldRotationSignTest.cs`

**Interfaces:**
- Consumes: 既存enum `RotationAxis`（`Client.Game.InGame.BlockSystem.StateProcessor` — Task 2でファイル移動するが型は同一）
- Produces: `static Vector3 GearWorldRotationSign.ToAxisVector(RotationAxis axis)`, `static float GearWorldRotationSign.GetWorldAxisSign(Quaternion worldRotation, RotationAxis axis)`（戻り値は +1f または -1f）。Task 3, 5 が使用する。

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/Gear/GearWorldRotationSignTest.cs` を作成:

```csharp
using Client.Game.InGame.BlockSystem.StateProcessor;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Gear
{
    public class GearWorldRotationSignTest
    {
        [Test]
        public void 無回転のZ軸は正符号()
        {
            Assert.AreEqual(1f, GearWorldRotationSign.GetWorldAxisSign(Quaternion.identity, RotationAxis.Z));
        }

        [Test]
        public void Yaw180のZ軸は負符号()
        {
            // 南向き配置相当。Z軸がワールド-Zを向くため符号が反転する
            // South-facing placement; local Z points to world -Z, so the sign flips
            Assert.AreEqual(-1f, GearWorldRotationSign.GetWorldAxisSign(Quaternion.Euler(0, 180, 0), RotationAxis.Z));
        }

        [Test]
        public void Yaw90のZ軸は正符号()
        {
            // Z軸がワールド+Xを向く。支配成分Xが正なので正符号
            // Local Z points to world +X; dominant component X is positive
            Assert.AreEqual(1f, GearWorldRotationSign.GetWorldAxisSign(Quaternion.Euler(0, 90, 0), RotationAxis.Z));
        }

        [Test]
        public void Yaw270のZ軸は負符号()
        {
            Assert.AreEqual(-1f, GearWorldRotationSign.GetWorldAxisSign(Quaternion.Euler(0, 270, 0), RotationAxis.Z));
        }

        [Test]
        public void 任意YawのY軸は常に正符号()
        {
            // 垂直軸はYaw回転の影響を受けない（平置き歯車は元々バグの影響外）
            // Vertical axis is unaffected by yaw; flat gears were never affected by the bug
            foreach (var yaw in new[] { 0f, 90f, 180f, 270f })
                Assert.AreEqual(1f, GearWorldRotationSign.GetWorldAxisSign(Quaternion.Euler(0, yaw, 0), RotationAxis.Y), $"yaw={yaw}");
        }

        [Test]
        public void Yaw180のX軸は負符号()
        {
            Assert.AreEqual(-1f, GearWorldRotationSign.GetWorldAxisSign(Quaternion.Euler(0, 180, 0), RotationAxis.X));
        }

        [Test]
        public void ToAxisVectorが各軸の単位ベクトルを返す()
        {
            Assert.AreEqual(Vector3.right, GearWorldRotationSign.ToAxisVector(RotationAxis.X));
            Assert.AreEqual(Vector3.up, GearWorldRotationSign.ToAxisVector(RotationAxis.Y));
            Assert.AreEqual(Vector3.forward, GearWorldRotationSign.ToAxisVector(RotationAxis.Z));
        }
    }
}
```

- [ ] **Step 2: コンパイルしてテストが失敗（コンパイルエラー）することを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `GearWorldRotationSign` が存在しないためコンパイルエラー

- [ ] **Step 3: 実装を書く**

`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/Gear/GearWorldRotationSign.cs` を作成:

```csharp
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    /// <summary>
    /// 歯車回転のワールド符号規約を計算する純ロジック。
    /// 規約: IsClockwise=true は「回転軸のワールド正方向(+X/+Y/+Z)から見てUnityの正回転」を意味する。
    /// これにより設置方向(Yaw)が180度違っても同一ネットワークの見た目回転方向が一致する。
    ///
    /// Pure logic for the world-sign convention of gear rotation.
    /// Convention: IsClockwise=true means positive Unity rotation viewed from the positive world axis (+X/+Y/+Z).
    /// This keeps the apparent spin direction consistent across placement directions differing by 180 degrees.
    /// </summary>
    public static class GearWorldRotationSign
    {
        public static Vector3 ToAxisVector(RotationAxis axis)
        {
            return axis switch
            {
                RotationAxis.X => Vector3.right,
                RotationAxis.Y => Vector3.up,
                RotationAxis.Z => Vector3.forward,
                _ => Vector3.zero,
            };
        }

        public static float GetWorldAxisSign(Quaternion worldRotation, RotationAxis axis)
        {
            var worldAxis = worldRotation * ToAxisVector(axis);

            // 支配成分(絶対値最大)の符号を採用。ブロックは軸整列配置なので厳密に決まる
            // Use the sign of the dominant (largest absolute) component; block placement is axis-aligned so this is exact
            var absX = Mathf.Abs(worldAxis.x);
            var absY = Mathf.Abs(worldAxis.y);
            var absZ = Mathf.Abs(worldAxis.z);

            if (absX >= absY && absX >= absZ) return worldAxis.x >= 0 ? 1f : -1f;
            if (absY >= absZ) return worldAxis.y >= 0 ? 1f : -1f;
            return worldAxis.z >= 0 ? 1f : -1f;
        }
    }
}
```

- [ ] **Step 4: コンパイルとテスト実行**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GearWorldRotationSignTest"`
Expected: 7件全てPASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/Gear/GearWorldRotationSign.cs* moorestech_client/Assets/Scripts/Client.Tests/Gear/
git commit -m "Add world-axis sign calculator for gear rotation convention

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

（`.cs*` はUnity生成の `.meta` を含めるため。`Client.Tests/Gear/` ディレクトリの `.meta` も含まれることを `git status` で確認）

---

### Task 2: RotationInfo群のファイル分割と Rotate シグネチャ変更

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/GearStateChangeProcessor.cs`（RotationInfo群を抜き、deltaTime引数を追加）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/Gear/RotationInfo.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/Gear/TransformRotationInfo.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/Gear/AnimatorRotationInfo.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/Gear/GearStateChangeProcessorSimulator.cs:45-82`（Rotate呼び出しにdeltaTime）

**Interfaces:**
- Consumes: Task 1 の `GearWorldRotationSign.ToAxisVector`
- Produces: `abstract void RotationInfo.Rotate(GearStateDetail gearStateDetail, float deltaTime)`、`public void GearStateChangeProcessor.Rotate(GearStateDetail gearStateDetail, float deltaTime)`、enum `GearRotationDirectionMode { NetworkSigned, AlwaysForward }`、enum `AnimationPlayDirection { Positive, Negative }`、`TransformRotationInfo` のテスト用コンストラクタ `TransformRotationInfo(RotationAxis, Transform, float, bool, GearRotationDirectionMode)`。Task 3, 4, 6 が使用する。

**注意:** このタスクは分割・シグネチャ変更のみで、回転ロジックの中身は既存のまま（ワールド符号はTask 3）。namespace は `Client.Game.InGame.BlockSystem.StateProcessor` を**全ファイルで維持**する（SerializeReference互換のため）。

- [ ] **Step 1: RotationInfo.cs を作成（base + enum群）**

`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/Gear/RotationInfo.cs`:

```csharp
using System;
using Game.Gear.Common;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    [Serializable]
    public abstract class RotationInfo
    {
        [SerializeField] protected bool isReverse;
        [SerializeField] protected GearRotationDirectionMode directionMode;

        public bool IsReverse => isReverse;

        // TransformRotationInfoのみ値を返す。Simulator互換のためベースに公開
        // Only TransformRotationInfo returns a value. Exposed on base for simulator compatibility
        public virtual Transform RotationTransform => null;

        public abstract void Rotate(GearStateDetail gearStateDetail, float deltaTime);
    }

    public enum GearRotationDirectionMode
    {
        // ネットワーク回転方向とワールド符号規約に追従(かみ合う歯車・シャフト用)
        // Follow the network direction with the world-sign convention (meshing gears and shafts)
        NetworkSigned,

        // ネットワーク回転方向を無視して常に正転(ベルト表面等のゲームプレイ方向固定パーツ用)
        // Always run forward ignoring the network direction (gameplay-directional parts like belt surfaces)
        AlwaysForward,
    }

    public enum RotationAxis
    {
        X,
        Y,
        Z,
    }

    public enum AnimationPlayDirection
    {
        Positive,
        Negative,
    }
}
```

- [ ] **Step 2: TransformRotationInfo.cs を作成（ロジックは既存のまま移設 + deltaTime + テスト用コンストラクタ）**

`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/Gear/TransformRotationInfo.cs`:

```csharp
using System;
using Game.Gear.Common;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    [Serializable]
    [SubclassSelectorName("Transform Rotate")]
    public class TransformRotationInfo : RotationInfo
    {
        [SerializeField] private RotationAxis rotationAxis;
        [SerializeField] private Transform rotationTransform;
        [SerializeField] private float rotationSpeed = 1;

        public RotationAxis RotationAxis => rotationAxis;
        public override Transform RotationTransform => rotationTransform;
        public float RotationSpeed => rotationSpeed;

        public TransformRotationInfo()
        {
        }

        // テスト用コンストラクタ
        // Constructor for tests
        public TransformRotationInfo(RotationAxis axis, Transform transform, float speed, bool reverse, GearRotationDirectionMode mode)
        {
            rotationAxis = axis;
            rotationTransform = transform;
            rotationSpeed = speed;
            isReverse = reverse;
            directionMode = mode;
        }

        public override void Rotate(GearStateDetail gearStateDetail, float deltaTime)
        {
            if (rotationTransform == null) return;

            // RPMからこのフレームの回転角を計算
            // Compute this frame's rotation angle from RPM
            var angle = gearStateDetail.CurrentRpm / 60 * deltaTime * 360 * rotationSpeed;
            angle *= isReverse ? -1 : 1;
            angle *= gearStateDetail.IsClockwise ? 1 : -1;

            rotationTransform.Rotate(GearWorldRotationSign.ToAxisVector(rotationAxis) * angle);
        }
    }
}
```

- [ ] **Step 3: AnimatorRotationInfo.cs を作成（ロジックは既存のまま移設 + deltaTime）**

`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/Gear/AnimatorRotationInfo.cs`:

```csharp
using System;
using Game.Gear.Common;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    [Serializable]
    [SubclassSelectorName("Animator")]
    public class AnimatorRotationInfo : RotationInfo
    {
        [SerializeField] private Animator animator;
        [SerializeField] private float rpm60Speed = 1;

        public Animator Animator => animator;
        public float Rpm60Speed => rpm60Speed;

        public override void Rotate(GearStateDetail gearStateDetail, float deltaTime)
        {
            if (animator == null) return;

            var rpmRate = gearStateDetail.CurrentRpm / 60f;
            var speed = rpm60Speed * (isReverse ? -1 : 1) * (gearStateDetail.IsClockwise ? 1 : -1) * rpmRate;
            animator.speed = speed;
        }
    }
}
```

- [ ] **Step 4: GearStateChangeProcessor.cs を書き換え（本体のみ残す）**

`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/GearStateChangeProcessor.cs` の全内容を以下に置換:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Game.Gear.Common;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    [RequireComponent(typeof(GearStateChangeProcessorSimulator))]
    public class GearStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        public IReadOnlyList<RotationInfo> RotationInfos => rotationInfos;
        [SerializeReference, SubclassSelector] private List<RotationInfo> rotationInfos = new();

        private GearStateDetail _currentGearState;

        public void Initialize(BlockGameObject blockGameObject) { }

        public void OnChangeState(BlockStateMessagePack blockState)
        {
            _currentGearState = blockState.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey);
        }

        private void Update()
        {
            if (_currentGearState == null) return;

            Rotate(_currentGearState, Time.deltaTime);
        }

        public void Rotate(GearStateDetail gearStateDetail, float deltaTime)
        {
            foreach (var rotationInfo in rotationInfos)
            {
                if (rotationInfo == null) continue;
                rotationInfo.Rotate(gearStateDetail, deltaTime);
            }
        }

#if UNITY_EDITOR
        public GearStateDetail DebugCurrentGearState => _currentGearState;
#endif
    }
}
```

- [ ] **Step 5: Simulator の Rotate 呼び出しを修正**

`GearStateChangeProcessorSimulator.cs` の変更点は2箇所。

(a) staticフィールド追加（`_isEditorUpdateRegistered` の直後）:

```csharp
        private static bool _isEditorUpdateRegistered = false;

        // エディタ更新の前回時刻(deltaTime計測用)
        // Last editor update time (for measuring deltaTime)
        private static double _lastEditorUpdateTime;
```

(b) `OnEditorUpdate()` の先頭でdeltaTimeを計測し、`Rotate` 呼び出しへ渡す:

```csharp
        private static void OnEditorUpdate()
        {
            // エディタはTime.deltaTimeが使えないため自前で計測する
            // Editor updates cannot rely on Time.deltaTime, so measure it manually
            var now = EditorApplication.timeSinceStartup;
            var deltaTime = Mathf.Clamp((float)(now - _lastEditorUpdateTime), 0f, 0.1f);
            _lastEditorUpdateTime = now;

            // アクティブな全シミュレーターを更新
            // Update all active simulators
            for (int i = _activeSimulators.Count - 1; i >= 0; i--)
            {
                var simulator = _activeSimulators[i];

                // nullチェック（GameObject削除済みの場合）
                // Null check (in case GameObject is deleted)
                if (simulator == null)
                {
                    _activeSimulators.RemoveAt(i);
                    continue;
                }

                // シミュレーション実行
                // Execute simulation
                if (simulator.isSimulating && simulator.targetProcessor != null)
                {
                    var state = new GearStateDetail(
                        simulator.simulateIsClockwise,
                        simulator.simulateRpm,
                        0
                    );
                    simulator.targetProcessor.Rotate(state, deltaTime);
                }
            }

            // アクティブなシミュレーターがなくなったらコールバック解除
            // Unregister callback if no active simulators
            if (_activeSimulators.Count == 0)
            {
                EditorApplication.update -= OnEditorUpdate;
                _isEditorUpdateRegistered = false;
            }
        }
```

さらに `StartSimulation()` の `EditorApplication.update += OnEditorUpdate;` の直前に1行追加:

```csharp
            if (!_isEditorUpdateRegistered)
            {
                _lastEditorUpdateTime = EditorApplication.timeSinceStartup;
                EditorApplication.update += OnEditorUpdate;
                _isEditorUpdateRegistered = true;
            }
```

- [ ] **Step 6: コンパイルと既存テストの回帰確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0（`Rotate(state)` の旧シグネチャ呼び出しが残っているとエラーになる。エラーが出たら該当呼び出し箇所に `deltaTime` を追加）

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GearWorldRotationSignTest"`
Expected: 7件PASS（回帰なし）

- [ ] **Step 7: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/
git commit -m "Split RotationInfo classes into files and inject deltaTime into Rotate

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 3: TransformRotationInfo にワールド符号規約と directionMode を実装

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/Gear/TransformRotationInfo.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Gear/TransformRotationInfoTest.cs`

**Interfaces:**
- Consumes: `GearWorldRotationSign.GetWorldAxisSign(Quaternion, RotationAxis)`（Task 1）、`TransformRotationInfo(RotationAxis, Transform, float, bool, GearRotationDirectionMode)`（Task 2）、`GearStateDetail(bool isClockwise, float currentRpm, float currentTorque)`（Game.Gear.Common・既存）
- Produces: `TransformRotationInfo.Rotate` の新挙動（NetworkSigned: ワールド符号×ネットワーク符号、AlwaysForward: 常に正転）。Task 5, 6 が前提とする。

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/Gear/TransformRotationInfoTest.cs` を作成:

```csharp
using Client.Game.InGame.BlockSystem.StateProcessor;
using Game.Gear.Common;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Gear
{
    public class TransformRotationInfoTest
    {
        [Test]
        public void 対向配置の回転パーツが同一ワールド方向に回転する()
        {
            // 北向きと南向き(Yaw180度差)で同じ設定のパーツが同じワールド回転をすることを検証
            // Verify that identically configured parts facing north and south spin the same way in world space
            var deltaNorth = RotateOnce(0f, true, GearRotationDirectionMode.NetworkSigned);
            var deltaSouth = RotateOnce(180f, true, GearRotationDirectionMode.NetworkSigned);

            AssertSameSpin(deltaNorth, deltaSouth);
        }

        [Test]
        public void 反時計回りは時計回りと逆方向に回転する()
        {
            var clockwise = RotateOnce(0f, true, GearRotationDirectionMode.NetworkSigned);
            var counterClockwise = RotateOnce(0f, false, GearRotationDirectionMode.NetworkSigned);

            clockwise.ToAngleAxis(out var angleCw, out var axisCw);
            counterClockwise.ToAngleAxis(out var angleCcw, out var axisCcw);
            Assert.Greater(angleCw, 0.1f);
            Assert.Greater(angleCcw, 0.1f);
            Assert.Less(Vector3.Dot(axisCw, axisCcw), -0.9f);
        }

        [Test]
        public void AlwaysForwardはネットワーク回転方向を無視する()
        {
            var clockwise = RotateOnce(0f, true, GearRotationDirectionMode.AlwaysForward);
            var counterClockwise = RotateOnce(0f, false, GearRotationDirectionMode.AlwaysForward);

            AssertSameSpin(clockwise, counterClockwise);
        }

        [Test]
        public void AlwaysForwardは設置方向にも依存しない()
        {
            // ベルト表面等はブロックローカルで正転し続ける(ワールド補正もしない)
            // Belt surfaces keep running forward in block-local space (no world-sign correction either)
            var north = RotateOnceLocal(0f, GearRotationDirectionMode.AlwaysForward);
            var south = RotateOnceLocal(180f, GearRotationDirectionMode.AlwaysForward);

            AssertSameSpin(north, south);
        }

        private static void AssertSameSpin(Quaternion deltaA, Quaternion deltaB)
        {
            deltaA.ToAngleAxis(out var angleA, out var axisA);
            deltaB.ToAngleAxis(out var angleB, out var axisB);
            Assert.Greater(angleA, 0.1f);
            Assert.AreEqual(angleA, angleB, 0.01f);
            Assert.Greater(Vector3.Dot(axisA, axisB), 0.9f);
        }

        private static Quaternion RotateOnce(float yaw, bool isClockwise, GearRotationDirectionMode mode)
        {
            var (parent, child) = CreateHierarchy(yaw);
            var info = new TransformRotationInfo(RotationAxis.Z, child, 1f, false, mode);

            var before = child.rotation;
            info.Rotate(new GearStateDetail(isClockwise, 60f, 0f), 1f / 60f);
            var delta = child.rotation * Quaternion.Inverse(before);

            Object.DestroyImmediate(parent.gameObject);
            return delta;
        }

        private static Quaternion RotateOnceLocal(float yaw, GearRotationDirectionMode mode)
        {
            var (parent, child) = CreateHierarchy(yaw);
            var info = new TransformRotationInfo(RotationAxis.Z, child, 1f, false, mode);

            var before = child.localRotation;
            info.Rotate(new GearStateDetail(true, 60f, 0f), 1f / 60f);
            var delta = child.localRotation * Quaternion.Inverse(before);

            Object.DestroyImmediate(parent.gameObject);
            return delta;
        }

        private static (Transform parent, Transform child) CreateHierarchy(float yaw)
        {
            // ブロック設置と同様に親(ブロックルート)へYawを与え、回転パーツを子に置く
            // Give yaw to the parent (block root) like block placement and put the rotating part as a child
            var parent = new GameObject("BlockRoot").transform;
            parent.rotation = Quaternion.Euler(0, yaw, 0);
            var child = new GameObject("RotationPart").transform;
            child.SetParent(parent, false);
            return (parent, child);
        }
    }
}
```

- [ ] **Step 2: コンパイルしてテストが失敗することを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0（テストはコンパイル可能。Task 2で型は揃っている）

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TransformRotationInfoTest"`
Expected: `対向配置の回転パーツが同一ワールド方向に回転する` と `AlwaysForwardはネットワーク回転方向を無視する` がFAIL（旧ロジックのため）

- [ ] **Step 3: TransformRotationInfo.Rotate を新ロジックに書き換え**

`TransformRotationInfo.cs` の `Rotate` メソッドを以下に置換:

```csharp
        public override void Rotate(GearStateDetail gearStateDetail, float deltaTime)
        {
            if (rotationTransform == null) return;

            // RPMからこのフレームの回転角を計算し、方向符号を掛ける
            // Compute this frame's rotation angle from RPM and apply the direction sign
            var angle = gearStateDetail.CurrentRpm / 60 * deltaTime * 360 * rotationSpeed;
            angle *= isReverse ? -1 : 1;
            angle *= CalculateDirectionSign(gearStateDetail.IsClockwise);

            rotationTransform.Rotate(GearWorldRotationSign.ToAxisVector(rotationAxis) * angle);
        }

        private float CalculateDirectionSign(bool isClockwise)
        {
            // 方向固定パーツはネットワーク符号もワールド符号も無視して常に正転
            // Direction-fixed parts always run forward, ignoring both network and world signs
            if (directionMode == GearRotationDirectionMode.AlwaysForward) return 1f;

            // ワールド符号規約: 軸のワールド正方向から見た回転方向を全設置方向で一致させる
            // World-sign convention: keep the apparent spin viewed from the positive world axis consistent across directions
            var worldSign = GearWorldRotationSign.GetWorldAxisSign(rotationTransform.rotation, rotationAxis);
            var networkSign = isClockwise ? 1f : -1f;
            return worldSign * networkSign;
        }
```

- [ ] **Step 4: コンパイルとテスト実行**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TransformRotationInfoTest|GearWorldRotationSignTest"`
Expected: 全件PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/Gear/TransformRotationInfo.cs moorestech_client/Assets/Scripts/Client.Tests/Gear/TransformRotationInfoTest.cs*
git commit -m "Apply world-axis sign convention to gear transform rotation

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 4: AnimatorRotationInfo の逆回転再生とデフォルト再生方向

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/Gear/AnimatorRotationInfo.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Gear/AnimatorRotationInfoDirectionTest.cs`

**Interfaces:**
- Consumes: enum `AnimationPlayDirection`, `GearRotationDirectionMode`（Task 2）
- Produces: `static float AnimatorRotationInfo.CalculateDirection(bool isClockwise, GearRotationDirectionMode mode, bool reverse, AnimationPlayDirection defaultDirection)`（+1f/-1f）、定数 `AnimatorRotationInfo.DirectionParameterName = "GearRotationDirection"`

**背景:** `Animator.speed` に負値を入れても逆再生は保証されない（Mecanimの負speedは非サポート）。公式にサポートされる逆再生手段はステートのSpeed Multiplierパラメータに負値を入れる方式。速度の大きさは `Animator.speed`、方向(±1)はfloatパラメータ `GearRotationDirection` に分離する。パラメータを持たないコントローラーは正転フォールバックし、既存プレハブの挙動を壊さない。

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/Gear/AnimatorRotationInfoDirectionTest.cs` を作成:

```csharp
using Client.Game.InGame.BlockSystem.StateProcessor;
using NUnit.Framework;

namespace Client.Tests.Gear
{
    public class AnimatorRotationInfoDirectionTest
    {
        [TestCase(true, false, AnimationPlayDirection.Positive, 1f)]
        [TestCase(false, false, AnimationPlayDirection.Positive, -1f)]
        [TestCase(true, false, AnimationPlayDirection.Negative, -1f)]
        [TestCase(false, false, AnimationPlayDirection.Negative, 1f)]
        [TestCase(true, true, AnimationPlayDirection.Positive, -1f)]
        [TestCase(false, true, AnimationPlayDirection.Positive, 1f)]
        public void NetworkSignedはネットワーク方向とデフォルト方向とisReverseの積(bool isClockwise, bool reverse, AnimationPlayDirection defaultDirection, float expected)
        {
            var actual = AnimatorRotationInfo.CalculateDirection(isClockwise, GearRotationDirectionMode.NetworkSigned, reverse, defaultDirection);
            Assert.AreEqual(expected, actual);
        }

        [TestCase(true, AnimationPlayDirection.Positive, 1f)]
        [TestCase(false, AnimationPlayDirection.Positive, 1f)]
        [TestCase(true, AnimationPlayDirection.Negative, -1f)]
        [TestCase(false, AnimationPlayDirection.Negative, -1f)]
        public void AlwaysForwardはネットワーク方向を無視する(bool isClockwise, AnimationPlayDirection defaultDirection, float expected)
        {
            var actual = AnimatorRotationInfo.CalculateDirection(isClockwise, GearRotationDirectionMode.AlwaysForward, false, defaultDirection);
            Assert.AreEqual(expected, actual);
        }
    }
}
```

- [ ] **Step 2: コンパイルして失敗を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `CalculateDirection` が存在しないためコンパイルエラー

- [ ] **Step 3: AnimatorRotationInfo を実装**

`AnimatorRotationInfo.cs` の全内容を以下に置換:

```csharp
using System;
using Game.Gear.Common;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    [Serializable]
    [SubclassSelectorName("Animator")]
    public class AnimatorRotationInfo : RotationInfo
    {
        // 逆再生用のSpeed Multiplierパラメータ名。AnimatorController側でfloat(既定値1)を宣言しステートのSpeed Multiplierに割り当てる
        // Speed Multiplier parameter name for reverse playback; declare a float (default 1) on the controller and bind it to the state's speed multiplier
        public const string DirectionParameterName = "GearRotationDirection";

        [SerializeField] private Animator animator;
        [SerializeField] private float rpm60Speed = 1;
        [SerializeField] private AnimationPlayDirection defaultPlayDirection = AnimationPlayDirection.Positive;

        public Animator Animator => animator;
        public float Rpm60Speed => rpm60Speed;

        private bool _directionParameterChecked;
        private bool _hasDirectionParameter;

        public override void Rotate(GearStateDetail gearStateDetail, float deltaTime)
        {
            if (animator == null) return;

            // 速度の大きさはAnimator.speed、方向はSpeed Multiplierパラメータに分離する(負のAnimator.speedは非サポートのため)
            // Split magnitude into Animator.speed and direction into the multiplier parameter (negative Animator.speed is unsupported)
            var rpmRate = gearStateDetail.CurrentRpm / 60f;
            animator.speed = Mathf.Abs(rpm60Speed * rpmRate);

            var direction = CalculateDirection(gearStateDetail.IsClockwise, directionMode, isReverse, defaultPlayDirection);
            if (HasDirectionParameter()) animator.SetFloat(DirectionParameterName, direction);
        }

        /// <summary>
        /// 再生方向(+1/-1)を計算する純関数
        /// Pure function computing the playback direction (+1/-1)
        /// </summary>
        public static float CalculateDirection(bool isClockwise, GearRotationDirectionMode mode, bool reverse, AnimationPlayDirection defaultDirection)
        {
            var networkSign = mode == GearRotationDirectionMode.AlwaysForward || isClockwise ? 1f : -1f;
            var reverseSign = reverse ? -1f : 1f;
            var defaultSign = defaultDirection == AnimationPlayDirection.Positive ? 1f : -1f;
            return networkSign * reverseSign * defaultSign;
        }

        private bool HasDirectionParameter()
        {
            // パラメータ有無を初回のみ走査してキャッシュ。無いコントローラーは正転フォールバック
            // Scan parameters once and cache; controllers without the parameter fall back to forward playback
            if (_directionParameterChecked) return _hasDirectionParameter;
            _directionParameterChecked = true;
            foreach (var parameter in animator.parameters)
            {
                if (parameter.name != DirectionParameterName || parameter.type != AnimatorControllerParameterType.Float) continue;
                _hasDirectionParameter = true;
                break;
            }
            return _hasDirectionParameter;
        }
    }
}
```

- [ ] **Step 4: コンパイルとテスト実行**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "AnimatorRotationInfoDirectionTest"`
Expected: 10件全てPASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/Gear/AnimatorRotationInfo.cs moorestech_client/Assets/Scripts/Client.Tests/Gear/AnimatorRotationInfoDirectionTest.cs*
git commit -m "Support reverse animator playback with selectable default direction

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 5: 歯車・シャフトPrefabの一括変換

**Files:**
- Modify（Unity経由のみ）: 「対象Prefab」セクションの10ファイル

**Interfaces:**
- Consumes: `GearWorldRotationSign.GetWorldAxisSign`（Task 1）、`GearStateChangeProcessor` の serialized field `rotationInfos`（SerializedProperty経由）
- Produces: 変換済みPrefab（プレハブ作成時ポーズでの見た目回転方向が変更前と同一になるよう `isReverse` を補正）

**変換ルール:** 各 `TransformRotationInfo` について、プレハブ作成時ポーズ（root無回転）での回転軸のワールド符号を計算し、負なら `isReverse` を反転する。ワールド符号規約導入によって符号が−1になるパーツだけが対象で、これにより変換前後でプレハブ標準ポーズの見た目が変わらない。確認済みの3プレハブ（Shaft/BigGear/SmallGear）は軸Z・無回転Transformなので符号+1＝no-op見込み。変換は冪等（2回実行しても符号が正のものは触らないため安全）。

- [ ] **Step 1: uloop execute-dynamic-code で変換を実行**

`uloop-execute-dynamic-code` スキルの手順に従い、以下のC#コードをUnity Editorで実行する:

```csharp
var paths = new[]
{
    "Assets/AddressableResources/Block/Shaft.prefab",
    "Assets/AddressableResources/Block/Shaft Iron.prefab",
    "Assets/AddressableResources/Block/Shaft Vertical.prefab",
    "Assets/AddressableResources/Block/Shaft Vertical Iron.prefab",
    "Assets/AddressableResources/Block/GearBeltConveyor Shaft.prefab",
    "Assets/AddressableResources/Block/BigGear.prefab",
    "Assets/AddressableResources/Block/SmallGear.prefab",
    "Assets/AddressableResources/Block/GearChainPole.prefab",
    "Assets/AddressableResources/Block/CompactGearChainPole.prefab",
    "Assets/AddressableResources/Item/Iron Gear.prefab",
};
var report = new System.Text.StringBuilder();
foreach (var path in paths)
{
    var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
    var changed = false;
    foreach (var processor in root.GetComponentsInChildren<Client.Game.InGame.BlockSystem.StateProcessor.GearStateChangeProcessor>(true))
    {
        var so = new UnityEditor.SerializedObject(processor);
        var list = so.FindProperty("rotationInfos");
        for (var i = 0; i < list.arraySize; i++)
        {
            var element = list.GetArrayElementAtIndex(i);
            if (!element.managedReferenceFullTypename.Contains("TransformRotationInfo")) continue;
            var rotationTransform = element.FindPropertyRelative("rotationTransform").objectReferenceValue as UnityEngine.Transform;
            if (rotationTransform == null) continue;
            var axis = (Client.Game.InGame.BlockSystem.StateProcessor.RotationAxis)element.FindPropertyRelative("rotationAxis").enumValueIndex;
            var sign = Client.Game.InGame.BlockSystem.StateProcessor.GearWorldRotationSign.GetWorldAxisSign(rotationTransform.rotation, axis);
            if (sign < 0)
            {
                var reverseProp = element.FindPropertyRelative("isReverse");
                reverseProp.boolValue = !reverseProp.boolValue;
                changed = true;
                report.AppendLine(path + " [" + i + "] isReverse -> " + reverseProp.boolValue);
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    if (changed) UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
    else report.AppendLine(path + ": no change");
    UnityEditor.PrefabUtility.UnloadPrefabContents(root);
}
UnityEditor.AssetDatabase.SaveAssets();
return report.ToString();
```

Expected: 各プレハブごとに `no change` または `isReverse -> true/false` の行が出力される。出力レポートを必ず読み、変換されたプレハブと理由をユーザー向け報告に残す。

- [ ] **Step 2: 変換結果の差分確認**

Run: `git -C /Users/katsumi/moorestech-worktrees/tree3 status --short -- "moorestech_client/Assets/AddressableResources/"`
Expected: 変換された `.prefab` のみが変更されている（レポートと一致すること）。差分内容は `git diff` で `isReverse: 0/1` の変化だけであることを確認

- [ ] **Step 3: コミット（変更があった場合のみ）**

```bash
git add moorestech_client/Assets/AddressableResources/
git commit -m "Convert gear and shaft prefabs to world-axis sign convention

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

変更ゼロ（全件 no change）の場合はコミット不要。その旨を最終報告に記載する。

---

### Task 6: 4方向設置一貫性の回帰テスト（プレハブ横断）

**Files:**
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Gear/GearPrefabFourDirectionConsistencyTest.cs`

**Interfaces:**
- Consumes: `GearStateChangeProcessor.RotationInfos`（`IReadOnlyList<RotationInfo>`）、`GearStateChangeProcessor.Rotate(GearStateDetail, float)`（Task 2）、`RotationInfo.RotationTransform`（Task 2）
- Produces: 目視QAの代替となる自動回帰テスト。「対向設置（0°vs180°、90°vs270°）で全回転パーツのワールド回転方向が一致する」ことを実プレハブで検証する。

- [ ] **Step 1: テストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/Gear/GearPrefabFourDirectionConsistencyTest.cs` を作成:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Game.Gear.Common;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Client.Tests.Gear
{
    public class GearPrefabFourDirectionConsistencyTest
    {
        private static readonly string[] GearShaftPrefabPaths =
        {
            "Assets/AddressableResources/Block/Shaft.prefab",
            "Assets/AddressableResources/Block/Shaft Iron.prefab",
            "Assets/AddressableResources/Block/Shaft Vertical.prefab",
            "Assets/AddressableResources/Block/Shaft Vertical Iron.prefab",
            "Assets/AddressableResources/Block/GearBeltConveyor Shaft.prefab",
            "Assets/AddressableResources/Block/BigGear.prefab",
            "Assets/AddressableResources/Block/SmallGear.prefab",
            "Assets/AddressableResources/Block/GearChainPole.prefab",
            "Assets/AddressableResources/Block/CompactGearChainPole.prefab",
        };

        [Test]
        public void 対向設置で同一ワールド方向に回転する([ValueSource(nameof(GearShaftPrefabPaths))] string prefabPath)
        {
            // 180度差の設置ペアで全回転パーツのワールド回転方向が一致することを検証
            // Verify all rotating parts spin the same world direction for placements differing by 180 degrees
            AssertSameWorldSpin(prefabPath, 0f, 180f);
            AssertSameWorldSpin(prefabPath, 90f, 270f);
        }

        private static void AssertSameWorldSpin(string prefabPath, float yawA, float yawB)
        {
            var deltasA = CollectSpinDeltas(prefabPath, yawA);
            var deltasB = CollectSpinDeltas(prefabPath, yawB);
            Assert.AreEqual(deltasA.Count, deltasB.Count);

            for (var i = 0; i < deltasA.Count; i++)
            {
                deltasA[i].ToAngleAxis(out var angleA, out var axisA);
                deltasB[i].ToAngleAxis(out var angleB, out var axisB);

                // 回転しないパーツ(rotationSpeed=0等)はスキップ
                // Skip parts that do not rotate (e.g. rotationSpeed = 0)
                if (angleA < 0.1f && angleB < 0.1f) continue;

                var label = $"{prefabPath} yaw {yawA} vs {yawB} part {i}";
                Assert.AreEqual(angleA, angleB, 0.01f, label);
                Assert.Greater(Vector3.Dot(axisA, axisB), 0.9f, label);
            }
        }

        private static List<Quaternion> CollectSpinDeltas(string prefabPath, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab, prefabPath);
            var instance = Object.Instantiate(prefab, Vector3.zero, Quaternion.Euler(0, yaw, 0));
            var processor = instance.GetComponentInChildren<GearStateChangeProcessor>(true);
            Assert.IsNotNull(processor, prefabPath);

            // 回転前後のワールド回転を記録して差分を取る
            // Record world rotations before and after rotating and take the delta
            var targets = new List<Transform>();
            foreach (var info in processor.RotationInfos)
            {
                if (info?.RotationTransform != null) targets.Add(info.RotationTransform);
            }

            var before = new List<Quaternion>();
            foreach (var target in targets) before.Add(target.rotation);

            processor.Rotate(new GearStateDetail(true, 60f, 0f), 1f / 60f);

            var deltas = new List<Quaternion>();
            for (var i = 0; i < targets.Count; i++) deltas.Add(targets[i].rotation * Quaternion.Inverse(before[i]));

            Object.DestroyImmediate(instance);
            return deltas;
        }
    }
}
```

- [ ] **Step 2: コンパイルとテスト実行**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GearPrefabFourDirectionConsistencyTest"`
Expected: 9件（プレハブごと）全てPASS

FAILした場合: 対象プレハブの `rotationInfos` 設定（軸・Transform階層）を `uloop execute-dynamic-code` で調査する。コードのワールド符号ロジックが正しければ、このテストは isReverse 値に関係なくPASSするはず（isReverse は全方向に等しく掛かるため）。FAILはロジックバグを意味する。

- [ ] **Step 3: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/Gear/GearPrefabFourDirectionConsistencyTest.cs*
git commit -m "Add four-direction spin consistency regression test for gear prefabs

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 7: 総仕上げ（全体回帰・最終確認）

**Files:**
- なし（検証のみ。修正が出た場合は該当ファイル）

**Interfaces:**
- Consumes: Task 1〜6 の全成果物
- Produces: 全テストグリーンの最終状態

- [ ] **Step 1: 全体コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0・警告の新規増加なし

- [ ] **Step 2: 歯車関連テストの一括実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GearWorldRotationSignTest|TransformRotationInfoTest|AnimatorRotationInfoDirectionTest|GearPrefabFourDirectionConsistencyTest"`
Expected: 全件PASS

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GearNetworkTest"`
Expected: 全件PASS（サーバー側ロジック無変更の回帰確認。クライアントproject-pathから実行可能）

- [ ] **Step 3: エラーログ確認**

Run: `uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: 今回の変更に起因するエラーなし

- [ ] **Step 4: 未コミット作業の確認とコミット**

Run: `git status --short`
Expected: クリーン。未コミットの `.meta` 等が残っていれば:

```bash
git add -A
git commit -m "Add remaining Unity-generated meta files for gear rotation work

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

- [ ] **Step 5: 最終報告に含める内容**

- Task 5 の変換レポート全文（どのプレハブが変換されたか / 全件no-changeか）
- 新規パターン2点（Rotateシグネチャ変更、Animator逆再生の `GearRotationDirection` パラメータ規約）の再掲
- 今後のプレハブ作成規約: かみ合う歯車・シャフトは `NetworkSigned`、ベルト表面・加工アニメ等は `AlwaysForward`。Animatorで逆再生したい場合はコントローラーに float `GearRotationDirection`（既定値1）を宣言しステートのSpeed Multiplierへ割当

---

## Self-Review 結果

- **Spec coverage:** ワールド符号規約（Task 1, 3）、方向固定パーツのAlwaysForward（Task 2, 3）、Animator逆再生+デフォルト方向選択＝ユーザー指示2（Task 4）、歯車・シャフトのみ一括変換＝ユーザー指示1（Task 5、機械系は対象外）、4方向QAの自動化（Task 6）— 全論点にタスクあり
- **Placeholder scan:** なし（全ステップに実コード・実コマンド・期待結果を記載）
- **Type consistency:** `Rotate(GearStateDetail, float deltaTime)` はTask 2で定義しTask 3/4/6で同シグネチャ使用。`GearRotationDirectionMode`/`AnimationPlayDirection`/`CalculateDirection`/`GetWorldAxisSign`/`ToAxisVector` の名前・引数はタスク間で一致
- **構造検査（spec-architecture-review）:** 冒頭「配置と前例」参照。全変更がClient層に閉じ、サーバー/Core無変更。SerializeReference互換のためnamespace固定を明記
