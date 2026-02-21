#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Rider の解析エンジンが Microsoft.Bcl.Memory と mscorlib の System.Index 重複で
/// 「型 'System.Index' は解決されません」エラーを出す問題を修正する。
/// csproj 生成時に Microsoft.Bcl.Memory の参照を削除する。
/// サーバー・クライアント両方の csproj 生成に適用される（クライアントはローカルパッケージ経由で参照）。
/// </summary>
public class CsprojFixBclMemory : AssetPostprocessor
{
    public static string OnGeneratedCSProject(string path, string content)
    {
        // csproj から Microsoft.Bcl.Memory 関連行を除去
        // Remove Microsoft.Bcl.Memory entries from generated csproj
        var lines = content.Split('\n');
        var result = new System.Text.StringBuilder();
        var skipUntilCloseTag = false;

        foreach (var line in lines)
        {
            if (line.Contains("Microsoft.Bcl.Memory"))
            {
                if (line.Contains("<Reference"))
                {
                    skipUntilCloseTag = true;
                    continue;
                }
                continue;
            }

            if (skipUntilCloseTag)
            {
                if (line.Contains("</Reference>") || line.TrimEnd().EndsWith("/>"))
                {
                    skipUntilCloseTag = false;
                }
                continue;
            }

            result.Append(line);
            result.Append('\n');
        }

        return result.ToString();
    }
}
#endif

