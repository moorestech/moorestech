# ElectricToGearGenerator モード選択UI（Plan 2-A）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** サーバー実装済みの `ElectricToGearGenerator` に、ブロックを開いて出力モード（rpm/torque/requiredPower の離散テーブル）を選択できるクライアントUIを実装する。

**Architecture:** サーバー `ElectricToGearGeneratorComponent` に `IBlockStateObservable` を足して mode/充足率変化を `BlockSystem` 経由でクライアントへ push。クライアントは `VanillaApiWithResponse` の新メソッドで `SetElectricToGearOutputModeProtocol` を送り、`IBlockInventoryView` 実装の View が master の `OutputModes` から行を生成、毎 `Update` で `GetStateDetail<ElectricToGearGeneratorBlockStateDetail>` をポーリングして選択・充足率・消費を反映する。UI prefab は `uloop execute-dynamic-code` で構築・Addressables 登録。

**Tech Stack:** C# / Unity / moorestech。サーバー: UniRx `Subject`、NUnit `CombinedTest`。クライアント: UniTask、UniRx、Addressables、`ClientDIContext.DIContainer.Instantiate`、`EditModeInPlayingTest`（PlayMode）。`uloop` CLI（compile / run-tests / execute-dynamic-code）。

**設計仕様:** `docs/superpowers/specs/2026-06-06-electric-to-gear-generator-mode-select-ui-design.md`

**前提（Plan 1 で実装済み）:** `ElectricToGearGeneratorComponent`（`SetSelectedMode(int)->bool` / `SelectedIndex` / `UpdateFulfillment` / `OutputModes` 経由の `CurrentMode`）、`ElectricToGearGeneratorBlockStateDetail`（key `"ElectricToGearGenerator"`、`SelectedIndex`/`ElectricFulfillmentRate`/`ConsumedElectricPower`）、`SetElectricToGearOutputModeProtocol`（`SetElectricToGearOutputModeRequest(Vector3Int, int)` / `SetElectricToGearOutputModeResponse{Success, AppliedIndex, FailureReason}`）。

**重要 gotcha（着手前に必読）:**
- `.cs` 編集後は必ず `uloop compile --project-path ./moorestech_client`。
- 新規サーバー/クライアント `.cs` も本セッションの実績では Unity 再起動不要で `uloop compile`/`run-tests` に拾われる（memory `server-tests-immutable-package` の追記参照）。拾われなければ再起動。
- 非ASCII（日本語）編集時は AGENTS.md「文字化け防止」。編集後 `git diff` で `縺 繧 繝 鬧 蛻 蜈` が出ないこと。
- try-catch 禁止。可変stateの単純 getter/setter 禁止（Set は `SetXxx`）。`#region Internal` はメソッド内ローカル関数専用。`.meta` 手動作成禁止（Unity 自動生成はコミット可）。Unity YAML（prefab/scene/asset）手編集禁止 → `uloop execute-dynamic-code` のみ。
- `EditModeInPlayingTest` は PlayMode（ドメインリロード）。`uloop run-tests` 直後はエラーを返すことがある → 45〜55秒待って `TestResults.xml`（`~/Library/Application Support/sakastudio/moorestech/TestResults.xml`）を読む（memory 参照）。

---

## 実装するファイル一覧

| 役割 | パス | 新規/変更 |
|---|---|---|
| サーバー状態push | `moorestech_server/Assets/Scripts/Game.Block/Blocks/ElectricToGear/ElectricToGearGeneratorComponent.cs` | 変更 |
| サーバーpushテスト | `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/ElectricToGearGeneratorTest.cs` | 変更 |
| クライアントAPI | `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs` | 変更 |
| 行View | `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/ElectricToGearOutputModeRowView.cs` | 新規 |
| UI本体View | `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/ElectricToGearGeneratorBlockInventoryView.cs` | 新規 |
| UI prefab | `moorestech_client/Assets/AddressableResources/UI/Block/ElectricToGearBlockInventory.prefab`（uloop構築） | 新規 |
| テストMod block | `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/blocks.json` | 変更 |
| テストMod item | `.../EditModeInPlayingTestMod/master/items.json` | 変更 |
| クライアントUIテスト | `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ElectricToGearModeSelectUITest.cs` | 新規 |

**タスク依存順:** Task 1（サーバーpush・独立）→ Task 2（API）→ Task 3（行View）→ Task 4（本体View、API/行に依存）→ Task 5（prefab、View script に依存）→ Task 6（テストMod、prefabパスに依存）→ Task 7（PlayModeテスト、全部に依存）。

---

