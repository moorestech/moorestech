# チャレンジカテゴリシステム変更計画

## 概要
現在の一本のツリー構造から、カテゴリごとに分けられたチャレンジツリー構造への変更計画書。

## 現在のシステム構造
- チャレンジは単一のフラットなリストとして管理
- 各チャレンジは`prevChallengeGuids`で前提チャレンジを参照
- 全体で一つの大きなツリー構造を形成

## 新システムの要件
- チャレンジを「カテゴリ」単位でグループ化
- 各カテゴリ内でチャレンジがツリー構造を持つ
- カテゴリ間の依存関係も管理可能

## 変更が必要な箇所

### 1. スキーマ定義の変更

#### 1.1 challenges.ymlの構造変更
- **`VanillaSchema/challenges.yml`**
  - 現在のフラットな配列構造から、カテゴリをトップレベルに持つ構造に変更
  - 新しい構造例：
    ```yaml
    categories:
      - categoryGuid: "category-guid-1"
        categoryName: "基本操作"
        categoryDescription: "ゲームの基本的な操作を学ぶ"
        displayOrder: 1
        initialUnlocked: true
        prevCategoryGuids: []
        challenges:
          - challengeGuid: "challenge-guid-1"
            title: "最初のアイテムを作る"
            # 既存のチャレンジフィールド
      - categoryGuid: "category-guid-2"
        categoryName: "建築"
        # ...
    ```

#### 1.2 チャレンジアクションの修正
- **`VanillaSchema/ref/challengeAction.yml`**
  - `unlockChallengeCategory`アクションタイプの追加
  - カテゴリアンロック時のアクション定義

### 2. サーバー側の実装変更

#### 2.1 マスターデータ関連
- **`Core.Master/ChallengeMaster.cs`**（修正）
  - 内部実装を新しいカテゴリ構造に対応
  - publicメソッドは変更なし（既存の互換性を維持）
  - カテゴリ情報の内部管理
  - カテゴリ内でのチャレンジツリー構築

#### 2.2 ゲームロジック関連
- **`Game.Challenge/ChallengeDatastore.cs`**（修正）
  - カテゴリを考慮したチャレンジ登録処理
  - カテゴリアンロック状態の管理機能追加
  - カテゴリ完了判定（全チャレンジ完了時）　指摘：「カテゴリが完了した」という概念は持たせないでください。カテゴリはあくまでアンロックされているか、ロックされているかというステートでのみ管理してください。
  - カテゴリ進捗率の計算　指摘：進捗率の計算は不要

- **`Game.UnlockState/States/ChallengeCategoryUnlockStateInfo.cs`**（新規）
  - カテゴリのアンロック状態管理
  - セーブ・ロード対応

#### 2.3 チャレンジタスク関連
- **`Game.Challenge/ChallengeTask/Factory/ChallengeFactory.cs`**（修正）
  - カテゴリ情報を考慮したタスク生成

#### 2.4 アクション処理
- **新規アクションクラスの追加**
  - `UnlockChallengeCategoryAction`：カテゴリアンロック処理
  - 既存のアクション処理システムに統合

### 3. ネットワークプロトコルの変更

#### 3.1 既存プロトコルの修正
- **`GetChallengeInfoProtocol`**（修正）
  - カテゴリ構造に対応したレスポンス形式
  - カテゴリごとのチャレンジ一覧とアンロック状態

#### 3.2 イベントパケット
- **`CategoryUnlockedEventPacket`**（新規）
  - カテゴリがアンロックされた時の通知
- **`CategoryCompletedEventPacket`**（新規）
  - カテゴリ内の全チャレンジが完了した時の通知

### 4. セーブデータ構造の変更

#### 4.1 新規セーブデータ
- カテゴリアンロック状態の保存
- カテゴリごとの進捗情報


### 5. テスト関連

#### 5.1 ユニットテスト
- ChallengeMasterのカテゴリ構造対応テスト
- カテゴリアンロックロジックのテスト
- カテゴリ内チャレンジ管理のテスト

#### 5.2 統合テスト
- カテゴリを跨いだチャレンジ進行テスト
- カテゴリ完了時の動作テスト

### 6. 実装順序の推奨

1. **Phase 1: スキーマ定義**
   - challenges.ymlの構造変更
   - challengeAction.ymlへのカテゴリアクション追加

2. **Phase 2: サーバー側実装**
   - ChallengeMaster.csの内部実装修正
   - ChallengeDatastore.csのカテゴリ対応
   - ChallengeCategoryUnlockStateInfo.csの新規作成
   - アクションクラスの追加
   - プロトコルの修正

3. **Phase 3: テスト**
   - ユニットテストの実装
   - 統合テストの実装

## 影響を受ける主要ファイル一覧

### スキーマ
- `VanillaSchema/challenges.yml`（構造変更）
- `VanillaSchema/ref/challengeAction.yml`（カテゴリアクション追加）

### サーバー側
- `Core.Master/ChallengeMaster.cs`（内部実装のみ修正）
- `Game.Challenge/ChallengeDatastore.cs`（カテゴリ対応）
- `Game.UnlockState/States/ChallengeCategoryUnlockStateInfo.cs`（新規）
- `Game.Challenge/ChallengeTask/Factory/ChallengeFactory.cs`
- ネットワークプロトコル関連
  - `GetChallengeInfoProtocol.cs`
  - 新規イベントパケットクラス
- アクションクラス
  - `UnlockChallengeCategoryAction.cs`（新規）

### その他
- セーブデータ関連クラス
- テストクラス