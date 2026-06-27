using System.IO;
using UnityEditor;
using UnityEngine;

namespace Client.Editor.RepositorySync
{
    public static class ExternalRepositoryRevisionStore
    {
        public static string GetRepositoryRootPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        }

        public static string GetRevisionFilePath()
        {
            return Path.Combine(GetRepositoryRootPath(), ExternalRepositoryRevisionDefaults.RevisionFileName);
        }

        public static ExternalRepositoryRevisionFile Load()
        {
            var path = GetRevisionFilePath();
            if (!File.Exists(path)) return ExternalRepositoryRevisionDefaults.CreateRevisionFile();

            var json = File.ReadAllText(path);
            var file = JsonUtility.FromJson<ExternalRepositoryRevisionFile>(json);
            if (file == null || file.repositories == null) return ExternalRepositoryRevisionDefaults.CreateRevisionFile();

            return file;
        }

        public static void Save(ExternalRepositoryRevisionFile file)
        {
            var path = GetRevisionFilePath();

            // 人がレビューしやすい形式でrootの記録ファイルを更新する
            // Write the root revision file in a readable format for review
            var json = EditorJsonUtility.ToJson(file, true);
            File.WriteAllText(path, json);
        }
    }
}
