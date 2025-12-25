---
name: master-schema-editing
description: マスターデータのスキーマ編集（VanillaSchema/*.yml）、新規スキーマ追加、フィールド追加/削除、SourceGenerator入力更新、`moorestech_server/Assets/Scripts/Core.Master/csc.rsp` の /additionalfile 更新、`Dummy.cs` の dummyText 更新、JSONマスター同期、MasterHolder/各Masterの追加・更新が絡む作業で使用する。
---

# Master Schema Editing

## Overview

マスターデータのスキーマ変更を、SourceGenerator・ランタイム・JSONの整合を保ちながら進めるための手順を示す。既存パターンを優先し、最小差分で実装する。

## Workflow

### 1. 目的確認と既存パターン調査

- XY問題を避けるため、根本目的を先に確認する
- 対象スキーマを `VanillaSchema/*.yml` または `VanillaSchema/ref/*.yml` から特定する
- `rg` で類似フィールドや命名規則を確認する

### 2. YAMLスキーマを編集

- 追加/変更/削除を実施する
- `optional: false` を基本とし、nullableが必須のときのみ `optional: true` を使う
- 既存の参照/外部キーの設計と命名規則に合わせる

### 3. csc.rsp を更新

- スキーマ追加・移動時は `moorestech_server/Assets/Scripts/Core.Master/csc.rsp` を更新する
- `/additionalfile:Assets/../../VanillaSchema/...` の形式で追加する
- `VanillaSchema/ref` の参照は既存の並びに揃える

### 4. SourceGenerator を再生成

- `moorestech_server/Assets/Scripts/Core.Master/Dummy.cs` の `dummyText` を新しい値に変更する
- リビルド/コンパイルで `Mooresmaster.Model.*` を生成する
- 生成コードは手動編集しない

### 5. ランタイム配線の更新

- 新規マスター追加時は `MasterHolder` と対応する Master クラスを実装する
- 既存のAPI/アクセスパターンを維持する

### 6. JSONマスターの同期

- `mods/*/master/*.json` をスキーマ変更に合わせて更新する
- テスト依存がある場合は以下も更新する
  - `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/`
  - `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs`

### 7. ドキュメント確認とコンパイル

- サーバー側変更時は `docs/ServerGuide.md` を読む
- 編集後は必ずコンパイルを実行する
