using System.Reflection;

namespace Client.Editor.StandaloneQa
{
    public static class StandaloneTerrainQaBuildPipeline
    {
        public static void BuildMacOs()
        {
            // 同一Editor assemblyのクライアント用CI入口へ委譲し、サーバー側の同名型を除外する
            // Delegate to the client CI entry in this Editor assembly, excluding the same-named server type
            var clientPipelineType = typeof(StandaloneTerrainQaBuildPipeline).Assembly.GetType("BuildPipeline");
            var buildMethod = clientPipelineType.GetMethod(
                "MacOsBuildFromGithubAction",
                BindingFlags.Public | BindingFlags.Static);
            buildMethod.Invoke(null, null);
        }
    }
}
