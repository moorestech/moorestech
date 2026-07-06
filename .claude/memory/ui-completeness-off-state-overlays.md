---
name: ui-completeness-off-state-overlays
description: UI機能の網羅棚卸しはUIStateEnum(状態機械)だけを背骨にすると状態外オーバーレイを構造的に見落とす
metadata: 
  node_type: memory
  type: feedback
  originSessionId: bf5fbf1d-a2c0-47ae-b99b-14132f4cd8ef
---

moorestech クライアントの uGUI→Web 移行で「全 UI 機能を洗い出す」際、バックグラウンドスキット(`Client.Game/InGame/BackgroundSkit/BackgroundSkitManager`)を見落とした。ユーザー指摘で発覚。

**Why:** 3つの失敗が重なった。
1. 探索スコープを `UI/` 配下 + `Client.Skit/` に限定したが、BackgroundSkit は `Client.Game/InGame/BackgroundSkit/`(UI/の兄弟ディレクトリ)にあり隙間に落ちた。
2. 網羅レンズの背骨を `UIStateEnum`(11状態)に固定した。バックグラウンドスキットは固有 UIState を持たず GameScreen 中にオーバーレイ表示する「状態機械の外」の機能なので、状態軸の検査では構造上拾えない。
3. 計画書に `BackgroundSkitUI` の名前を一度書いていたため、再監査エージェントが「名前がある＝カバー済み」と誤判定した(文字列存在 ≠ 正しい分類の検証)。

**How to apply:**
- UI 網羅の背骨は状態機械でなく**「全ソースルートの全 `.cs` ファイル台帳」を母集団**に置く。grep は母集団でなく「UI らしさの優先順位フラグ」に降格する（grep を背骨にすると 0ヒットdir・カスタム基底経由UI・トークン無し子View が沈黙脱落する）。
- 「状態を持たないオーバーレイ/常時表示/プレイ中割り込み UI」を独立カテゴリ(軸C)として明示的に探す。`UIStateEnum` の外に**第2の状態機械 `GameStateType`(InGame/Skit/CutScene)**もある。
- 計画書に名前が出ているだけの項目は「カバー済み」扱いせず、責務が正しく記述されているか必ず実コードで検証する。TODO の「空/不在」断定も実コードで反証する。
- ソースルートは手書きリストにせず**機械列挙(`find . -maxdepth 1 -type d` + 全 asmdef)→分類**で導出（`Client.Game/InGame` だけに絞ると兄弟 `Client.Game/Common`・`Client.Game/Skit` が漏れる）。uGUI ゲームなので **prefab が主たる UI 定義層**(`.uxml`/`.uss` は僅少)＝資産台帳も母集団に含める。
- **手順の決定版**: `/Users/katsumi/moorestech-worktrees/tree2/docs/ui-completeness-reaudit-plan.md`（tree2 worktreeのみに存在、mainリポジトリ未コミット。multi-lens-review 3周収束 + 407cs+32資産で実走済み 2026-06-14）。旧確認 grep `grep -rlE "UIDocument|..." | grep -v /UI/` は **BackgroundSkitManager すら拾えず supersede 済み**。これを背骨に使わない。

関連: [[key-files]] の InGame UI 構造。
