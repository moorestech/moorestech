[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/moorestech/moorestech)



# moorestech

工業ゲームmoorestechのUnityのサーバー、クライアントです

# 起動方法
mooresech_clientをUnityで開きMainGameシーンを再生してください

# コーディングエージェントの使用について

## codex cli を使用する場合の注意事項

`tools/unity-test.sh` スクリプトを Codex CLI 経由で実行する際は、Unity のライセンス認証にネットワークアクセスが必要となります。

デフォルトのサンドボックス設定ではネットワークアクセスが制限されているため、`--dangerously-bypass-approvals-and-sandbox` (`--yolo`) オプションを指定してClaude Code CLIを起動してください。

```bash
codex --yolo
```

MCPを使う場合はこの限りではありません。
~/.codex/config.toml
```toml
[mcp_servers.moorestech_server]
command = "node"
args    = ["/Users/[user]/mcp-stdio-to-streamable/dist/index.js"]
env = { MCP_SERVER_IP = "localhost", MCP_SERVER_PORT = "56901" }

[mcp_servers.moorestech_client]
command = "node"
args    = ["/Users/[user]/mcp-stdio-to-streamable/dist/index.js"]
env = { MCP_SERVER_IP = "localhost", MCP_SERVER_PORT = "56902" }
```

# LICENSE
コードは[Apache2.0](https://github.com/moorestech/moorestech/blob/master/LICENSE) ライセンスで配布されています。
コード以外の各種コンテンツは[moorestech Game Content License ](https://github.com/moorestech/moorestech/blob/master/CONTENT_LICENSE.md) ライセンスでは配布されています。

### OSSに関する表記


TODO この辺ちゃんとする

VContainer
https://github.com/hadashiA/VContainer

NaughtyCharacter
https://github.com/dbrizov/NaughtyCharacter

Microsoft.Extensions.DependencyInjection  
https://github.com/aspnet/DependencyInjection

CsvHelper
https://github.com/JoshClose/CsvHelper

AllSkyFree_Godot
https://github.com/rpgwhitelock/AllSkyFree_Godot


### フォントラインセンスに関する表記

Noto Sans Japanese
https://fonts.google.com/noto/specimen/Noto+Sans+JP

Noto Sans
https://fonts.google.com/noto/specimen/Noto+Sans


