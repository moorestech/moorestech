# ブループリント（既存建築のコピー＆ペースト）設計

日付: 2026-07-07
ステータス: 設計承認待ち

## 概要

ワールド上の既存建築を矩形範囲で選択してコピーし、名前を付けてライブラリに保存、
一覧から選んで何度でも貼り付けられる機能。ライブラリはサーバーセーブに永続化し、
貼り付けは既存の一括設置プロトコル `va:placeBlock` に乗せる。

The blueprint feature lets players copy an area of existing builds, save it to a
named library persisted in the server save, and paste it repeatedly through the
existing bulk placement protocol.

## 決定事項

| 論点 | 決定 |
|---|---|
| 保存形態 | ライブラリ型（名前付き保存・何度でも貼り付け） |
| 保存先 | サーバーセーブ内（`WorldSaveAllInfoV1` にセクション追加、ワールド共有） |
| 解放 | 最初から使える（解放ゲートなし） |
| 選択UX | 地面平面の矩形ドラッグ＋高さ全域（柱状選択） |
| 包含規則 | 占有セルが1つでも矩形に入るブロックは含む |
| コピー対象 | ブロック＋向き＋ブロック設定（フィルタ等）。実行時状態は対象外 |
| コピー対象外 | レール系ブロック、列車・エンティティ・MapObject、手動接続（歯車チェーン・電線手動張り） |
| 回転 | 水平回転のみ（Rキー90度） |
| コスト | 貼り付け時に通常の建設コストを消費。コピーは無料 |
| 衝突・コスト不足 | 既存 `va:placeBlock` どおり置けないセルをスキップする部分設置 |
| 電線 | コピーせず設置時の電線自動接続に任せる |

### スコープ外（v1）

BP文字列エクスポート／プレイヤー間共有、ワールド横断ライブラリ、高さ調整付き選択、
上下反転、貼り付けUndo、BP数・サイズ上限、BPの上書き保存・リネーム（作成と削除のみ）。

## データモデル

BPは「アンカーからの相対オフセット＋ブロック種＋向き＋設定バイト列」の列。
座標・InstanceIdなどワールド固有の情報は持たない。

```
BlueprintJsonObject
├ name: string（重複時は "name (2)" のように連番付与）
├ blocks: List<BlueprintBlockJsonObject>
│   ├ offset: {x,y,z}          // アンカー（選択矩形のXZ中心セル・最下段Y）からの相対
│   ├ blockGuid: string        // BlockGuid（ロード時にBlockIdへ解決、失敗ブロックは除外）
│   ├ direction: int           // BlockDirection
│   └ settings: Dictionary<string, string>  // SettingsKey → 設定JSON文字列（可読形式）
```

- セーブ統合: `WorldSaveAllInfoV1` に `[JsonProperty("blueprints")] List<BlueprintJsonObject>` を追加。
  Research/Challenge と同じ先行パターン（保存は `AssembleSaveJsonText`、復元は `WorldLoaderFromJson`）。
- 保持クラス: `Game.Blueprint`（新規 asmdef）の `BlueprintDatastore`。`ServerContext` からアクセス。

## 設定の抽出と注入

「設定」（フィルタ設定等）と「実行時状態」（加工進捗・インベントリ中身）を区別するため、
コピー可能な設定を持つコンポーネント用の新インターフェースを導入する。

```csharp
// Game.Block.Interface/Component/
public interface IBlockBlueprintSettings : IBlockComponent
{
    string SettingsKey { get; }
    string GetBlueprintSettingsJson();  // 可読JSON。フィルタ等の参照はGUIDで表現する
}
```

- コピー時: 範囲内ブロックの `IBlockBlueprintSettings` 実装コンポーネントから
  `SettingsKey → JSON文字列` を収集し、そのままBPに永続化する（可読・GUIDベース。
  `FilterItemGuids` 等の既存セーブJSONと同じ流儀）。バイナリはセーブに入れない。
- 貼り付け時: クライアントが各設定JSONをUTF8バイト列へ変換して
  `PlaceInfo.BlockCreateParams`（key＋byte[]、通信用の既存機構）に載せ、
  `BlockFactory.Create` → 各テンプレートの `New(…, createParams)` 経路で注入する（`Load` 経路とは別）。
- v1の実装対象: `VanillaFilterSplitterComponent`（方向別フィルタ設定）。他は仕組みだけ用意し順次追加。
- 設定JSONはクライアントでは不透明データとして素通しする（クライアントは解釈しない）。

