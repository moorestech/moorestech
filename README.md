# moorestech



工業ゲームmoorestechのUnityのサーバー、クライアントです

# 起動方法
mooresech_server、clientともにUnityで開き、serverを再生した後、clientのMainGameシーンを再生してください


# subtreeの設定

## 初期設定
remoteの追加

```sh
git remote add schema git@github.com:moorestech/VanillaSchema.git
git fetch schema
```

## コミットをVanillaSchemaにpush

```sh
git subtree push --prefix=schema schema main 
```

## Vani;aSchemaからpull
```sh
git subtree pull --prefix=schema schema main 
```


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


