namespace Client.Editor.RepositorySync
{
    public class GitCommandResult
    {
        public readonly int exitCode;
        public readonly string standardOutput;
        public readonly string standardError;

        public GitCommandResult(int exitCode, string standardOutput, string standardError)
        {
            this.exitCode = exitCode;
            this.standardOutput = standardOutput;
            this.standardError = standardError;
        }
    }
}