## Task 1: サーバー — ElectricToGearGeneratorComponent に状態push（IBlockStateObservable）を追加

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/ElectricToGear/ElectricToGearGeneratorComponent.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/ElectricToGearGeneratorTest.cs`

> 手本は `FuelGearGeneratorComponent`（`new IObservable<Unit> OnChangeBlockState => _onChangeBlockState;` ＋ 独自 `Subject<Unit>`）。基底 `GearEnergyTransformer.OnChangeBlockState`（= `_simpleGearService.BlockStateChange`）は `virtual` でないため `new` で隠す。基底のギア状態変化も転送する（網由来の `CurrentRpm` 変化を取りこぼさない）。`SetSelectedMode` と `UpdateFulfillment`（充足率 or index が変わった時）で発火。

- [ ] **Step 1: 失敗するテストを書く**

`ElectricToGearGeneratorTest.cs` の using に追加（無ければ）:
```csharp
using Game.Block.Interface.Component;
using UniRx;
```
クラス内に追加:
```csharp
        [Test]
        public void ModeSwitchFiresOnChangeBlockState()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.TestElectricToGearGenerator, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);

            var observable = block.GetComponent<IBlockStateObservable>();
            var fired = 0;
            using var _ = observable.OnChangeBlockState.Subscribe(__ => fired++);

            // モード切替で状態変更が発火する（クライアントへ push されるトリガ）。
            // A mode switch fires a state change (the trigger that pushes to clients).
            var before = fired;
            block.GetComponent<ElectricToGearGeneratorComponent>().SetSelectedMode(2);
            Assert.Greater(fired, before, "SetSelectedMode で OnChangeBlockState が発火していない");
        }
```

- [ ] **Step 2: テスト実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricToGearGeneratorTest.ModeSwitchFiresOnChangeBlockState"`
Expected: FAIL（`block.GetComponent<IBlockStateObservable>()` は現状 base 実装を返し、`SetSelectedMode` では base subject が発火しないため `fired` が増えず Assert.Greater で失敗）。あるいはコンパイル前なら次 Step 後に赤を確認。

- [ ] **Step 3: コンポーネントに状態pushを実装**

`ElectricToGearGeneratorComponent.cs` を以下のように変更する。

(a) using に追加:
```csharp
using Game.Block.Interface.Component;
using UniRx;
```
(b) クラス宣言に `IBlockStateObservable` を追加:
```csharp
    public class ElectricToGearGeneratorComponent :
        GearEnergyTransformer, IGearGenerator, IElectricConsumer, IUpdatableBlockComponent, IBlockStateDetail, IBlockSaveState, IBlockStateObservable
```
(c) フィールドに Subject と転送購読の IDisposable を追加（既存フィールド群の近くに）:
```csharp
        // 自前の状態変更通知。BlockSystem がこれを購読し、変化時にクライアントへ state を push する。
        // Own state-change notifier; BlockSystem subscribes to it and pushes state to clients on change.
        private readonly Subject<Unit> _onChangeBlockState = new Subject<Unit>();
        private readonly System.IDisposable _baseStateForward;

        public new IObservable<Unit> OnChangeBlockState => _onChangeBlockState;
```
(d) 第1コンストラクタ（生成用）末尾で、基底のギア状態変化を自前 Subject へ転送する購読を張る:
```csharp
            _baseStateForward = base.OnChangeBlockState.Subscribe(_ => _onChangeBlockState.OnNext(Unit.Default));
```
> 注: セーブ復元コンストラクタは `this(param, ...)` を呼ぶので、この購読は両経路で張られる。
(e) `UpdateFulfillment` を「充足率 or 何か可変stateが変わったら発火」に変更:
```csharp
        // 供給電力を保存し、現在モードの requiredPower に対する充足率を再計算する。変化があれば状態 push を発火。
        // Store supplied power and recompute fulfillment; fire a state push if it changed.
        private void UpdateFulfillment(ElectricPower power)
        {
            _suppliedPower = power;
            var required = (float)CurrentMode.RequiredPower;
            var newRate = required > 0f ? Math.Min(power.AsPrimitive() / required, 1f) : 0f;
            if (Math.Abs(newRate - _electricFulfillmentRate) > 0.0001f)
            {
                _electricFulfillmentRate = newRate;
                _onChangeBlockState.OnNext(Unit.Default);
            }
            else
            {
                _electricFulfillmentRate = newRate;
            }
        }
```
(f) `SetSelectedMode` で index が変わったら発火（`UpdateFulfillment` は呼ぶが、index 変化自体も通知したい）:
```csharp
        public bool SetSelectedMode(int index)
        {
            BlockException.CheckDestroy(this);
            if (index < 0 || index >= _param.OutputModes.Length) return false;
            var changed = _selectedIndex != index;
            _selectedIndex = index;
            UpdateFulfillment(_suppliedPower);
            if (changed) _onChangeBlockState.OnNext(Unit.Default);
            return true;
        }
```
(g) コンポーネントの破棄処理で転送購読と Subject を破棄する。`GearEnergyTransformer` の破棄フック（`Destroy` メソッド等）を確認し、override 可能ならそこで `_baseStateForward?.Dispose(); _onChangeBlockState.Dispose();` を呼ぶ。破棄フックが `IBlockComponent.Destroy()` 等で存在する場合はそれを override（base 呼び出しを忘れない）。フックが無い/ややこしい場合は最小限、`_baseStateForward` を破棄しないと leak するため、`GearEnergyTransformer` の Destroy を必ず確認すること（FuelGearGenerator が Subject をどう破棄しているかも確認し、同じ方式に揃える）。

