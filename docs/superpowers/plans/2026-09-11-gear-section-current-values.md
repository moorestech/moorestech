# 歯車セクションの現在値報告化 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Web UI ブロック詳細の歯車セクションを「消費トルク／発生トルクの現在値」と「RPM（消費側は基準併記）」の報告に改め、旧比例配分モデル由来の「現在<基準で赤」を全廃する（ADR 0056）。

**Architecture:** サーバーの `GearStateDetail` は変えない。クライアントの dto 生成（`Client.WebUiHost`）が「役割（消費/発生）」をマスタ param の `IGearConsumptionParam` 実装有無から導出して `GearDetailDto` に載せ、`BaseTorque` を落とす。Web UI `GearSection.tsx` は役割で文言を出し分け、赤判定を持たない `Text` で描く。データフローは「サーバー state → dto ビルダ（書き手）→ Web UI（読み手）」の既存一方向のまま。

**Tech Stack:** C# (Unity, Client.WebUiHost dto / NUnit 契約テスト) / TypeScript (React + Mantine, zod 契約, vitest, Playwright e2e) / `Localization/localization.csv` → `npm run gen:i18n`

## Requirements

設計ADR: `docs/adr/0056-gear-section-reports-current-values-without-satisfaction-comparison.md`（用語: `CONTEXT.md` 「歯車」節）

- R1 消費側（`IGearConsumptionParam` を持つブロック全部: 歯車機械・歯車ベルト・歯車採掘機・歯車ポンプ・歯車・シャフト・チェーンポール・回転発電機・フィルター分岐器）のトルク行は「消費トルク {現在値}」のみ。分母も赤判定も無い。受け入れ: 待機中（消費トルクが基準より小さい）でも `data-insufficient` 属性が付かず、文言に「/」が無い。
- R2 発電機側（`IGearConsumptionParam` を持たないブロック: 風車・蒸気機関・回転生成機）のトルク行は「発生トルク {現在値}」。受け入れ: 同じ `gear-torque` testId で文言だけが変わり、「/ 0.0」が出ない。
- R3 消費側の RPM 行は「RPM {現在} / {基準RPM}」の並記を維持し、赤判定は廃止。受け入れ: 現在<基準でも `data-insufficient` が付かない。
- R4 発電機側の RPM 行は「RPM {現在}」のみ。受け入れ: 文言に「/」が無い。
- R5 役割（消費/発生）はクライアントの dto ビルダがマスタ param から導出し、`GearDetailDto.Role` に `"consumer"` / `"generator"` の文字列で載せる。`GearStateDetail`（サーバー）は変更しない。
- R6 `GearDetailDto` から `BaseTorque` を落とす（`BaseRpm` は残す）。契約 fixture・zod スキーマ・e2e モックがすべて新形状に追従する。
- R7 純伝達ブロック（チェーンポール等、基準トルク0）も同じ行構成で出す（「消費トルク 0.0」のまま）。クライアントに基準トルク0の分岐を作らない。
- R8 歯車機械の機械行（稼働状態ラベル＋充足率）は現状維持。`MachineSection` / `MachineStateRow` / `detailLogic.machineStateDisplay` は触らない。
- R9 不足の赤は `GearNetworkSection` の停止理由行だけ。`GearSection` は `LackHighlightText` を使わない。
- R10 ローカライズ: `ui.blockInventory.gearTorqueSummary` / `gearRpmSummary` を廃止し、`gearConsumedTorque` / `gearGeneratedTorque` / `gearRpmWithBase` / `gearRpmCurrent` の4キーを `Localization/localization.csv` に4言語列（Source, english, japanese, german）で追加。`npm run gen:i18n` で `localizationKeys.ts` を再生成。
- R11 テスト追従: `WireContractBlockDetailTest.GearMachineFixtureMatchesDto` と fixture JSON、`wireContract.test.ts` / `validators.test.ts`、e2e `blockDetails.spec.ts` の「トルク」断言、e2e モック fixture 3件（gearMachine / gearMiner / gearPump）。
- R12 e2e に発電機側の代表ケース（`gearGenerator` type、blockType `SimpleGearGenerator`）を追加し「発生トルク」文言を断言する。
- やらないこと（スコープ外・ADR裁定済み）: 待機中ラベル／要求倍率の併記、回転方向（時計/反時計）の表示、歯車網の負荷率表示、`GearStateDetail` の拡張、ADR 0010 本文の改訂、目視QA撮影スクリプト `e2e/capture-machine-qa.ts` の変更（同スクリプトは歯車行の文言を読んでいないため追従不要。撮影結果の PNG に新文言が写るだけ）。

## Global Constraints

