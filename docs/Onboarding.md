

# アーキテクチャ概要

moorestechは完全分離型クライアント・サーバーアーキテクチャを採用した自動化工業ゲームです。

## サーバーの役割

TODO 3行ぐらいで

## クライアントの役割

TODO 3行ぐらいで


## プロジェクト構成

```
moorestech/
├── moorestech_server/    # サーバー側（ゲームロジック、ワールド状態管理）
│   └── Assets/Scripts/
│       ├── Server.Boot/           # 起動・DI設定
│       ├── Server.Protocol/       # 通信層
│       ├── Game.World/            # ワールド・ブロック配置
│       ├── Game.Block/            # ブロック実装
│       ├── Game.Entity/           # エンティティ管理
│       ├── Game.PlayerInventory/  # プレイヤーインベントリ
│       ├── Game.Context/          # ゲームコンテキスト
│       └── Core.*/                # 基盤システム
│
├── moorestech_client/    # クライアント側（UI、描画、入力）
│   └── Assets/Scripts/
│       ├── Client.Starter/        # 起動・DI設定
│       ├── Client.Network/        # サーバー通信
│       ├── Client.Game/           # ゲーム状態
│       └── Client.Game.InGame.*/  # UI・表示
│
└── VanillaSchema/        # マスターデータスキーマ（YAML）
    ├── items.yml         # SourceGenerator入力
    ├── blocks.yml
    └── ...
```

## 共通アーキテクチャ

### DIコンテナ
初期化の際、DIコンテナを使って依存を解決する
クライアントはVContainer、サーバーはMicrosoft Dipendency Injectionを使用

### ゲームコンテキスト
ServerContextやClientContextを利用して、よくアクセスするインスタンスに一発でアクセスできるようにする

### マスターデータについて
MasterHolderから各種マスターデータをstaticアクセスで簡単に取得できるようにしてる。
MasterHolderの各要素が、マスターごとに便利にアクセスできるメソッドを提供している。

### UniRxの使用
プロジェクト全体としてUniRxを参照しているが、Rxは可読性やデータフローのトレーサビリティを下げるという思想のもと、必要な箇所にのみIObservableとSubjectを使用するのみにとどめている。
あくまでC#のeventを使いやすくするだけのライブラリとしてのみ使用している。



# サーバーアーキテクチャ
TODO

## 全体アーキテクチャ
TODO ざっくり Core ← Game ← Server という関係になっていることを明示

## Coreの役割
TODO

## Gameの役割
TODO

## Serverの役割
TODO

### プロトコルの設計思想
TODO
- ステートレス。各リクエストが独立している
- クライアント主導。クライアントからリクエストが来て、初めてサーバーがデータを返したりサーバーの状態を変更したりする。サーバーから能動的にデータを送ることはない。
- MessagePackを利用してデータをシリアライズする

### サーバーイベントシステムについて
TODO
- クライアント主導でデータをとってくるが、サーバー側で起きたことをクライアントに伝えたいことはある。（ブロックの内部状態の更新など）
- クライアント側の責務でイベントリクエストパケットをポーリングする
- サーバー側は各ユーザーごとにイベントデータを保持しておき、クライアントからイベントのリクエストが来た時に溜めておいたイベントを返す。

## サーバーの初期化
TODO


## セーブ、ロードシステム
TODO


# クライアントアーキテクチャ
TODO

# 全体アーキテクチャ
TODO

# Networkの役割
TODO

# InGameの役割

TODO
そもそも、ゲームの仕様として、InGameは各要素間が複雑に絡み合う
大まかに、上位レイヤーっぽい動きをするもの（UIState）や、各要素の詳細レイヤーを実装するものという区分けがあるが、実装速度や、下手に分離してデータフローがわかりづらくなるよりかは、アセンブリを分けず、素直に参照することにする。

また、各モジュール間のSeralizeFieldをなるべく避け、Injectによって注入することにより、ゆるく循環参照を避けるようなアーキテクチャをとった。

# Skitの役割
TODO


