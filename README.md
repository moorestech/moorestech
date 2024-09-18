
# subtreeの設定

## 初期設定
remoteの追加

```sh
git remote add schema git@github.com:moorestech/VanillaSchema.git
git fetch schema
```

## コミットをVanillaSchemaにpush

```sh
git subtree push --prefix=mooresmaster.SandBox/schema schema main 
```

## VanillaSchemaからpull
```sh
git subtree pull --prefix=mooresmaster.SandBox/schema schema main 
```