- AGENTS.md 規約: 1ファイル200行以下、partial 禁止、`Func<>` 禁止、try-catch 禁止、デフォルト引数禁止、単純 getter/setter 禁止（public フィールド dto は既存 `BlockDetailDtos.cs` の流儀に従う）、コメントは日本語→英語の2行セット、`#region Internal` はメソッド内ローカル関数のみ。
- .cs 変更後は必ず `uloop compile --project-path ./moorestech_client`。
- Web UI は webui-design スキルのホワイトリストに従う（`Text` の色は `c="var(--text-default)"`、不足色は `LackHighlightText` 経由のみ）。
- 文字列 enum の dto 表現は既存の `ToCamelCase` 流儀（`currentState: "idle"`, `stopReason: "overRequirePower"`）に合わせ、camelCase 小文字始まりの文字列。
- 歯車の役割判別は「スキーマの `IGearConsumptionParam` が正本（具体型の列挙はしない）」（既存 `BlockDetailDtoBuilder.GetGearConsumption` のコメントを継承）。
- 作業ブランチ: `feature/gear-section-current-values`（worktree `/Users/katsumi/moorestech-gear-section`、origin/master 6c34a5d96 起点）。Unity を開く前にメイン worktree の `Library/` をコピーする（AGENTS.md）。他 worktree の PlayMode と同時実行しない。
- Beads: `moorestech-0tf8`（着手済み）。完了時 `bd close moorestech-0tf8 --reason="..."`。

---

### Task 1: dto の役割付与と BaseTorque 廃止（C# 側）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockDetailDtos.cs:109-116`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/GearDetailDtoBuilder.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockDetailDtoBuilder.cs:1-11, 73-88, 157-162`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractBlockDetailTest.cs:57`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/block_inventory_gear_machine.json:23`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractBlockDetailTest.cs`

**Interfaces:**
- Consumes: `BlockGameObject.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey)`（既存）、`IGearConsumptionParam.GearConsumption.BaseRpm`（生成マスタ）
- Produces: `GearDetailDto { bool IsClockwise; float CurrentRpm; float CurrentTorque; float BaseRpm; string Role; }`、`GearDetailDtoBuilder.ConsumerRole == "consumer"`、`GearDetailDtoBuilder.GeneratorRole == "generator"`、`static void GearDetailDtoBuilder.Apply(BlockInventoryDto dto, BlockGameObject block, object param)`。JSON は `{"isClockwise":..,"currentRpm":..,"currentTorque":..,"baseRpm":..,"role":"consumer"}`（Task 2 の zod スキーマがこの形を要求する）

- [ ] **Step 1: 契約 fixture を新形状に書き換える（失敗するテストを先に作る）**

`moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/block_inventory_gear_machine.json` の `"gear"` 行を置換:

```json
  "gear": { "isClockwise": true, "currentRpm": 12.5, "currentTorque": 3.0, "baseRpm": 20.0, "role": "consumer" },
```

`WireContractBlockDetailTest.cs:57` を置換:

```csharp
                Gear = new GearDetailDto { IsClockwise = true, CurrentRpm = 12.5f, CurrentTorque = 3f, BaseRpm = 20f, Role = GearDetailDtoBuilder.ConsumerRole },
```

- [ ] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー（`GearDetailDto` に `Role` が無い、`GearDetailDtoBuilder` が未定義）

- [ ] **Step 3: dto を変更する**

`BlockDetailDtos.cs:109-116` の `GearDetailDto` を置換:

```csharp
    public class GearDetailDto
    {
        public bool IsClockwise;
        public float CurrentRpm;
        public float CurrentTorque;
        public float BaseRpm;
        // 消費側か発生側か。GearDetailDtoBuilder.ConsumerRole / GeneratorRole の文字列
        // Consumer or generator; one of GearDetailDtoBuilder.ConsumerRole / GeneratorRole
        public string Role;
    }
```

- [ ] **Step 4: GearDetailDtoBuilder を新設する**

`moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/GearDetailDtoBuilder.cs`:

```csharp
using Client.Game.InGame.Block;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// 歯車の現在値と役割（消費/発生）を capability DTO へ充填する
    /// Fills the gear's current values and its role (consumer/generator) into the capability DTO
    /// </summary>
    public static class GearDetailDtoBuilder
    {
        public const string ConsumerRole = "consumer";
        public const string GeneratorRole = "generator";

        public static void Apply(BlockInventoryDto dto, BlockGameObject block, object param)
        {
            var gear = block.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey);
            if (gear == null) return;

            // 役割の正本はスキーマの IGearConsumptionParam（具体型の列挙はしない）。持たないブロックは発電機
            // The schema's IGearConsumptionParam is the authority on role (no concrete-type enumeration); blocks without it are generators
            var isConsumer = param is IGearConsumptionParam;
            var baseRpm = isConsumer ? (float)((IGearConsumptionParam)param).GearConsumption.BaseRpm : 0f;
            dto.Gear = new GearDetailDto
            {
                IsClockwise = gear.IsClockwise,
                CurrentRpm = gear.CurrentRpm,
                CurrentTorque = gear.CurrentTorque,
                BaseRpm = baseRpm,
                Role = isConsumer ? ConsumerRole : GeneratorRole,
            };
        }
    }
}
```