> 実装時に必ず `GearEnergyTransformer`（`GearEnergyTransformerComponent.cs`）と `FuelGearGeneratorComponent.cs` の破棄経路を Read し、Subject/購読の破棄を既存に合わせること。FuelGearGenerator が Subject を破棄していなければ（leak 許容なら）`_onChangeBlockState` も同様に放置し、`_baseStateForward` だけは破棄経路があれば破棄する。

- [ ] **Step 4: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。`new OnChangeBlockState` が base を隠し、`IBlockStateObservable` を満たす。

- [ ] **Step 5: テスト実行して通過を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricToGearGeneratorTest"`
Expected: 既存 ElectricToGearGeneratorTest 群 ＋ 新規 `ModeSwitchFiresOnChangeBlockState` が全 PASS。

- [ ] **Step 6: 文字化けチェック & Commit**

```bash
git diff -- moorestech_server/Assets/Scripts/Game.Block/Blocks/ElectricToGear/ElectricToGearGeneratorComponent.cs | grep -E "縺|繧|繝|鬧|蛻|蜈" && echo MOJIBAKE || echo clean
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/ElectricToGear/ElectricToGearGeneratorComponent.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/ElectricToGearGeneratorTest.cs
git commit -m "feat(server): ElectricToGearGenerator pushes state on mode/fulfillment change (IBlockStateObservable)"
```
（コミットメッセージ末尾に `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>` を付ける）

---

## Task 2: クライアントAPI — VanillaApiWithResponse にモード切替メソッドを追加

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs`

> 手本は同ファイルの `SetTrainPlatformTransferMode`（`_packetExchangeManager.GetPacketResponse<TResponse>(request, ct)`）。サーバープロトコル `SetElectricToGearOutputModeProtocol`（Plan 1）の Request/Response を使う。

- [ ] **Step 1: メソッドを追加**

`VanillaApiWithResponse.cs` の他の `Set...` メソッドが並ぶ箇所（`SetTrainPlatformTransferMode` 付近、285 行付近）に追加:
```csharp
        public async UniTask<SetElectricToGearOutputModeProtocol.SetElectricToGearOutputModeResponse> SetElectricToGearOutputMode(
            Vector3Int position, int index, CancellationToken ct)
        {
            var request = new SetElectricToGearOutputModeProtocol.SetElectricToGearOutputModeRequest(position, index);
            return await _packetExchangeManager.GetPacketResponse<SetElectricToGearOutputModeProtocol.SetElectricToGearOutputModeResponse>(request, ct);
        }
```

> 注: `SetElectricToGearOutputModeProtocol` は `Server.Protocol.PacketResponse` 名前空間。`VanillaApiWithResponse.cs` の既存 using に `Server.Protocol.PacketResponse`（または `SetTrainPlatformTransferModeProtocol` を参照している using）があるか確認し、無ければ追加。`Request` クラスの実コンストラクタは `SetElectricToGearOutputModeRequest(Vector3Int position, int index)`（Plan 1 実装済み）。実装時に `SetElectricToGearOutputModeProtocol.cs` を Read してクラス名・名前空間・コンストラクタ引数を最終確認すること。

- [ ] **Step 2: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。

- [ ] **Step 3: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs
git commit -m "feat(client): add VanillaApi SetElectricToGearOutputMode"
```
（Co-Authored-By 行を付ける）

---

## Task 3: クライアント — 行View（ElectricToGearOutputModeRowView）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/ElectricToGearOutputModeRowView.cs`

> 1 モード = 1 行。`Toggle`（`ToggleGroup` 配下）＋ ラベル（index / rpm / torque / requiredPower）。クリックで自分の index を `Subject` 発火。表示更新は `SetIsOnWithoutNotify` でループ防止。手本は `FilterSplitterDirectionColumnView`（plain MonoBehaviour、`Build(...)`、Subject 通知、`OnDestroy` で dispose）。

- [ ] **Step 1: 行Viewを作成**

```csharp
using System;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class ElectricToGearOutputModeRowView : MonoBehaviour
    {
        [SerializeField] private Toggle toggle;
        [SerializeField] private TMP_Text label;

        public IObservable<int> OnSelectRequested => _onSelectRequested;
        private readonly Subject<int> _onSelectRequested = new Subject<int>();

        private int _index;

        // 行を初期化。index と出力値を表示し、Toggle ON 操作で選択を通知する。
        // Initialize a row: show index and output values; notify selection when toggled on.
        public void Build(int index, double rpm, double torque, double requiredPower, ToggleGroup group)
        {
            _index = index;
            toggle.group = group;
            label.text = $"{index}:  {rpm:0}rpm   {torque:0}trq   {requiredPower:0}W";
            toggle.onValueChanged.AddListener(isOn =>
            {
                // ユーザー操作で ON になった時だけ送信（SetIsOnWithoutNotify では発火しない）。
                // Only emit when turned on by user action (SetIsOnWithoutNotify does not fire this).
                if (isOn) _onSelectRequested.OnNext(_index);
            });
        }

        // 通知を出さずに選択表示だけ更新する（StateDetail 反映用。送信ループを防ぐ）。
        // Update the selected display without firing (for StateDetail sync; prevents a send loop).
        public void SetSelectedWithoutNotify(bool selected)
        {
            toggle.SetIsOnWithoutNotify(selected);
        }

        private void OnDestroy()
        {
            _onSelectRequested.Dispose();
        }
    }
}
```