/*
# Rider で `[^1]` が「型 'System.Index' は解決されません」エラーになる問題

## 現象

- Rider のコードエディタ上で `[^1]`（C# 8.0 Index 構文）に赤波線が表示される
- エラーメッセージ: **「型 'System.Index' は解決されません」**
- Unity のコンパイルは正常に成功する
- Rider のビルド（コンパイル）も成功する
- エラーコード（CSxxxx）は表示されない
- Alt+Enter によるクイックフィックスも表示されない

## 環境

- Unity 6000.3.8f1（Unity 6）
- JetBrains Rider 2025.3.2
- com.unity.ide.rider 3.0.39
- LangVersion: 9.0
- TargetFrameworkVersion: v4.7.1（Unity が csproj に自動設定）

## 根本原因

### `Microsoft.Bcl.Memory` パッケージの `System.Index` 定義が Rider の型解決を妨害

Unity の `mscorlib.dll`（unity-4.8-api）には `System.Index` と `System.Range` が **TypeDef**（型の実体）として定義されている。
しかし、プロジェクトが参照する `Microsoft.Bcl.Memory` パッケージにも同じ型が含まれており、**型の重複**が Rider の解析エンジンを混乱させていた。

#### 型解決チェーンの詳細

```
mscorlib.dll (unity-4.8-api)
  → TypeDef: System.Index ✓（本来ここから解決されるべき）

Microsoft.Bcl.Memory.dll (netstandard2.1 版)
  → TypeForwarder: System.Index → netstandard → mscorlib（転送のみ）

Microsoft.Bcl.Memory.dll (net462 版)
  → TypeDef: System.Index ✓（実体が重複定義）

netstandard.dll (unity-4.8-api/Facades)
  → TypeForwarder: System.Index → mscorlib
```

Rider の解析エンジンは、`mscorlib.dll` と `Microsoft.Bcl.Memory.dll` の両方に `System.Index` が存在する（TypeDef または TypeForwarder として）状況を正しく解決できず、「型が解決されません」エラーを表示していた。

#### なぜ Unity のコンパイルは成功するか

Unity は `.csproj` を使ってコンパイルしない。Unity 独自のビルドパイプライン（内蔵 Roslyn コンパイラ）を使用しており、アセンブリ参照の解決方法が Rider の解析エンジンとは異なるため、型の重複を正しく処理できる。

#### なぜ Rider のビルドは成功するか

Rider の「ビルド」は MSBuild を使用し、HintPath に基づいて実際のアセンブリを参照する。MSBuild の型解決は Rider のリアルタイムコード解析エンジンとは別のプロセスであり、型の重複を正しく処理できる。エラーが出るのは **リアルタイムコード解析エンジンのみ**。

## 修正方法

### 方法: csproj 生成時に `Microsoft.Bcl.Memory` の参照を削除する

Unity は `.csproj` を IDE 向けに自動生成するため、直接編集しても上書きされる。`AssetPostprocessor.OnGeneratedCSProject` コールバックを使用して、生成時に自動で修正する。

#### Editor スクリプトの配置

以下のスクリプトを **サーバーとクライアント両方の** `Assets/Scripts/Editor/` に配置する。

```csharp
#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Rider の解析エンジンが Microsoft.Bcl.Memory と mscorlib の System.Index 重複で
/// 「型 'System.Index' は解決されません」エラーを出す問題を修正する。
/// csproj 生成時に Microsoft.Bcl.Memory の参照を削除する。
/// </summary>
public class CsprojFixBclMemory : AssetPostprocessor
{
    public static string OnGeneratedCSProject(string path, string content)
    {
        // Microsoft.Bcl.Memory の Reference 行と HintPath 行を削除
        // 正規表現を使わず行単位で処理
        var lines = content.Split('\n');
        var result = new System.Text.StringBuilder();
        var skipUntilCloseTag = false;

        foreach (var line in lines)
        {
            if (line.Contains("Microsoft.Bcl.Memory"))
            {
                // <Reference Include="Microsoft.Bcl.Memory"> の場合、</Reference> まで読み飛ばす
                if (line.Contains("<Reference"))
                {
                    skipUntilCloseTag = true;
                    continue;
                }
                // <None Include="...Microsoft.Bcl.Memory..." /> 等の単独行もスキップ
                continue;
            }

            if (skipUntilCloseTag)
            {
                if (line.Contains("</Reference>") || line.TrimEnd().EndsWith("/>"))
                {
                    skipUntilCloseTag = false;
                }
                continue;
            }

            result.Append(line);
            result.Append('\n');
        }

        return result.ToString();
    }
}
#endif
```

#### 適用手順

1. 上記スクリプトを以下の2箇所に配置:
   - `moorestech_server/Assets/Scripts/Editor/CsprojFixBclMemory.cs`
   - `moorestech_client/Assets/Scripts/Editor/CsprojFixBclMemory.cs`
2. Unity で **Assets → Open C# Project** を実行して csproj を再生成
3. Rider でプロジェクトをリロード
4. `[^1]` のエラーが消えたことを確認

## 検証で否定された仮説

以下は調査の過程で試行し、**効果がなかった**アプローチ:

| # | 仮説 | 結果 |
|---|------|------|
| 1 | `ImplicitlyExpandNETStandardFacades` → true | 効果なし |
| 2 | `TargetFrameworkVersion` → v4.8 | 効果なし |
| 3 | `TargetFrameworkVersion` → v4.8.1 | 効果なし |
| 4 | `NoStdLib` → false | 効果なし |
| 5 | `ImplicitlyExpandDesignTimeFacades` → true | 効果なし |
| 6 | 上記全部同時適用 | 効果なし |
| 7 | `_TargetFrameworkDirectories` → 実パス | 効果なし |
| 8 | 実パス + v4.8 組み合わせ | 効果なし |
| 9 | `TargetFramework` → netstandard2.1 | 効果なし |
| 10 | `_TargetFrameworkDirectories` 削除 | 効果なし |
| 11 | `.editorconfig` で CS0518/CS0656/CS8652 抑制 | 効果なし（CSコードではないため） |
| 12 | ソースコード `System.Index` ポリフィル | 効果なし |
| 13 | Rider MSBuild 設定変更 | 選択肢なし |
| 14 | Rider キャッシュ無効化 | 全環境で再現するため無関係 |

## 調査で判明した技術的事実

- Unity 6 の `mscorlib.dll`（unity-4.8-api）は `System.Index`/`System.Range` を TypeDef として含む
- `netstandard.dll`（unity-4.8-api/Facades）は mscorlib への TypeForwarder を含む
- `Microsoft.Bcl.Memory` の netstandard2.1 版は TypeForwarder のみ、net462 版は TypeDef を含む
- Rider の「Search Everywhere」で `System.Index` は XML ドキュメント内にのみ見つかり、型としては存在しなかった → Rider の型モデルに型が登録されていなかった
- `Microsoft.Bcl.Memory` の参照を削除すると、Rider は `mscorlib.dll` から `System.Index` を正しく解決できるようになった

*/