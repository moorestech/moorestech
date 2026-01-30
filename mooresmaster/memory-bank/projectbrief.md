# Mooresmaster プロジェクト概要

## プロジェクト名
Mooresmaster - JSONスキーマベースのC#コード生成ツール

## 目的
YAMLスキーマファイルからC#のデータクラスとローダーコードを自動生成するRoslynソースジェネレーター

## 主要機能
- YAMLスキーマファイルの解析
- JSONスキーマからC#クラス定義の生成
- データローダーコードの自動生成
- 型安全なデータアクセス機能の提供

## プロジェクト構成
- **mooresmaster.Generator**: メインのソースジェネレーター実装
- **mooresmaster.Tests**: ユニットテストプロジェクト
- **mooresmaster.SandBox**: 動作確認用サンプルプロジェクト

## 技術スタック
- .NET Standard
- Roslyn Source Generators
- YamlDotNet (組み込み版)
- Newtonsoft.Json

## 主要な処理フロー
1. YAMLファイルの読み込み
2. JSONスキーマへの変換
3. セマンティクス解析
4. 名前解決
5. 定義生成
6. C#コード生成
7. ローダーコード生成

## 対象ユーザー
- ゲーム開発者（特にUnity開発者）
- データドリブンなアプリケーション開発者
- 設定ファイルやマスターデータを扱う開発者