- [ ] **Step 2: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。`[SerializeField]` 参照は Task 5 の prefab 構築で wiring する。

- [ ] **Step 3: 文字化けチェック & Commit**

```bash
git diff -- moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/ElectricToGearOutputModeRowView.cs | grep -E "縺|繧|繝|鬧|蛻|蜈" && echo MOJIBAKE || echo clean
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/ElectricToGearOutputModeRowView.cs
git commit -m "feat(client): add ElectricToGearOutputModeRowView"
```
（Co-Authored-By 行を付ける）

---

## Task 4: クライアント — UI本体View（ElectricToGearGeneratorBlockInventoryView）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/ElectricToGearGeneratorBlockInventoryView.cs`

> `MonoBehaviour, IBlockInventoryView` を直接実装（インベントリ無し → no-op stub）。`Initialize` で master の `OutputModes` から行を生成、`Update` で `GetStateDetail` をポーリングして選択/充足率/消費を反映、行クリックで API 送信。手本: `GearEnergyTransformerUIView`（Update で `GetStateDetail` 読み）＋ `FilterSplitterBlockInventoryView`（no-inventory stub、行の動的生成、`_cts` ローカルキャプチャ、`DestroyUI`）。

- [ ] **Step 1: 本体Viewを作成**

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using Client.Common.Asset;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory.Element;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.ElectricToGear;
using Mooresmaster.Model.BlocksModule;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class ElectricToGearGeneratorBlockInventoryView : MonoBehaviour, IBlockInventoryView
    {
        [SerializeField] private Transform rowsParent;
        [SerializeField] private ElectricToGearOutputModeRowView rowTemplate;
        [SerializeField] private ToggleGroup toggleGroup;
        [SerializeField] private Slider fulfillmentBar;
        [SerializeField] private TMP_Text consumedPowerText;
        [SerializeField] private TMP_Text statusText;

        // インベントリ無しブロックのためのスタブ（FilterSplitter と同形）。
        // Stubs for a no-inventory block (same shape as FilterSplitter).
        public IReadOnlyList<ItemSlotView> SubInventorySlotObjects => Array.Empty<ItemSlotView>();
        public List<IItemStack> SubInventory { get; } = new();
        public int Count => 0;
        public ISubInventoryIdentifier ISubInventoryIdentifier { get; } = null;

        private readonly List<ElectricToGearOutputModeRowView> _rows = new();
        private BlockGameObject _blockGameObject;
        private Vector3Int _blockPosition;
        private bool _isSending;
        private bool _initialized;

        public void Initialize(BlockGameObject blockGameObject)
        {
            _blockGameObject = blockGameObject;
            _blockPosition = blockGameObject.BlockPosInfo.OriginalPos;

            var param = blockGameObject.BlockMasterElement.BlockParam as ElectricToGearGeneratorBlockParam;
            if (param == null)
            {
                statusText.text = "invalid block param";
                Debug.LogError("[ElectricToGearGeneratorBlockInventoryView] BlockParam is not ElectricToGearGeneratorBlockParam");
                return;
            }

            BuildRows(param.OutputModes);
            rowTemplate.gameObject.SetActive(false);
            statusText.text = "未同期 / not synced";
            _initialized = true;

            #region Internal

            void BuildRows(OutputModesElement[] modes)
            {
                for (var i = 0; i < modes.Length; i++)
                {
                    var row = Instantiate(rowTemplate, rowsParent);
                    row.gameObject.SetActive(true);
                    row.Build(i, modes[i].Rpm, modes[i].Torque, modes[i].RequiredPower, toggleGroup);
                    var capturedIndex = i;
                    row.OnSelectRequested.Subscribe(idx => OnRowSelected(idx)).AddTo(row);
                    _rows.Add(row);
                }
            }

            #endregion
        }

        private void Update()
        {
            if (!_initialized) return;

            var state = _blockGameObject.GetStateDetail<ElectricToGearGeneratorBlockStateDetail>(
                ElectricToGearGeneratorBlockStateDetail.BlockStateDetailKey);
            if (state == null)
            {
                statusText.text = "未同期 / not synced";
                return;
            }

            statusText.text = string.Empty;
            for (var i = 0; i < _rows.Count; i++)
            {
                _rows[i].SetSelectedWithoutNotify(i == state.SelectedIndex);
            }
            fulfillmentBar.value = state.ElectricFulfillmentRate;
            consumedPowerText.text = $"{state.ConsumedElectricPower:0} W";
        }

        // 行が選ばれたらサーバーへ送る。二重送信は無視。応答失敗時は何もしない（次 Update で真値へ戻る）。
        // On row select, send to server. Ignore double-sends. On failure do nothing (next Update reverts to truth).
        private void OnRowSelected(int index)
        {
            if (_isSending) return;
            SendAsync(index).Forget();

            #region Internal

            async UniTask SendAsync(int idx)
            {
                _isSending = true;
                var ct = this.GetCancellationTokenOnDestroy();
                var response = await Client.Game.InGame.Context.ClientContext.VanillaApi.Response.SetElectricToGearOutputMode(_blockPosition, idx, ct);
                // 破棄後は UI を触らない。
                // Don't touch UI after destroy.
                if (this == null) return;
                _isSending = false;
                // 成功/失敗いずれも表示は次 Update の StateDetail に従う（楽観更新しない）。
                // Display follows StateDetail next Update regardless of success/failure (no optimistic update).
            }

            #endregion
        }

        public void UpdateItemList(List<IItemStack> items) { }
        public void UpdateInventorySlot(int slot, IItemStack item) { }

        public void DestroyUI()
        {
            Destroy(gameObject);
        }
    }
}
```

> 実装時に確認・調整すること（手本ファイルを Read）:
> - `ISubInventory` の必須メンバー（`SubInventorySlotObjects`/`SubInventory`/`Count`/`ISubInventoryIdentifier`）の**正確な型と名前**を `FilterSplitterBlockInventoryView.cs:32-37` と `ISubInventory` 定義で確認し、過不足なく実装する。`ItemSlotView` の名前空間（`Client.Game.InGame.UI.Inventory.Element` 等）も実物に合わせる。
> - `ClientContext` の名前空間（`Client.Game.InGame.Context`）と `VanillaApi.Response` への正しい参照を `FilterSplitterBlockInventoryView` の呼び出しに合わせる（using を整える）。
> - `this.GetCancellationTokenOnDestroy()`（UniTask 拡張）を使う。`FilterSplitter` のような明示 `_cts` 管理にしたい場合はそちらに合わせてもよいが、本 View は破棄=GameObject破棄なので `GetCancellationTokenOnDestroy` で十分。
> - `OutputModesElement` のプロパティは `Rpm`/`Torque`/`RequiredPower`（`float`）。`Build` の引数型は `double` で受けているが `float`→`double` 暗黙変換で問題ない。厳密にしたければ `float` に揃える。

- [ ] **Step 2: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。エラーが出たら手本（FilterSplitter/GearEnergyTransformerUIView）の実シグネチャに合わせて修正。

- [ ] **Step 3: 文字化けチェック & Commit**

```bash
git diff -- moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/ElectricToGearGeneratorBlockInventoryView.cs | grep -E "縺|繧|繝|鬧|蛻|蜈" && echo MOJIBAKE || echo clean
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/ElectricToGearGeneratorBlockInventoryView.cs
git commit -m "feat(client): add ElectricToGearGeneratorBlockInventoryView"
```
（Co-Authored-By 行を付ける）

---

## Task 5: UI prefab を uloop execute-dynamic-code で構築し Addressables 登録

**Files:**
- Create: `moorestech_client/Assets/AddressableResources/UI/Block/ElectricToGearBlockInventory.prefab`（Unity 生成）
- Addressables: `Vanilla Asset Group` に address `Vanilla/UI/Block/ElectricToGearBlockInventory` で登録

> prefab は手編集禁止。`uloop execute-dynamic-code` で GameObject 階層を組み、View/行 script を AddComponent、`[SerializeField]` を SerializedObject で wiring、`PrefabUtility.SaveAsPrefabAsset` で保存、`AddressableAssetSettings.CreateOrMoveEntry` でアドレス設定。**1 回で完璧を狙わず、保存→確認→修正の反復前提**。見た目の精緻化はスコープ外。

- [ ] **Step 1: prefab 構築コードを実行**

以下を `uloop execute-dynamic-code --project-path ./moorestech_client --code '...'` で実行する（長いので段階実行可。下記は要点。実行時は実際の型名・名前空間を `using` で揃える）。構築内容:
1. ルート GameObject `ElectricToGearBlockInventory`（`RectTransform` ＋ 背景 `Image` ＋ `VerticalLayoutGroup`）。ルートに `ElectricToGearGeneratorBlockInventoryView` を `AddComponent`。
2. 子 `Rows`（`RectTransform` ＋ `VerticalLayoutGroup` ＋ `ToggleGroup`）= 行の親。
3. 子 `RowTemplate`（`RectTransform` ＋ `Toggle` ＋ 子 `Label`(`TextMeshProUGUI`)）に `ElectricToGearOutputModeRowView` を AddComponent。RowTemplate は最初 `SetActive(false)`。Toggle の `targetGraphic`/checkmark と Label を行 View の `toggle`/`label` に wiring。
4. 子 `FulfillmentBar`（`Slider`、min 0 max 1）、`ConsumedPowerText`（`TextMeshProUGUI`）、`StatusText`（`TextMeshProUGUI`）。
5. SerializedObject で本体 View の `rowsParent=Rows`, `rowTemplate=RowTemplate の RowView`, `toggleGroup=Rows の ToggleGroup`, `fulfillmentBar`, `consumedPowerText`, `statusText` を wiring。
6. `PrefabUtility.SaveAsPrefabAsset(root, "Assets/AddressableResources/UI/Block/ElectricToGearBlockInventory.prefab")`、その後シーン上の一時 root を `Object.DestroyImmediate`。
7. Addressables 登録:
```csharp
var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
var group = settings.FindGroup("Vanilla Asset Group");
var guid = UnityEditor.AssetDatabase.AssetPathToGUID("Assets/AddressableResources/UI/Block/ElectricToGearBlockInventory.prefab");
var entry = settings.CreateOrMoveEntry(guid, group);
entry.address = "Vanilla/UI/Block/ElectricToGearBlockInventory";
UnityEditor.AssetDatabase.SaveAssets();
```
> 実装メモ: TMP テキストは `TextMeshProUGUI`。TMP の既定フォントは `TMP_Settings.defaultFontAsset` でよい（明示参照不要）。`RectTransform` のサイズ/アンカーは既存 `GearEnergyTransformerUI.prefab` を `uloop execute-dynamic-code` で読み出して数値を流用してよい。`Toggle.group` は行 View の `Build` で実行時に設定するので prefab 側 wiring は不要（RowTemplate の Toggle に group 未設定で可）。実行は分割し、各段階で返り値（生成オブジェクト名やエラー）を確認する。

- [ ] **Step 2: 登録を検証**

Run: `uloop execute-dynamic-code --project-path ./moorestech_client --code 'var s=UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings; foreach(var g in s.groups){ foreach(var e in g.entries){ if(e.address=="Vanilla/UI/Block/ElectricToGearBlockInventory") return e.AssetPath; } } return "NOT FOUND";'`
Expected: `Assets/AddressableResources/UI/Block/ElectricToGearBlockInventory.prefab` が返る。

- [ ] **Step 3: コンパイル（.meta 生成確認）**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。prefab と `.meta`、Addressables 設定差分が生成される。

- [ ] **Step 4: Commit**

```bash
git add moorestech_client/Assets/AddressableResources/UI/Block/ElectricToGearBlockInventory.prefab moorestech_client/Assets/AddressableResources/UI/Block/ElectricToGearBlockInventory.prefab.meta moorestech_client/Assets/AddressableAssetsData
git commit -m "feat(client): build ElectricToGearBlockInventory UI prefab + addressables entry"
```
（Co-Authored-By 行を付ける。Addressables 設定ファイルの差分も含めること）

---

## Task 6: テストMod（EditModeInPlayingTestMod）に UI 付きブロックを追加

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/blocks.json`
- Modify: `.../EditModeInPlayingTestMod/master/items.json`

