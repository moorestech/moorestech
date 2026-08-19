# 0014. 研究UIは枠色4状態・ステージ全域占有・種類別解放物表示にする

日付: 2026-08-18
状態: 採択

## 文脈

Web UIの研究ツリー画面には3つの問題がある。

1. **状態が読めない。** サーバーは研究ノード状態を5値（Completed / Researchable / UnresearchableNotEnoughItem / UnresearchableNotEnoughPreNode / UnresearchableAllReasons）で送り、クライアントもインベントリとの突き合わせ（`hasEnoughItems`）を実装済みだが、ノードカードのCSSは「未解放=減光45%」「研究済み=白枠」の2区別しかない。「研究可能」と「アイテム不足」はどちらも同じ通常表示で、ノードを見ても実行できるか分からない。
2. **画面が狭い。** パネルは上端 `--menu-upper-safe-area`(128px)・高さ525px固定・持ち物列378pxを空けたグリッド配置で、ツリーの見える範囲が小さい。
3. **解放物が届いていない。** マスタの `clearedActions` には実データ47ノード中 unlockBlock 32件・unlockItemRecipeView 18件・unlockMachineRecipe 11件・unlockConnectTool 3件・unlockTrainCar 2件が入っているが、DTO変換器（`ResearchNodeDtoFactory.AppendActionItems`）は giveItem と unlockItemRecipeView の2種しか拾わず、最多のブロック解放が一切表示されない（uGUI時代の欠落をそのまま移植した形）。詳細ペインも報酬と解放を無札で1行に混載しており、webui-design §4 の無札並置禁止に反する。

## 決定

1. **ノードカードは枠色の系統で4状態を見分ける。** 未解放=減光45%（現状維持）/ 研究可能・アイテム不足=通常グレー枠 / 研究可能・充足=シアン枠（`--select-cyan`）/ 研究済み=白枠（現状維持）。webui-design §8.5 と `--select-cyan` の用途ホワイトリストへ「研究ノードの実行可能点灯」を追記する。
   - 出所: ユーザー裁定 2026-08-18「枠色の系統で4分類」（赤warning枠案・チェックマーク装飾案は却下。[[2026-08-18-研究ノードは枠色の系統で4状態を見分ける]]）
   - アイテム充足はクライアント側でライブ再計算する（サーバーstateは画面突入時と研究実行後にしか更新されないため。研究ボタンと同じ `hasEnoughItems` ロジック）: agent前提（既存 `deriveResearchButton` 同型の先行パターン）
2. **研究パネルはステージ全域（上下左右とも端まで）を占有する。** チャレンジHUDは半透明パネルの上に重畳表示され続ける（ツリーはパン可能なので重なりは回避できる）。webui-design §1「全画面UIは作らない」と §8.14 の安全帯前提へ研究画面の例外を明記する。パネル様式自体は GamePanel(default) のまま変えない。
   - 出所: ユーザー裁定 2026-08-18「上端も埋めてほぼ全面」（チャレンジ同型の安全帯維持案・小余白案は却下。[[2026-08-18-研究画面はステージ全域を占有しHUD重畳を許容する]]）
   - **持ち物パネルは今まで通り表示を維持する。** 現状の位置・見た目・アイテム把持操作を変えず、研究パネルより上の層に重畳する（研究パネルはその背後も占有し、ツリーのパンで重なりを回避できる）。
   - 出所: ユーザー裁定 2026-08-18「持ち物は今まで通り表示する。それ以外で全面表示する」（研究画面から持ち物を外す案は却下。[[2026-08-18-研究画面全面化でも持ち物パネルは今まで通り重畳表示する]]）
3. **clearedActions 全種をDTOへ通し、詳細ペインは種類別ラベル付きセクションで縦積みする。** 表示対象は unlockBlock / unlockMachineRecipe / unlockItemRecipeView / unlockConnectTool / unlockTrainCar / giveItem の6種。「解放: ブロック」「解放: 機械レシピ」等の `--text-muted` ラベル+アイコン列で分け、機械レシピは出力アイテムのアイコン、connect tool / train car は名前のテキスト行で表す。playSkit 等の演出系アクションは表示しない。
   - 出所: ユーザー裁定 2026-08-18「種類別ラベル付きセクション」（フラット一覧+ホバー種別案は却下。[[2026-08-18-研究の解放物は種類別ラベル付きセクションで表示する]]）
   - 演出系アクション非表示・機械レシピの出力アイテム代表表示: agent前提（機械レシピ選択タブ §8.7 の代表出力アイテム前例）
4. **詳細ペインの消費アイテム表示はクラフトUI前例へ揃える。** 不足スロットは40%減光+赤文字、ツールチップで所持/必要数（`CraftRecipeView` の materialTooltip 様式）。
   - 出所: agent前提（前例一致原則。`CraftRecipeView` が最充実の先行パターン）

## 検証

コンパイル＋既存テスト（researchLogic のユニットテスト・WireContractResearchTest）に加え、mockホストのスクリーンショットで4状態の枠色・全域レイアウト・種類別セクションを目視QAする（webui-design §10）。

## 影響

- webui-design（`.claude/skills/webui-design`）の §1・§8.5・§8.14・シアン用途リストを本ADRと同時に更新する（様式が先、実装が後の原則）。
- ResearchTopic のDTO契約が拡張されるため、Unityホスト側（Client.WebUiHost）とWeb側の両方が変わる。
- サーバー側（Game.Research）のロジック・プロトコルは変更しない。