- [ ] **Step 5: BlockDetailDtoBuilder から歯車ブロックを委譲に置き換える**

`BlockDetailDtoBuilder.cs:73-88`（「// ギア: GearStateDetail + マスタ GearConsumption（要求値）」から `}` まで）を置換:

```csharp
            // ギア: GearDetailDtoBuilderが役割と現在値を算出
            // Gears: GearDetailDtoBuilder derives the role and current values
            GearDetailDtoBuilder.Apply(dto, block, param);
```

`BlockDetailDtoBuilder.cs:157-162` の `GetGearConsumption` メソッド全体（コメント2行含む）を削除する。

`BlockDetailDtoBuilder.cs:1-11` の using から次の2行を削除する（他に参照が無いことを `grep -n "GearStateDetail\|GearConsumption" BlockDetailDtoBuilder.cs` で確認。0件になること）:

```csharp
using Game.Gear.Common;
using Mooresmaster.Model.GearConsumptionModule;
```

- [ ] **Step 6: コンパイルして通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。`BlockDetailDtoBuilder.cs` は 165 行以下。
- `GearDetailDtoBuilder.cs` で `IGearConsumptionParam` が CS0246 になる場合は `using Mooresmaster.Model.GearConsumptionModule;` を追加する（生成コードの名前空間は BlocksModule / GearConsumptionModule のどちらかで、旧 `BlockDetailDtoBuilder` は両方を using していた）
- `BlockDetailDtoBuilder.cs` で using 削除により CS0246 が出た場合は、その型を含む using だけ戻す（削除は「未使用なら消す」の意図）

- [ ] **Step 7: 契約テストを実行する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WireContractBlockDetailTest"`
Expected: 全件 PASS（`GearMachineFixtureMatchesDto` が新 fixture と一致）

- [ ] **Step 8: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockDetailDtos.cs \
        moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/GearDetailDtoBuilder.cs \
        moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockDetailDtoBuilder.cs \
        moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractBlockDetailTest.cs \
        moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/block_inventory_gear_machine.json
git commit -m "feat(webui-host): 歯車dtoに役割(消費/発生)を載せBaseTorqueを落とす (ADR 0056)"
```

（`GearDetailDtoBuilder.cs.meta` が Unity 起動で生成されていれば一緒に add する。手で作らない。）

---

### Task 2: Web 契約（zod）と e2e モック fixture の追従

**Files:**
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/inventory.ts:86-88`
- Modify: `moorestech_web/webui/src/bridge/contract/payloadTypes.ts:113`
- Modify: `moorestech_web/webui/src/bridge/contract/validators.test.ts:103`
- Modify: `moorestech_web/webui/e2e/mock-host/blockDetailFixtures.ts:66, 111, 153`
- Modify: `moorestech_web/webui/e2e/mock-host/fixtures/blockLocalizationFixtures.ts:17, 36`
- Modify: `moorestech_web/webui/e2e/mock-host/httpHandler.ts:33`
- Test: `moorestech_web/webui/src/bridge/contract/validators.test.ts`, `moorestech_web/webui/src/bridge/contract/wireContract.test.ts`

**Interfaces:**
- Consumes: Task 1 の JSON 形 `{isClockwise, currentRpm, currentTorque, baseRpm, role}`
- Produces: `GearRoleSchema = z.enum(["consumer","generator"])`、型 `GearRole`、`GearDetailData.role: GearRole`、`GearDetailData` から `baseTorque` が消える。e2e の `/__block?type=gearGenerator` が発電機 fixture を返す

- [ ] **Step 1: 失敗するテストを書く（validators.test.ts）**

`validators.test.ts:103` を置換:

```ts
      gear: { isClockwise: true, currentRpm: 10, currentTorque: 3, baseRpm: 20, role: "consumer" },
```

同ファイルの同 `it` ブロックの末尾（`expect(...).valid).toBe(true)` の直後）に追加:

```ts
    expect(parseTopicPayload(Topics.blockInventory, {
      ...openBase,
      gear: { isClockwise: true, currentRpm: 10, currentTorque: 3, baseRpm: 20, baseTorque: 5 },
    }).valid).toBe(false);
    expect(parseTopicPayload(Topics.blockInventory, {
      ...openBase,
      gear: { isClockwise: true, currentRpm: 10, currentTorque: 3, baseRpm: 0, role: "generator" },
    }).valid).toBe(true);
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && npx vitest run src/bridge/contract/validators.test.ts`
Expected: FAIL（`role` が unknown key、`baseTorque` 欠落で invalid）