> 既存 `Shaft`（`blockUIAddressablesPath: "Vanilla/UI/Block/GearEnergyTransformerUI"`）が同型の前例。新ブロックは `blockType: "ElectricToGearGenerator"`、`blockParam` に `teethCount`/`outputModes`(3件)/`gear`、`blockUIAddressablesPath` に Task 5 のアドレスを設定。GUID は未使用を確認して使う。

- [ ] **Step 1: 未使用 GUID を確認**

Run: `grep -c "00000000-0000-0000-0000-0000000000a1" moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/blocks.json; grep -c "11110a01-0000-0000-0000-000000000000" moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/items.json`
Expected: 両方 0（未使用）。使用済みなら別の空き GUID にする。

- [ ] **Step 2: items.json にアイテム追加**

`items.json` の `data` 配列末尾付近に既存要素の体裁で追加:
```json
{
  "blockSize": [1, 1, 1],
  "initialUnlocked": true,
  "sortPriority": 950,
  "maxStack": 100,
  "name": "TestElectricToGearGeneratorUI",
  "recipeViewType": "IsUnlocked",
  "itemGuid": "11110a01-0000-0000-0000-000000000000"
}
```
> 注: items.json の既存要素の**実際のフィールド集合**を Read して過不足なく合わせる（`imagePath`/`handGrabModelAddressablePath` 等が必要なら付ける）。

