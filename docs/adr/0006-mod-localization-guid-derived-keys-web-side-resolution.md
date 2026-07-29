# 0006: mod同梱辞書とGuid導出キー、名前解決のWeb側統一

日付: 2026-07-29
状態: 採択

## 背景

アイテム名・ブロック名はゲーム内で最も分量の多いユーザー可視テキストだが、マスタデータの `name` は `"小石"` のような単一日本語文字列で、ロケール軸が存在しない。この生文字列が Unity 側約20箇所、Web 側は ItemMasterEndpoint / BlockInventoryTopic / BuildMenuEntryDtoFactory 等のホスト解決 payload 経由で UI に直結していた。mod 合成は未実装（MasterHolder が mod[0] 固定）、出荷 mod の modMeta.json の id は空文字という状態。

## 決定

1. **modはローカライズCSVを同梱する**: `mods/<mod>/localization/localization.csv`。フォーマットはバニラCSVと完全同一（`key,Source,english,japanese,...`）。パーサー1本・翻訳者に渡す単位が1ファイル。
2. **マスタ由来テキストのキーは `<type>.<guid>.<field>` の規約で自動導出する**（例: `item.<guid>.name`）。コードや辞書にキーをベタ書きせず、Guid から動的に構築する。
3. **キーに modId は含めない。** Guid が既にグローバル一意であり、mod id は「辞書の出所」であってキーの一部ではない。翻訳modが他modのアイテムの翻訳を提供することも自然に可能になる。
4. **合成済み辞書の正本はクライアント側 Localize（後継）が持つ。** 起動時にバニラ埋め込み辞書＋全mod CSVを単一辞書へ合成する。マスタJSONと同じ「同一ディレクトリ直読み」前提でサーバーは非関与。Webへは既存 `/api/i18n` 配信を維持。
5. **ホスト側の Name 解決・payload 同梱を全廃し、Web は Guid から辞書解決に統一する。** 言語切替はトピック再push不要でWeb側の再描画だけで完結する。
6. **modキーの未翻訳フォールバックは 対象言語 → english → master の name 原文 →（それも無ければ）`[!key]`。** mod制作者が辞書を書かないのは正常状態として扱う。
7. **master の `name` フィールドは原文（フォールバック表示元）として維持する。** スキーマに言語マップは入れない。
8. **character masterへ必須 `characterGuid` を追加する。** 全characters JSONを一括更新し、既存 `characterId` はスキット実行時の操作IDとして維持する。表示名の導出キーだけを `character.<characterGuid>.name` とし、optional・欠損フォールバックは設けない。
9. **buildMenuのカテゴリとサブカテゴリへ必須Guidを追加する。** 名前を識別子にせず、全buildMenu JSONを一括更新して導出キーにGuidを使う。
10. **既存 `Skit/i18n/{english,japanese}.json` は削除しない。** CommandForgeEditorが `<projectPath>/i18n/*.json` から動的ロードする正式なプロジェクト辞書として `command.*` / `master.*` を維持し、ゲーム台詞用の `skit.<skitTitle>.<commandId>.<field>` を同じ `translations` へ追加できる正本へ拡張する。
11. **ゲームはskit開始時に対象言語とenglishのSkit専用辞書だけをAddressablesから動的ロードする。** `skit.` の非空翻訳だけを取り込み、mod合成済み辞書へ欠けているキーだけ追加する。空文字は欠落として次段へ進み、解決順は `mod対象言語 → skit専用対象言語 → mod英語 → skit専用英語 → skit JSON原文` とする。全skit JSONの事前ロードは行わない。
12. **Skit titleの正本はAddressable assetのbasename（runtimeの `TextAsset.name`）とする。** JSON `meta.title` はキー導出に使わず、完全性テストでbasenameと一致することだけを検証する。runtime/testは同じ `SkitTitle.FromAssetName` を通してキーを作る。

## 却下した選択肢

- **スキーマに言語別マップを内包**（items.yml の name を言語マップ化） — 翻訳作業がマスタ編集と密結合し、翻訳者に渡すファイルを分離できないため却下。出所: ユーザー裁定 2026-07-29（AskUserQuestion「マスタ名方式」）
- **name をローカライズキー文字列にする**（旧CSV英語キー方式の復活） — 全マスタの name 書き換えが必要で、未翻訳時に生キーが露出するため却下。出所: 同上
- **言語別JSON / master/ 統合（JSONマスタ化）** — パーサー二重化、または MasterHolder への役割過多のため却下。出所: ユーザー裁定 2026-07-29（AskUserQuestion「mod辞書形式」）
- **サーバー側合成＋プロトコル配信** — マスタ自体がネットワークを渡っていない現行アーキに対し先行しすぎるため却下。出所: ユーザー裁定 2026-07-29（AskUserQuestion「辞書の正本」）
- **ホスト解決の維持＋言語切替時全トピック再push** — ロケール依存 payload が各所に残り再push漏れがバグ源になるため却下。出所: ユーザー裁定 2026-07-29（AskUserQuestion「名前解決場所」）
- **english を挟まないフォールバック**（対象言語→name原文） — 却下し english を優先。出所: ユーザー裁定 2026-07-29「modキーについて、未翻訳のフォールバックは 英語 → nameの原文 にして」

## 帰結

- ItemMasterEndpoint の DTO から Name が消え、BlockInventoryTopic / MachineRecipesTopic / BuildMenuEntryDtoFactory のインライン名前解決を削除する（波及は一括更新で受ける）。
- 初回スコープ: item/block の name、研究・チャレンジ等の文言、skit台詞。レガシーuGUI文言（KeyControlDescription 等）は対象外。出所: ユーザー裁定 2026-07-29（AskUserQuestion「初回スコープ」）
- skit本文・背景本文・選択肢・上書き話者名はCommandForge command schemaの正確なプロパティ名をfieldに使い、同じcommandIdからキーを導出する。Webは従来どおりUnityからpush済み表示文字列を受け取る。
- `Client.Skit` へ `Localize` を直接依存させない。汎用層にresolver interfaceを置き、`Client.Game`側のAddressables loader/具体resolverをStoryContextへ登録する。
- modMeta.json の id 空文字（スキーマ required 違反）はキー設計と無関係になったが、別途修理する。
