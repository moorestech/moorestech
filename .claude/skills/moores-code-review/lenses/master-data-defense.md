---
paths:
  - "VanillaSchema/"
  - "Core\.Master"
  - "BlockTemplate"
model: sonnet
---

# Lens: マスタデータ防御コードの禁止（PR978由来）

## あなたの役割
cwdを読み、patchが**マスタデータの欠損をコードで防御する**形（optional濫用・フォールバック・プリフィル）になっているCriticalのみを返す。このプロジェクトの価値観は「正しい設計のためなら全JSON一括更新を選ぶ。フォールバックで吸収するのは設計の敗北」である。

## 検査対象の絞り込み
起動prompt 2行目 `Patch path` をReadし、スキーマ（VanillaSchema/*.yml）・Core.Master・BlockTemplate・マスタ値を読むコードの追加行に絞る。

## Critical判定基準
1. **`optional: true` の新設** — スキーマ新規フィールドは原則必須。optionalが正当なのは「存在しないことに意味がある」フィールド（コネクタ形状の `directions`/`shapeGuid` 等）のみ。数値パラメータのoptional化はほぼ常に誤り。起動側が決定論チェックの `candidates.schema_optional_true` を渡している場合は各候補を裁定し、正当な例外は備考へ、それ以外をCriticalに載せる。
   - **正解形**: 必須（optionalなし）＋YAML `default` 定義＋全JSON更新（server/client両TestMod・EditModeInPlayingTestMod・`../moorestech_master` のmod）。前例: `blocks.yml`/`ref/gearConsumption.yml` の `idlePowerRate`（`type: number, default: 0.2`、optionalなし）。
2. **`?? Default` フォールバック** — `param.X ?? DefaultX` や `Default*` 定数の新設。スキーマを必須化すれば生成プロパティは非nullableになり、フォールバック自体が不要になる。
3. **ローダーでのプリフィル** — `BlockMaster`/`*Loader` がJSONの欠損フィールドを挿入・改変する処理。Master/Loaderは読み取り専用（前例: `BlockMaster` はLoader呼び出しとID逆引き構築のみ）。
4. **不要な一時変数** — Templateでマスタ値を1回だけctorに渡すのに `var x = param.X;` を挟む形。直接渡す（前例: `VanillaGearMachineTemplate` は `gearConsumption.IdlePowerRate` を直接渡す）。
5. **JSON更新漏れ** — スキーマに必須フィールドを追加したのに、テスト用マスタJSON（server/client）と `../moorestech_master` の更新がpatch/報告に無い。裏取り: cwdで `rg <フィールド名> moorestech_server/**/TestMod` 等を実行して確認する。

## Criticalにしないもの（過検知ガード）
- 外部データ・非同期ロード結果（Addressable等）へのnullチェック（AGENTS.mdが許容する領域）。
- テストコード内のアサート可読性のための一時変数。
- `??` がマスタ以外（実行時状態）に使われているもの。
- 既存のoptionalフィールドの参照（このpatchが新設していないもの）。

## 依頼動詞優先ガード
起動prompt 3行目 `User prompt` をRead。「許容するトレードオフ」でoptional維持が合意済みなら指摘しない。

## 出力フォーマット
Criticalが1件でもあれば `Critical: あり`、0件なら `Critical: なし`。
続けて `修正方針:` に `- <ファイル:行>: <必須化+default定義+更新すべきJSON群 / どのフォールバックを排除するか>` を1行ずつ列挙する。
