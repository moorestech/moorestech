//Credit https://kan-kikuchi.hatenablog.com/entry/DirectoryProcessor

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
///     ディレクトリを操作するクラス
/// </summary>
public static class DirectoryProcessor
{
    /// <summary>
    ///     コピー先を消してから中身をコピーし、コピーしたファイル数を返す
    ///     Deletes the destination first, copies the tree into it and returns the copied file count
    /// </summary>
    public static int CopyAndReplace(string sourcePath, string copyPath, IReadOnlyList<string> excludedFileNames)
    {
        Delete(copyPath);
        return Copy(sourcePath, copyPath, excludedFileNames);
    }

    /// <summary>
    ///     ディレクトリとその中身を上書きコピーし、コピーしたファイル数を返す
    ///     Copies the directory tree with overwrite and returns the copied file count
    /// </summary>
    public static int Copy(string sourcePath, string copyPath, IReadOnlyList<string> excludedFileNames)
    {
        Directory.CreateDirectory(copyPath);

        // Unityの.metaと呼び出し側指定のファイルは成果物に不要なので飛ばす
        // Unity .meta files and the caller's excluded names are useless in an artifact
        var copiedFileCount = 0;
        foreach (var filePath in Directory.GetFiles(sourcePath))
        {
            var fileName = Path.GetFileName(filePath);
            if (fileName.EndsWith(".meta")) continue;
            if (excludedFileNames.Contains(fileName, StringComparer.OrdinalIgnoreCase)) continue;

            File.Copy(filePath, Path.Combine(copyPath, fileName), true);
            copiedFileCount++;
        }

        // ドット始まりはツールのローカル情報なので成果物へ持ち込まない
        // Dot-prefixed directories hold local tooling state and never ship
        foreach (var directoryPath in Directory.GetDirectories(sourcePath))
        {
            var directoryName = Path.GetFileName(directoryPath);
            if (directoryName.StartsWith(".")) continue;

            copiedFileCount += Copy(directoryPath, Path.Combine(copyPath, directoryName), excludedFileNames);
        }

        return copiedFileCount;
    }

    /// <summary>
    ///     指定したディレクトリとその中身を全て削除する
    /// </summary>
    public static void Delete(string targetDirectoryPath)
    {
        if (!Directory.Exists(targetDirectoryPath)) return;

        //ディレクトリ以外の全ファイルを削除
        var filePaths = Directory.GetFiles(targetDirectoryPath);
        foreach (var filePath in filePaths)
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
            File.Delete(filePath);
        }

        //ディレクトリの中のディレクトリも再帰的に削除
        var directoryPaths = Directory.GetDirectories(targetDirectoryPath);
        foreach (var directoryPath in directoryPaths) Delete(directoryPath);

        //中が空になったらディレクトリ自身も削除
        Directory.Delete(targetDirectoryPath, false);
    }
}
