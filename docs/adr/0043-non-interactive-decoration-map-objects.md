# 0043. インタラクトできない装飾mapObject（miningType: None）

日付: 2026-08-30
状態: 採択

## Context

チュートリアルの `mapObjectPin`（`pinTargetType: earnItem`、原木）は `ChallengeMaster.TryResolvePinTargets` → `MapObjectMaster.GetMapObjectGuidsByEarnItem` で「そのアイテムを落とす全mapObject」に解決される。map.json ではブッシュ系（ブッシュ・Bush1〜3×Mountains/Savanna・Olivebush1〜3・Brittlebush_1〜4）が木と同じ `earnItems: 原木×1`・`miningType: Mining` で定義されているため、木を指すべきピンがブッシュへ吸われる。

スキーマ `VanillaSchema/map.yml` の `miningType` は `PickUp | Mining` の2値switch。`mapVeins.handMiningType: none | minable` に「触れない」を表す同形の前例がある。

クライアントの当たり判定は `MapObjectGameObject.Initialize` が子の `MapObjectRayTarget` を初期化し、レティクルが `IMiningRayTarget` 経由で `TryBeginHandMining` へ到達する構造。サーバーは `MapObjectMiningService` が `MiningType` を見て採掘要求を処理する。

## Decision

- **ブッシュ系mapObjectは完全な装飾物にする。** 攻撃対象にならず、レティクル反応・ツールチップ・HPバー無し、アイテムドロップ無し。サーバーはダメージ/採取要求を弾く。結果として earnItem ピン候補から外れ、原木は木からのみ得られる（ブッシュから原木を得る導線が消える帰結は受容）。
  出所: ユーザー裁定 2026-08-30 原文「インタラクトできないmap objectを作りたい（bushがピンされるのがうざい）」→ 選択「A 完全な装飾物」
  棄却案: ①採掘可のままピン候補からだけ除外するフラグ ②earnItemピン解決を主産物条件で絞る間接方式

- **マスタ表現は `map.yml` の `miningType` に第3値 `None` を追加する**（`PickUp | Mining | None`。`miningParam` switch に `None → optional 空object` を追加）。`None` なのに `earnItems` が非空ならマスタ検証（`MapObjectMasterUtil`）で弾く。
  出所: ユーザー裁定 2026-08-30 選択「miningType に None を追加」
  棄却案: ①別軸 `interactionType: interactable | decoration` の並列追加（Mining かつ decoration の矛盾状態が表現可能） ②スキーマ据え置きで `earnItems:[]`・`miningTools:[]` の空定義（意味が暗黙でHPバー等が残る）

- **クライアントはレイ判定の層で除外する。** `None` の個体は `MapObjectRayTarget` を有効化せず、HPバーも出さない。狙っても何も起きない。物理コライダー（歩行の当たり）は据え置き。サーバー `MapObjectMiningService` も `None` を偽造要求として拒否し二重防御する。
  出所: ユーザー裁定 2026-08-30 選択「A レイ判定の層で除外」
  棄却案: レイには乗せ `MiningStartOutcome.NotInteractable` を返しツールチップで「採取できない」と出す方式（触れる感が残る）

- **`None` にするマスタ対象**: ブッシュ系13件（ブッシュ dc8285a7、Mountains Bush1/3、Oasis Olivebush1〜3、Savanna Bush1〜3、MesaDesert Brittlebush_1〜4）＋メサ崖・地層系26件（BigMesa_0〜5、ThinMesa_0〜5、StratMesaSharp_0〜4、Strate_0〜5、StrateCliff_0〜2）。いずれも `earnItems: []`。Boulders・Rubble・CowSkull・サボテン類は据え置き。
  出所: ユーザー裁定 2026-08-30 原文「ブッシュ系と、あとメサのでかい崖」→ 崖範囲は選択「A 崖・地層系5グループ26件すべて」
  棄却案: ①旧「ブッシュ」1件のみ ②バイオーム別Bush12件のみ ③BigMesa 6件のみ ④BigMesa＋StrateCliff＋ThinMesa の15件

- agent前提: `None` 個体のレイ除外は `MapObjectRayTarget` 側のコライダー無効化で行う（`IMiningRayTarget` の所有者nullを配らないため）。マスタ変更は `moorestech_master` の別PRとし、本repo `.moorestech-external-revisions.json` のピンをそのPRのpush済みコミットへ更新する（AGENTS.md規約）。アセット変更のみのテスト新設はしない。

## 先行裁定との関係

- `.decisions/2026-08-28-取得アイテムのない植物系mapObjectは原木を落とす.md`（ADR 0037）は「earnItems空53件すべてに原木を割り当てる」と決めた。本ADRはそのうちブッシュ系13件を装飾物へ戻す部分的な上書きであり、残り（サボテン・DryGrass・Peanut・Wildflowers・Opuntia）は0037のまま原木を落とす。
- ADR 0036（岩系は石を落とす）のメサ崖・地層系26件も同様に本ADRで上書きする。Boulders・Rubble・CowSkull は0036のまま。
- 0037が追加した `MapObjectMasterUtil` の「earnItems空はエラー」検査は `miningType != None` に限定する（Noneは逆に「earnItems非空はエラー」）。
  出所: agent前提（ユーザー裁定「A 完全な装飾物」の帰結。検査を残すと今回の39件が全件エラーになる）

## Consequences

- `MapObjectMasterElement.MiningTypeConst.None` が生成され、`MiningParam` の型判定箇所（クライアント `MapObjectGameObject`、サーバー `MapObjectMiningService`・`MapObjectMasterUtil`）に分岐が増える
- earnItem ピン解決は `earnItems` 索引がそのまま使えるため変更不要（`None` は `earnItems: []` を検証で強制する）
- `docs/adr/0043` ／ `.decisions/2026-08-30-装飾mapObjectはminingType-Noneで表現しブッシュは完全に触れなくする.md`