## プロトコル（新規1本: BP管理プロトコル）

`creating-server-protocol` スキルの Request-Response 型に従う。
1プロトコル＝1ドメインの方針に基づき、BP管理を単一プロトコル `va:blueprint` に集約し、
リクエスト内の `Mode` enum で分岐する（`ElectricWireConnectionEditProtocol` の
`WireEditMode` switch と同じ先行パターン）。

| Mode | 内容 |
|---|---|
| `Create` | 範囲（min/max座標）＋名前 → サーバーが範囲内ブロックを抽出しライブラリへ登録。登録結果（ブロック数）を返す |
| `GetAll` | ライブラリ全件（名前＋blocks）を返す。貼り付けプレビューとPlaceInfo展開に必要なためデータ本体ごと返す |
| `Delete` | 名前指定で削除 |

- コピーの抽出はサーバーが行う（ブロック設定はサーバーにしか存在しないため）。
  レール系ブロック・対象外ブロックはこの抽出時にスキップする。
- 貼り付けは新プロトコルを作らない。クライアントがBPデータを回転・平行移動して
  `List<PlaceInfo>` に展開し、既存 `va:placeBlock` へ送る。

## クライアント（UI・設置システム）

既存の「選択→設置」フローに統合する。

- ビルドメニュー: `BuildMenuEntry` にブループリント種別を追加。
  「コピーツール」1エントリ＋保存済みBPの動的エントリ群（`va:blueprint` GetAll の結果から生成）。
  `BuildMenuEntryCatalog` を動的エントリ対応に拡張する。
- コピーモード（新規 `IPlaceSystem` 実装 `BlueprintCopySystem`）:
  地面ドラッグで矩形を可視化（カーソル下の地面座標取得は既存設置系の計算を流用、矩形化は新規）→
  マウスリリースで名前入力ダイアログ →
  確定で `va:blueprint` Create 送信。ESCキャンセル。削除ツールの「選択→プレビュー→コミット」構造を踏襲。
- 貼り付けモード（新規 `IPlaceSystem` 実装 `BlueprintPasteSystem`）:
  `PlacementSelectionType.Blueprint` を追加し `PlaceSystemSelector` で分岐。
  BP全ブロックのゴーストを既存 `BlockPlacePreviewObjectPool` で表示し、セルごとに設置可否を色分け。
  Rキーで90度回転（オフセットをアンカー周りに回転＋各ブロックに `HorizonRotation()` 適用）。
  左クリックで `PlaceInfo` 展開→送信。連続貼り付け可（モードは維持）。
- アンカー: 選択矩形のXZ中心セル・最下段Y。回転しても貼り付け位置がカーソルから飛ばない。

## エラーハンドリング

- 範囲内に対象ブロックが0個 → BPを作成せずエラー表示（空BPをライブラリに入れない）。
- BP名重複 → サーバー側で連番付与（作成は常に成功させ、リネームUIを持たない設計を補完）。
- ロード時に `blockGuid` がマスタに存在しない（mod構成変更）→ 該当ブロックだけBPから除外して継続。
- 貼り付けの部分失敗 → 既存プロトコルの挙動（スキップ）。クライアントはプレビュー色で事前に可視化。

## テスト

サーバー側 CombinedTest を主軸にする（`creating-server-tests` スキル準拠）。

1. `va:blueprint` Create: 範囲抽出（包含規則・マルチセル・対象外ブロックのスキップ・空範囲拒否）
2. セーブ／ロード往復: `BlueprintDatastore` の永続化と `blockGuid` 欠落時の除外
3. 貼り付け: BPから展開した `PlaceInfo` での設置、`BlockCreateParams` によるフィルタ設定復元
   （フィルタスプリッタで設定が再現されることを直接検証）
4. 回転: 90/180/270度でのオフセット変換と `BlockDirection` 変換の整合（マルチセルブロック含む）
5. `va:blueprint` Delete / GetAll の往復

クライアント側はプレビュー・回転操作をプレイテストDSL（実プレイ検証）で確認する。

## 実装順序（概略）

1. サーバー: `Game.Blueprint`（データモデル・Datastore・セーブ統合）
2. サーバー: 3プロトコル＋`IBlockBlueprintSettings`（フィルタスプリッタ実装）＋テスト
3. クライアント: コピーモード（矩形選択・名前入力）
4. クライアント: 貼り付けモード（ゴースト・回転・送信）＋ビルドメニュー統合
