---
name: mooreseditor-block-connectors
description: "Use When: moorestech の任意ブロックについて、mooreseditor で placement offset、inputConnects、outputConnects、connectorGuid、direction、flowCapacity、connectTankIndex を編集またはQAし、Unity の BlockSetup で結果確認する必要がある場合に使う。「ブロックの接続を調整」「inputConnects/outputConnectsを追加」「Fluid/Gear/Inventory connectorを編集」「BlockSetupで見ながらoffsetを直す」「録画からブロック編集手順を抽象化」「接続位置がずれている」「ブロックが接続されない」といった依頼で起動する。"
---

# mooreseditor-block-connectors

mooreseditor の `blocks` データで任意ブロックの offset / inputConnects / outputConnects を編集し、Unity の `BlockSetup` シーンで見た目と接続位置を確認する。

## 最初に確定する入力

編集前に、ユーザー依頼・mooreseditor の現在選択・マスタデータから次を確定する。

- 対象ブロック: `name` または `blockGuid`。
- 編集範囲: ブロック本体の `offset`、`inputConnects`、`outputConnects`、またはその組み合わせ。
- コネクタの向きと種類: `Input` / `Output`、`Fluid` / `Gear` / `Inventory`、またはスキーマ上の enum 値。
- コネクタ値: `connectorGuid`、`offset.x/y/z`、`directions`、`connectOption.flowCapacity`、`connectTankIndex`。
- Unity 確認観点: 編集後に何がどこと揃うべきか、または何と接続すべきか。

ユーザーが録画だけを渡した場合は、まず具体的な編集値を抽出し、それを上記の入力に一般化する。録画に出てきた値を今後の編集へ固定値として埋め込まない。

## 基本手順

1. `pwd` で現在の worktree を確認する。このプロジェクトは git worktree を多用する。
2. Unity で `moorestech_client/Assets/Scenes/Other/BlockSetup.unity` を開く。
3. `mooreseditor` を開き、`Editor` タブで `blocks` テーブルを表示する。
4. `name` または `blockGuid` で対象ブロック行を探し、その行の `Edit` を押す。
5. ブロック全体をセットアップ位置から動かす必要がある場合だけ、ブロック本体の `offset` を編集する。
6. コネクタ編集では、`inputConnects` または `outputConnects` の `Edit` を押す。
7. 適切なコネクタ行が存在しない場合だけ `Add Item` で追加し、既存行が使えるならその行を編集する。
8. `connectType` を先に設定する。種類によって表示されるコネクタ固有フィールドが変わるため。
9. 確定済み入力に従い、`connectorGuid`、コネクタ `offset`、`directions`、`connectOption`、`connectTankIndex` を入力する。
10. Unity に戻り、`BlockSetup` で対象ブロックを確認する。視点を回転・パンし、意図した位置に揃っているかを見る。
11. mooreseditor に戻って値を修正し、Unity で期待位置になるまで反復する。
12. 視覚確認に合格したら、mooreseditor で `Cmd+S` を押して保存する。

この作業中に Unity の scene、prefab、ScriptableObject の YAML ファイルを直接編集しない。マスタデータは mooreseditor、Unity シリアライズ資産は Unity Editor または uloop 経由で変更する。

## コネクタ編集ルール

- ブロック本体の `offset` とコネクタの `offset` は別レイヤーとして扱う。ブロック `offset` はブロック全体を動かし、コネクタ `offset` はブロック相対の接続点を動かす。
- ずれの原因を説明する最小のフィールドを編集する。パイプ口だけがずれているなら、ブロック本体ではなくコネクタ offset を変える。
- `Fluid` コネクタでは `connectTankIndex` と `connectOption.flowCapacity` も確認する。見た目の位置合わせ項目ではないが、ランタイム挙動に影響する。
- コピーした connector GUID は必ずフル GUID で貼り付け、短縮表示が別行を指していないか視覚確認する。
- input と output のコネクタ行は区別する。ブロック設計上の対称ポートでない限り、値を機械的にミラーしない。

## 録画解析

Record & Replay の出力を根拠にする場合:

1. `events.jsonl` を読む。mooreseditor のテーブルは巨大なので、生のアクセシビリティツリーだけに依存しない。
2. まず有用なイベントだけを抽出する。

```bash
jq -r 'select(.kind|test("mouse|keyboard|window.changed|session")) | [.id,.timestamp,.kind,(.app.name // ""),(.window.title // ""),(.mouse.target.title // ""),(.keyboard.keyEquivalent // ""),((.keyboard.modifiers // [])|join("+"))] | @tsv' events.jsonl
```

3. `keyboard.text_input` の target を見て、編集された `X`、`Y`、`Z`、GUID、enum 値を復元する。
4. `Edit inputConnects`、`Edit outputConnects`、`Add Item`、`クリップボードの内容を追加` のクリックを意味のある手順へ変換する。
5. 録画が汎用的な `AXButton Edit` しか捉えていない場合、対象行が不確実であることを明記する。
6. event ID と timestamp の順序が食い違う場合、手順の時系列は timestamp を基準にし、ID は根拠ラベルとしてだけ使う。

## 検証

- 重要な offset または connector 変更ごとに Unity 側で確認したことを確認する。
- mooreseditor で `Cmd+S` 保存したことを確認する。
- `.cs` ファイルを変更していない場合、プロジェクト規約上 Unity compile は不要。
- 補助作業でコード、YAML スキーマ、JSON マスタを変更した場合は、該当スキルに従って必要な compile / validation を実行する。
- 完了前に `git diff -- .codex/skills/mooreseditor-block-connectors/SKILL.md` を実行するか、スキル本文を直接読み、録画由来の固定値が残っていないか確認する。

## Gotchas

- Record & Replay ログには巨大な `AX` テーブル dump が含まれる。要約前に event kind と keyboard target で絞る。
- ログ上の汎用的な `Edit` ボタンだけでは、どのブロック行を編集したかは確定しない。周辺テーブル状態を使うか、行特定が重要ならユーザーに対象ブロックを確認する。
- `keyboard.text_input` の値は追加入力のトレースである。target の placeholder/value と近くの `Cmd+A` / delete イベントから最終値を判断する。
- mooreseditor の UI 更新で element index は変わる。1回の録画に出た AX element 番号を手順の根拠にしない。
- Unity のシーン操作キー入力は QA 文脈であり、データ編集ではない。マスタデータ変更として扱わない。
