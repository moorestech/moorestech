
# subtreeの設定

## 初期設定
remoteの追加

```sh
git remote add schema git@github.com:moorestech/VanillaSchema.git
```

## コミットをVanillaSchemaにpush

```sh
git subtree push --prefix=mooresmaster.SandBox/schema schema main 
```

## Vani;aSchemaからpull
```sh
git subtree pull --prefix=mooresmaster.SandBox/schema schema main 
```