- [ ] **Step 3: blocks.json にブロック追加**

`blocks.json` の `data` 配列に追加（`gear` は既存 `Shaft`/`GearToElectric` の `gear` 構造を流用、`connectorGuid` は既存値流用可）:
```json
{
  "initialUnlocked": true,
  "sortPriority": 950,
  "maxStack": 100,
  "name": "TestElectricToGearGeneratorUI",
  "blockSize": [1, 1, 1],
  "blockType": "ElectricToGearGenerator",
  "blockParam": {
    "teethCount": 10,
    "outputModes": [
      { "rpm": 60, "torque": 50, "requiredPower": 30 },
      { "rpm": 120, "torque": 100, "requiredPower": 100 },
      { "rpm": 240, "torque": 150, "requiredPower": 300 }
    ],
    "gear": {
      "gearConnects": [
        {
          "offset": [0, 0, 0],
          "connectType": "Gear",
          "connectOption": { "isReverse": false },
          "directions": [[0, 0, -1], [0, 0, 1], [1, 0, 0], [-1, 0, 0]],
          "connectorGuid": "72231375-1dcb-4af7-8314-9eaa783116e9"
        }
      ]
    }
  },
  "itemGuid": "11110a01-0000-0000-0000-000000000000",
  "blockGuid": "00000000-0000-0000-0000-0000000000a1",
  "blockPrefabAddressablesPath": "",
  "blockUIAddressablesPath": "Vanilla/UI/Block/ElectricToGearBlockInventory",
  "overrideVerticalBlock": {}
}
```
> 注: blocks.json 既存要素（特に `Shaft` ブロック）の**実フィールド集合**を Read して、トップレベルキー（`blockPrefabAddressablesPath` の要否等）を過不足なく合わせる。`gear` の中身も既存 gear ブロックから正確にコピーする。

