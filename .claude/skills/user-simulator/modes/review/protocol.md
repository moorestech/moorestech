# review モード — spec/plan完成時の予測レビュー

brainstorming（spec）/ writing-plans（plan）の完了後・ユーザーレビュー依頼の**前**に必ず実施する。

**フック関所**: 両スキルのfrontmatter hooksが `scripts/sim-gate.sh` を配線しており、spec/planを書いた
セッションは `modes/improve/misses.md` への採点追記（＝手順5）が行われるまでターン終了がブロックされる。
ユーザーが明示的にreviewのスキップを指示した場合も、その旨をmisses.mdに1行記録して通過する。

## メインセッションの手順

1. 4カテゴリ文脈（ゴール/非目標/許容トレードオフ/制約。ユーザー発言由来と自分の判断を区別）を
   scratchpadに書く。対象docにADRがあれば「裁定済み事項・蒸し返し禁止」として含める。
   auto-memory（自セッションのシステムプロンプト記載のmemoryディレクトリ）に対象と関連しそうな
   メモリがあれば、その抜粋またはフルパスをcontextへ含める（スキルファイルには環境依存パスを書かない）。
2. Fable判事を1体起動する（Agent tool, model: **fable**。subagentのFable指定はこのスキルだけの例外・decisions.md #2）:
   ```
   Read this : <skills>/user-simulator/agents/judge.md
   mode      : review
   doc       : <対象docの絶対パス>
   context   : <文脈ファイルの絶対パス>
   protocol  : <skills>/user-simulator/modes/review/protocol.md
   ```
3. 判事の予測レポートを受け、**確信ありの指摘は自分でdocへ適用**し、docのADRを更新する。
4. ユーザーには下記テンプレートで提示する。指摘の適用済み/要裁定の区別を明確に。
5. ユーザーの反応を採点として `modes/improve/misses.md` に追記する（的中予測の根拠にした知識の実名を寄与知識欄へ転記）:
   - 追加指摘があった → **FN**。即座に改善ハンドオフを発行する（modes/improve/protocol.md の発行手順）
   - 適用済み指摘への否定 → **FP**。同じくハンドオフ発行
   - 何もなければ的中分を記録

## 判事の出力契約（レポート）

```markdown
## 元々の想定
（docの設計要約と、依拠している主要な前提を3-6行）

## 指摘予測 — 適用推奨（確信あり・反証済み）
- [対象セクション] 指摘内容 → 修正方針（根拠: 裁定/原則/前例の実名）

## 指摘予測 — Warning（確信一段弱い）
- ...

## 裁定が必要（ユーザーにしか決められない分岐。選択肢付き）
- ...

## 見なかった領域
- （ロードしなかった知識・検査しなかった観点と、その理由を1行ずつ）
```

## ユーザー提示テンプレート（メインが書く）

```markdown
## user-simulator review 結果
**元々の想定**: （1-3行）
**適用済みの指摘予測**: （各1行＋根拠）
**要裁定**: （あればAskUserQuestionへ。preanswerを通し予測付きで提示）
**見なかった領域**: （1-3行）
```