- [ ] **Step 3: zod スキーマと型を変更する**

`inventory.ts:86-88` を置換:

```ts
export const GearRoleSchema = z.enum(["consumer", "generator"]);

// baseRpm は消費側だけ意味を持ち発電機は0。役割はクライアントdtoがマスタparamから導出済み（ADR 0056）
// baseRpm is meaningful only for consumers (generators send 0); the role is derived host-side from the master param (ADR 0056)
export const GearDetailDataSchema = z.object({
  isClockwise: z.boolean(), currentRpm: z.number(), currentTorque: z.number(), baseRpm: z.number(), role: GearRoleSchema,
});
```

`payloadTypes.ts:113` の直前に追加:

```ts
export type GearRole = z.infer<typeof GearRoleSchema>;
```

（`payloadTypes.ts` 先頭の import 行に `GearRoleSchema` を追加する。既存の `GearDetailDataSchema` と同じ import 文に並べる。）

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && npx vitest run src/bridge/contract/validators.test.ts src/bridge/contract/wireContract.test.ts`
Expected: PASS（`wireContract.test.ts` は Task 1 で更新済みの `block_inventory_gear_machine.json` を読む）

- [ ] **Step 5: e2e モック fixture を新形状にし、発電機 fixture を足す**

`blockDetailFixtures.ts:66` を置換:

```ts
  gear: { isClockwise: true, currentRpm: 12.5, currentTorque: 3.0, baseRpm: 20.0, role: "consumer" },
```

`blockDetailFixtures.ts:111` を置換:

```ts
  gear: { isClockwise: false, currentRpm: 8.0, currentTorque: 2.0, baseRpm: 12.0, role: "consumer" },
```

`blockDetailFixtures.ts:153` を置換:

```ts
  gear: { isClockwise: true, currentRpm: 10.0, currentTorque: 2.0, baseRpm: 10.0, role: "consumer" },
```

`blockLocalizationFixtures.ts:17`（`GEAR_PUMP_BLOCK_GUID` の次行）に追加:

```ts
export const GEAR_GENERATOR_BLOCK_GUID = "00000000-0000-4000-8000-000000000216";
```

`blockLocalizationFixtures.ts:36`（`[GEAR_PUMP_BLOCK_GUID, "Gear Pump", "歯車ポンプ"],` の次行）に追加:

```ts
  [GEAR_GENERATOR_BLOCK_GUID, "Windmill", "風車"],
```

`blockDetailFixtures.ts` の `blockGearPump` 定義の直後に追加:

```ts
// 歯車発電機: 消費要求を持たない発電側。gear.role が generator で baseRpm は 0
// Gear generator: the generating side with no consumption; gear.role is generator and baseRpm is 0
export const blockGearGenerator = {
  open: true,
  source: "block",
  blockType: "SimpleGearGenerator",
  identifier: "block:16",
  blockGuid: BlockGuids.GEAR_GENERATOR_BLOCK_GUID,
  itemSlots: [],
  fluidSlots: [],
  gear: { isClockwise: true, currentRpm: 20.0, currentTorque: 5.0, baseRpm: 0.0, role: "generator" },
  gearNetwork: { totalRequiredGearPower: 60.0, totalGenerateGearPower: 100.0, stopReason: "none" },
} satisfies BlockInventoryWireData;
```

`httpHandler.ts:33`（`electricToGear: fx.blockElectricToGear,`）の直前に追加:

```ts
  gearGenerator: fx.blockGearGenerator,
```

- [ ] **Step 6: 型検査を実行する**

Run: `cd moorestech_web/webui && npx tsc -b && npx tsc -p e2e/tsconfig.json --noEmit`
Expected: エラー0（`GearSection.tsx` は `baseTorque` を参照しているためここでエラーになる場合は Task 3 で解消する。その場合 Task 3 Step 3 まで進めてから本 Step を再実行する）

- [ ] **Step 7: コミットする**

```bash
git add moorestech_web/webui/src/bridge/contract/schemas/inventory.ts \
        moorestech_web/webui/src/bridge/contract/payloadTypes.ts \
        moorestech_web/webui/src/bridge/contract/validators.test.ts \
        moorestech_web/webui/e2e/mock-host/blockDetailFixtures.ts \
        moorestech_web/webui/e2e/mock-host/fixtures/blockLocalizationFixtures.ts \
        moorestech_web/webui/e2e/mock-host/httpHandler.ts