- [ ] **Step 4: JSON 検証 & コンパイル**

```bash
python3 -c "import json;json.load(open('moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/blocks.json'));json.load(open('moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/items.json'));print('JSON OK')"
```
Run: `uloop compile --project-path ./moorestech_client`
Expected: JSON OK、コンパイルエラー0。

- [ ] **Step 5: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/blocks.json moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/items.json
git commit -m "test(client): add TestElectricToGearGeneratorUI block with mode-select UI to EditModeInPlayingTestMod"
```
（Co-Authored-By 行を付ける）

---

## Task 7: クライアント PlayMode テスト（往復 ＋ スモーク）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ElectricToGearModeSelectUITest.cs`

> OS入力クリックは使わない（脆い）。実ブロックをサーバーに設置→クライアント `BlockGameObject` を取得→ UI prefab を `ClientDIContext.DIContainer.Instantiate` で生成→ `Initialize`→ 対象行の選択を起動→サーバー component の `SelectedIndex` 変化を検証。別途スモーク（prefab ロード＋ルートに View）。手本: `PlayerMovementTest.cs`、`EditModeInPlayingTestUtil`（`LoadMainGame`/`PlaceBlock`）、`SubInventoryState`（Instantiate 経路）。

- [ ] **Step 1: テストを書く**

```csharp
using System;
using System.Threading;
using Client.Common.Asset;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Block;
using Client.Tests.EditModeInPlayingTest.Util;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.ElectricToGear;
using Game.Context;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.EditModeInPlayingTest
{
    public class ElectricToGearModeSelectUITest
    {
        private const string BlockName = "TestElectricToGearGeneratorUI";
        private const string UiAddress = "Vanilla/UI/Block/ElectricToGearBlockInventory";

        // prefab がロードでき、ルートに View が付いていることのスモーク。
        // Smoke: the prefab loads and its root has the View.
        [UnityTest]
        public IEnumerator PrefabLoadsAndHasRootView() => UniTask.ToCoroutine(async () =>
        {
            EditModeInPlayingTestUtil.EnterPlayModeUtil();
            await EditModeInPlayingTestUtil.LoadMainGame();

            using var loaded = await AddressableLoader.LoadAsync<GameObject>(UiAddress, CancellationToken.None);
            Assert.IsNotNull(loaded.Asset, "UI prefab がロードできない");
            var view = loaded.Asset.GetComponent<ElectricToGearGeneratorBlockInventoryView>();
            Assert.IsNotNull(view, "prefab ルートに View が無い（SubInventoryState.GetComponent<ISubInventoryView> が null になる）");
        });

        // 行選択 → サーバー component の SelectedIndex が変わる往復。
        // Round-trip: selecting a row changes the server component's SelectedIndex.
        [UnityTest]
        public IEnumerator RowSelectChangesServerSelectedIndex() => UniTask.ToCoroutine(async () =>
        {
            EditModeInPlayingTestUtil.EnterPlayModeUtil();
            await EditModeInPlayingTestUtil.LoadMainGame();

            var pos = new Vector3Int(0, 0, 0);
            var serverBlock = EditModeInPlayingTestUtil.PlaceBlock(BlockName, pos, BlockDirection.North);
            var component = serverBlock.GetComponent<ElectricToGearGeneratorComponent>();
            Assert.AreEqual(0, component.SelectedIndex);

            // クライアント GameObject が生成されるまで数フレーム待つ。
            // Wait a few frames for the client GameObject to spawn.
            BlockGameObject blockGameObject = null;
            for (var i = 0; i < 30 && blockGameObject == null; i++)
            {
                ClientContext.BlockGameObjectDataStore.TryGetBlockGameObject(pos, out blockGameObject);
                await UniTask.Yield();
            }
            Assert.IsNotNull(blockGameObject, "client BlockGameObject が生成されない");

            // UI prefab を DI 経由で生成し、Initialize。
            // Instantiate the UI prefab via DI and Initialize.
            using var loaded = await AddressableLoader.LoadAsync<GameObject>(UiAddress, CancellationToken.None);
            var instance = ClientDIContext.DIContainer.Instantiate(loaded.Asset);
            var view = instance.GetComponent<ElectricToGearGeneratorBlockInventoryView>();
            view.Initialize(blockGameObject);

            // index 2 の行を選択（行 View の選択通知を起動）。
            // Select row index 2 (drive the row view's selection notification).
            view.SelectModeForTest(2);

            // サーバー反映を待って検証。
            // Wait for server application and assert.
            for (var i = 0; i < 60 && component.SelectedIndex != 2; i++)
            {
                await UniTask.Yield();
            }
            Assert.AreEqual(2, component.SelectedIndex, "行選択がサーバー SelectedIndex に反映されない");

            UnityEngine.Object.Destroy(instance);
        });
    }
}
```

- [ ] **Step 2: テスト用フックを View に追加**

`ElectricToGearGeneratorBlockInventoryView.cs` に、テストから行選択を起動するための内部メソッドを追加（行クリック相当を呼ぶ。`OnRowSelected` を public 化せず最小フックにする）。ファイル末尾の `DestroyUI` 付近に追加:
```csharp
#if UNITY_EDITOR
        // テスト用: 指定 index の選択操作をプログラムから起動する（行クリック相当）。
        // Test-only: drive a selection for the given index (equivalent to a row click).
        public void SelectModeForTest(int index)
        {
            OnRowSelected(index);
        }
#endif
```
> AGENTS.md: エディタ専用コードは `#if UNITY_EDITOR` で囲みファイル末尾に配置。テストアセンブリは Editor 環境で動くので `UNITY_EDITOR` で可。`OnRowSelected` は private のままでよい（同クラス内から呼ぶ）。
> 代替: フックを足したくない場合は、テストで `instance.GetComponentInChildren<ElectricToGearOutputModeRowView>()` から対象行の `toggle.isOn = true`（onValueChanged 経由で送信）を起動する方法もある。どちらか確実な方を採用（実装時に View 構造に合わせて決める）。

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。

- [ ] **Step 3: PlayMode テストを実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricToGearModeSelectUITest"`
> ドメインリロードで `uloop` がエラーを返したら 55 秒待って結果を読む:
```bash
sleep 55; python3 -c "
import xml.etree.ElementTree as ET
t=ET.parse('$HOME/Library/Application Support/sakastudio/moorestech/TestResults.xml'); r=t.getroot()
fails=[(tc.get('fullname'),tc.get('result')) for tc in r.iter('test-case') if tc.get('result') not in ('Passed','Skipped','Inconclusive')]
print('run:',{k:r.get(k) for k in ('total','passed','failed')}); print('fails:',fails[:20])
"
```
Expected: `PrefabLoadsAndHasRootView` と `RowSelectChangesServerSelectedIndex` が PASS。失敗時はメッセージを読み、wiring（prefab の SerializeField）・DI・待機フレーム数を調整。

- [ ] **Step 4: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ElectricToGearModeSelectUITest.cs moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/ElectricToGearGeneratorBlockInventoryView.cs
git commit -m "test(client): PlayMode mode-select UI round-trip + prefab smoke"
```
（Co-Authored-By 行を付ける）

---

## Self-Review メモ（計画作成者による確認）

- **Spec カバレッジ:** マスタ/StateDetail分離（Task 4）／サーバー状態push（Task 1）／インベントリ無しUI土台（Task 4）／単一prefab＋行テンプレート（Task 5）／API（Task 2）／行View（Task 3）／テストデータ＝EditModeInPlayingTestMod（Task 6）／PlayMode往復＋スモーク（Task 7）。Codex監査の3要修正（楽観更新撤廃→push、テストMod配置、OS入力廃止）すべて反映。
- **型整合:** `SetElectricToGearOutputMode(Vector3Int,int,ct)`（Task 2）／`ElectricToGearOutputModeRowView.Build(int,double,double,double,ToggleGroup)` `OnSelectRequested`(`IObservable<int>`) `SetSelectedWithoutNotify(bool)`（Task 3）／本体 View の `Initialize`/`Update`/`OnRowSelected`/`SelectModeForTest`（Task 4,7）／StateDetail key `"ElectricToGearGenerator"`・プロパティ `SelectedIndex`/`ElectricFulfillmentRate`/`ConsumedElectricPower`（Plan 1）／prefab address `Vanilla/UI/Block/ElectricToGearBlockInventory`（Task 5,6,7）一致。
- **実装時に必ず実コード確認（計画は手本ベースの仮定を含む）:** ① `GearEnergyTransformer`/`FuelGearGeneratorComponent` の Subject/購読**破棄経路**（Task 1 Step 3g）。② `ISubInventory` 必須メンバーの正確な型（Task 4）。③ `ItemSlotView`/`ClientContext`/`ClientDIContext`/`BlockGameObjectDataStore` の名前空間とメソッド名（Task 4,7）。④ `VanillaApiWithResponse` の using と Request コンストラクタ（Task 2）。⑤ EditModeInPlayingTestMod の既存 JSON フィールド集合（Task 6）。⑥ `EditModeInPlayingTestUtil.PlaceBlock` の戻り値型と `BlockDirection` の名前空間（Task 7）。各タスク内に確認手順を明記済み。
- **リスク:** Task 5（uloop prefab 構築）と Task 7（PlayMode 往復）は不確実性が高く反復前提。prefab の SerializeField wiring 漏れが Task 7 で顕在化するので、Task 5 で `entry.AssetPath` 検証＋ Task 7 スモークで早期検出する。