git commit -m "feat(webui): 歯車契約に role を足し baseTorque を落とす。e2e モックに歯車発電機を追加 (ADR 0056)"
```

---

### Task 3: ローカライズ4文言と GearSection の描画差し替え

**Files:**
- Modify: `Localization/localization.csv:23-24`
- Regenerate: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（`npm run gen:i18n`）
- Modify: `moorestech_web/webui/src/features/blockInventory/details/detailLogic.ts`（末尾に追加）
- Modify: `moorestech_web/webui/src/features/blockInventory/details/detailLogic.test.ts`
- Modify: `moorestech_web/webui/src/features/blockInventory/details/GearSection.tsx`（全面置換）
- Create: `moorestech_web/webui/src/features/blockInventory/details/GearSection.test.ts`
- Test: 上記2テスト

**Interfaces:**
- Consumes: Task 2 の `GearRole` 型と `GearDetailData.role`
- Produces: `gearTorqueTranslationKey(role: GearRole): TranslationKey`、`gearRpmTranslationKey(role: GearRole): TranslationKey`（`detailLogic.ts`）。`L.ui.blockInventory.gearConsumedTorque` / `gearGeneratedTorque` / `gearRpmWithBase` / `gearRpmCurrent`

- [ ] **Step 1: localization.csv を書き換える**

`Localization/localization.csv:23-24` の2行（`ui.blockInventory.gearTorqueSummary` と `ui.blockInventory.gearRpmSummary`）を次の4行に置換:

```csv
ui.blockInventory.gearConsumedTorque,Consumed torque {value},Consumed torque {value},消費トルク {value},Verbrauchtes Drehmoment {value}
ui.blockInventory.gearGeneratedTorque,Generated torque {value},Generated torque {value},発生トルク {value},Erzeugtes Drehmoment {value}
ui.blockInventory.gearRpmWithBase,RPM {current} / {base},RPM {current} / {base},RPM {current} / {base},U/min {current} / {base}
ui.blockInventory.gearRpmCurrent,RPM {value},RPM {value},RPM {value},U/min {value}
```

- [ ] **Step 2: キーを再生成し、旧キーの参照が消えることを確認する**

Run: `cd moorestech_web/webui && npm run gen:i18n && grep -rn "gearTorqueSummary\|gearRpmSummary" src e2e ../../Localization`
Expected: `localizationKeys.ts` が更新され、grep が `GearSection.tsx` の2箇所だけを返す（Step 5 で消える）

- [ ] **Step 3: 失敗するテストを書く（detailLogic.test.ts）**

`detailLogic.test.ts` の import に `gearRpmTranslationKey, gearTorqueTranslationKey` を追加し、`describe("detailLogic"` ブロック内の末尾に追加:

```ts
  it("gear labels follow the role: consumers consume with a base RPM, generators generate with current RPM only", () => {
    expect(gearTorqueTranslationKey("consumer")).toBe(L.ui.blockInventory.gearConsumedTorque);
    expect(gearTorqueTranslationKey("generator")).toBe(L.ui.blockInventory.gearGeneratedTorque);
    expect(gearRpmTranslationKey("consumer")).toBe(L.ui.blockInventory.gearRpmWithBase);
    expect(gearRpmTranslationKey("generator")).toBe(L.ui.blockInventory.gearRpmCurrent);
  });
```

- [ ] **Step 4: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && npx vitest run src/features/blockInventory/details/detailLogic.test.ts`
Expected: FAIL（関数未定義）

- [ ] **Step 5: detailLogic に役割→キーのテーブルを追加し、GearSection を全面置換する**

`detailLogic.ts` の import を `import type { GearNetworkStopReason, GearRole, MachineProcessState } from "@/bridge";` に変え、ファイル末尾に追加:

```ts
// 歯車行の文言は役割で決まる。消費側はトルク現在値＋RPM現在/基準、発生側はトルク・RPMとも現在値のみ（ADR 0056）
// Gear row wording follows the role: consumers show current torque plus RPM current/base, generators show current values only (ADR 0056)
export function gearTorqueTranslationKey(role: GearRole): TranslationKey {
  return GearTorqueKeys[role];
}

export function gearRpmTranslationKey(role: GearRole): TranslationKey {
  return GearRpmKeys[role];
}

const GearTorqueKeys: Record<GearRole, TranslationKey> = {
  consumer: L.ui.blockInventory.gearConsumedTorque,
  generator: L.ui.blockInventory.gearGeneratedTorque,
};

const GearRpmKeys: Record<GearRole, TranslationKey> = {
  consumer: L.ui.blockInventory.gearRpmWithBase,
  generator: L.ui.blockInventory.gearRpmCurrent,
};
```

`GearSection.tsx` を全面置換:

```tsx
import { Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import { gearRpmTranslationKey, gearTorqueTranslationKey } from "./detailLogic";
import { useI18n } from "@/shared/i18n";

// ギア: 消費/発生トルクとRPMの現在値報告。不足の赤は網停止理由行（GearNetworkSection）だけが担う（ADR 0056）
// Gear: reports current consumed/generated torque and RPM; only the network stop-reason row carries the insufficient tone (ADR 0056)
export default function GearSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  if (!data.gear) return null;
  const gear = data.gear;
  return (
    <Stack gap={2} data-testid="gear-section">
      <Text size="sm" c="var(--text-default)" data-testid="gear-torque">
        {t(gearTorqueTranslationKey(gear.role), { value: gear.currentTorque.toFixed(1) })}
      </Text>
      <Text size="sm" c="var(--text-default)" data-testid="gear-rpm">
        {t(gearRpmTranslationKey(gear.role), { current: gear.currentRpm.toFixed(1), base: gear.baseRpm.toFixed(1), value: gear.currentRpm.toFixed(1) })}
      </Text>
    </Stack>
  );
}
```

- [ ] **Step 6: GearSection の描画テストを書く**

`moorestech_web/webui/src/features/blockInventory/details/GearSection.test.ts`:

```ts
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { BlockInventoryOpen, GearDetailData } from "@/bridge";

vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string, params?: Record<string, string>) => `${key}|${JSON.stringify(params)}` }),
}));
vi.mock("@mantine/core", () => ({
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));

import GearSection from "./GearSection";

function render(gear: GearDetailData) {
  const data = { open: true, source: "block", blockType: "GearMachine", identifier: "block:1", blockGuid: "g", itemSlots: [], fluidSlots: [], gear } as unknown as BlockInventoryOpen;
  let tree!: ReturnType<typeof create>;
  act(() => { tree = create(createElement(GearSection, { data })); });
  return tree.root;
}

function textOf(root: ReturnType<typeof render>, testId: string): string {
  const node = root.findAll((n) => n.props["data-testid"] === testId)[0];
  return String(node.props.children);
}

describe("GearSection", () => {
  // 待機中（現在<基準相当）でも赤にならず、消費側は基準RPMを併記する
  // Even when idle (current below base), nothing turns insufficient; consumers show the base RPM
  it("renders consumed torque without a denominator and RPM with its base for consumers", () => {
    const root = render({ isClockwise: true, currentRpm: 5, currentTorque: 2.4, baseRpm: 10, role: "consumer" });
    expect(textOf(root, "gear-torque")).toBe('ui.blockInventory.gearConsumedTorque|{"value":"2.4"}');
    expect(textOf(root, "gear-rpm")).toBe('ui.blockInventory.gearRpmWithBase|{"current":"5.0","base":"10.0","value":"5.0"}');
    expect(root.findAll((n) => n.props["data-insufficient"] !== undefined)).toHaveLength(0);
  });
  it("renders generated torque and current RPM only for generators", () => {
    const root = render({ isClockwise: true, currentRpm: 20, currentTorque: 5, baseRpm: 0, role: "generator" });
    expect(textOf(root, "gear-torque")).toBe('ui.blockInventory.gearGeneratedTorque|{"value":"5.0"}');
    expect(textOf(root, "gear-rpm")).toBe('ui.blockInventory.gearRpmCurrent|{"current":"20.0","base":"0.0","value":"20.0"}');
  });
  // チェーンポール等の純伝達ブロックも同じ行構成（0.0がそのまま出る）
  // Pure transmission blocks such as chain poles share the same rows (0.0 is shown as is)
  it("keeps the same rows for a zero-torque transmission block", () => {
    const root = render({ isClockwise: true, currentRpm: 5, currentTorque: 0, baseRpm: 5, role: "consumer" });
    expect(textOf(root, "gear-torque")).toBe('ui.blockInventory.gearConsumedTorque|{"value":"0.0"}');
  });
});
```

- [ ] **Step 7: テストと型検査と lint を実行する**

Run: `cd moorestech_web/webui && npx vitest run src/features/blockInventory/details && npx tsc -b && npm run lint`
Expected: 全 PASS、型エラー0、lint エラー0（旧キー参照が無い）

- [ ] **Step 8: コミットする**

```bash
git add Localization/localization.csv \
        moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts \
        moorestech_web/webui/src/features/blockInventory/details/detailLogic.ts \
        moorestech_web/webui/src/features/blockInventory/details/detailLogic.test.ts \
        moorestech_web/webui/src/features/blockInventory/details/GearSection.tsx \
        moorestech_web/webui/src/features/blockInventory/details/GearSection.test.ts
git commit -m "feat(webui): 歯車セクションを消費/発生トルクとRPMの現在値報告にし赤判定を廃止 (ADR 0056)"
```

---

### Task 4: e2e（Playwright）の文言断言と発電機ケース

**Files:**
- Modify: `moorestech_web/webui/e2e/tests/block/blockDetails.spec.ts:29-34`
- Test: 同ファイル

**Interfaces:**
- Consumes: Task 2 の `gearGenerator` モック type、Task 3 の日本語文言「消費トルク」「発生トルク」

- [ ] **Step 1: 断言を新文言に置き換え、発電機ケースを足す**

`blockDetails.spec.ts:29-34` の `test("gear machine shows torque and gear network info", ...)` を置換:

```ts
test("歯車機械は消費トルクと基準付きRPMを赤なしで出し、網の需給行を持つ", async ({ page }) => {
  await setBlock(page, "gearMachine");
  await page.goto("/");
  await expect(page.getByTestId("gear-torque")).toHaveText("消費トルク 3.0");
  await expect(page.getByTestId("gear-rpm")).toHaveText("RPM 12.5 / 20.0");
  await expect(page.getByTestId("gear-torque")).not.toHaveAttribute("data-insufficient", "true");
  await expect(page.getByTestId("gear-rpm")).not.toHaveAttribute("data-insufficient", "true");
  await expect(page.getByTestId("gear-network-section")).toBeVisible();
});

test("歯車発電機は発生トルクと現在RPMだけを出す", async ({ page }) => {
  await setBlock(page, "gearGenerator");
  await page.goto("/");
  await expect(page.getByTestId("gear-torque")).toHaveText("発生トルク 5.0");
  await expect(page.getByTestId("gear-rpm")).toHaveText("RPM 20.0");
});
```

（e2e の既定言語は日本語。`blockDetails.spec.ts` 内の他テストが「100%」等の日本語文言を断言しているのと同じ前提。もし既定が英語なら `page.goto` 前に既存テストが使う言語切替ヘルパを同様に呼ぶ。）

- [ ] **Step 2: e2e を実行する**

Run: `cd moorestech_web/webui && npm run test:e2e -- e2e/tests/block/blockDetails.spec.ts`
Expected: 全 PASS（`renders gearMachine detail section` を含む）

- [ ] **Step 3: コミットする**

```bash
git add moorestech_web/webui/e2e/tests/block/blockDetails.spec.ts
git commit -m "test(webui e2e): 歯車行の新文言と歯車発電機ケースを断言 (ADR 0056)"
```

---

### Task 5: 実機確認（Unity PlayMode）とBeadsクローズ

**Files:**
- なし（確認のみ）。スクリーンショットは `docs/superpowers/plans/` に置かず PR 本文へ貼る

- [ ] **Step 1: Unity 全体コンパイルと WebUiHost 系テスト**

Run: `uloop compile --project-path ./moorestech_client && uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WireContract|WebUi"`
Expected: コンパイルエラー0、テスト全 PASS

- [ ] **Step 2: 実機で歯車機械（待機中）と風車を開いて確認する**

unity-playmode-recorded-playtest スキル（プレイテストDSL）で、風車＋歯車機械（`原始的な粉砕機`、レシピ未選択＝待機中）を接続して設置し、両ブロックの詳細パネルを開く。確認項目:
- 歯車機械: 「消費トルク 4.0」付近（基準20×待機0.2、RPM=基準時）が黒字、「RPM 10.0 / 10.0」が黒字、機械行に「待機中」と充足率が従来どおり出る
- 風車: 「発生トルク X」「RPM Y」の2行で「/ 0.0」が無い
- 供給不足を作る（歯車機械を複数繋ぐ）と網の停止理由行だけが赤になる

Expected: 上記3点が録画とスクリーンショットで確認できる

- [ ] **Step 3: Beads を閉じる**

```bash
bd close moorestech-0tf8 --reason="ADR 0056 実装完了: 歯車セクションを消費/発生トルクとRPMの現在値報告にし赤判定を廃止。実機確認済み"
```

---

### Task 6: 全ブランチレビュー（必須・省略不可）

- [ ] **Step 1: moores-code-review スキルで全ブランチレビューを実行する**

必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。使うスキルは moores-code-review。

- [ ] **Step 2: 指摘を反映したら Task 4 の e2e と Task 5 Step 1 を反映後のバイナリで再実施する**

レビュー反映がソース（役割判定・文言テーブル・dto 生成）に触れた場合、テスト通過だけを根拠にせず Task 4 Step 2 と Task 5 Step 1 を再実行してから完了とする。

- [ ] **Step 3: pr-create スキルで PR を作る**

PR 本文に ADR 0056 と `.decisions/2026-09-11-*` 9件をリンクし、Task 5 のスクリーンショットを貼る。

---

## 配置と前例（spec-architecture-review）

| # | 項目 | 配置先 | 機構 | 前例 |
|---|---|---|---|---|
| 1 | `GearDetailDto.Role`（string）/ `BaseTorque` 削除 | `Client.WebUiHost` dto | public フィールド dto、camelCase 文字列 enum | `BlockDetailDtos.cs` の `MachineDetailDto.CurrentState`（`ToCamelCase`）、`GearNetworkDto.StopReason` |
| 2 | `GearDetailDtoBuilder.Apply(dto, block, param)` 新設 | `Client.WebUiHost/Game/Topics/BlockDetail/` | static builder、`BlockDetailDtoBuilder.Apply` から委譲 | `MinerDetailDtoBuilder.Apply` / `PumpDetailDtoBuilder.Apply` / `TrainPlatformDetailDtoBuilder.Apply`（同ディレクトリ、同シグネチャ族） |
| 3 | 役割判別 `param is IGearConsumptionParam` | クライアント dto ビルダ | 生成マスタのスキーマ interface | 旧 `BlockDetailDtoBuilder.GetGearConsumption`（「スキーマの IGearConsumptionParam が正本」） |
| 4 | `GearRoleSchema` / `GearRole` 型 | `src/bridge/contract/schemas/inventory.ts`, `payloadTypes.ts` | zod `z.enum` | `MachineProcessStateSchema`, `GearNetworkStopReasonSchema` |
| 5 | `gearTorqueTranslationKey` / `gearRpmTranslationKey` | `details/detailLogic.ts` | `Record<enum, TranslationKey>` テーブル | `stopReasonTranslationKey` + `GearStopReasonKeys`、`MachineStateDisplayTable` |
| 6 | `GearSection.tsx` の `Text` 化 | `details/GearSection.tsx` | Mantine `Text c="var(--text-default)"` | `GeneratorSection.tsx` の稼働率行 |
| 7 | e2e モック `blockGearGenerator` + `gearGenerator` type | `e2e/mock-host/` | fixture 定数 + `BLOCK_FIXTURES` map | `blockGearPump` / `gearPump` |
| 8 | ローカライズ4キー | `Localization/localization.csv` → `gen:i18n` | CSV 1行1キー・4言語列 | `ui.blockInventory.generatorOperatingRate` 行 |

データフロー: `GearStateDetail`（サーバー、無変更）→ `GearDetailDtoBuilder`（書き手: 役割を足し BaseTorque を落とす）→ `[BlockInventoryDto.Gear]` → `GearSection`（読み手）。交差点なし。サーバーへの逆流・新プロトコル・新購読なし（歯車の役割はマスタ静的情報から導出できるため、`creating-server-protocol` の3点セットは不要。原則表「研究/アンロック由来の派生値の同期に新プロトコルが要るか → 原則不要」と同型）。

検査1（層責務）: 役割判別は「表示のための分類」であり Web UI ホストの責務。サーバー `GearStateDetail` は物理値のみ（基盤にドメイン語彙を持ち込まない）。検査4（機構選択）: 既存機構の抑止・迂回なし。Phase 2.5 死活表: 歯車セクション（消費側/発電機側とも表示継続）、機械行（不変）、網の需給・停止理由行（不変）、`data-testid` `gear-section`/`gear-torque`/`gear-rpm`（維持）。死ぬ操作なし。

新規パターン（レビュー注目点）: なし。

## 判断記録（ADR）

- 設計裁定: `docs/adr/0056-gear-section-reports-current-values-without-satisfaction-comparison.md`（`.decisions/2026-09-11-*` 9件）
- planning 中の判断:
  - 歯車ブロックの dto 生成を `BlockDetailDtoBuilder` から `GearDetailDtoBuilder` へ切り出す。出所: agent前提（同ディレクトリの Miner/Pump/TrainPlatform builder 前例、200行規約）
  - 役割文字列は `GearDetailDtoBuilder.ConsumerRole / GeneratorRole` の定数で C# 側に置き、zod `z.enum` で受ける。出所: agent前提（`ToCamelCase` 文字列 enum 前例）
  - RPM 行の翻訳パラメータは `current` / `base` / `value` を両役割で全部渡し、文言側で使う分だけ参照する（役割で引数を分岐させない）。出所: agent前提（`t(key, params)` の未使用パラメータは無害。分岐を減らす）
  - e2e の発電機代表は `SimpleGearGenerator`（風車）。出所: agent前提（発電機3種のうち最小構成）
  - 目視QA撮影スクリプトは無変更。出所: agent前提（`capture-machine-qa.ts` は歯車行の文言・testId を参照していない）
  - 作業ブランチを `feature/gear-section-current-values`（origin/master 起点、専用 worktree）とし、設計文書コミットは fluid-slot ブランチから移した。出所: agent前提（別機能 PR #1341 へ混ぜない）
