// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Docs.Build
{
    public static class GitUtilityTest
    {
        [Theory]
        [InlineData("README.md")]
        public static void GetRepoInfoSameAsGitExe(string file)
        {
            Assert.False(GitUtility.IsRepo(Path.GetFullPath(file)));

            var repo = GitUtility.FindRepo(Path.GetFullPath(file));
            Assert.NotNull(repo);

            var (remote, branch, commit) = GitUtility.GetRepoInfo(repo);
            Assert.Equal(Exec("git", "config --get remote.origin.url", repo), remote);
            Assert.Equal(Exec("git", "rev-parse --abbrev-ref HEAD", repo), branch ?? "HEAD");
            Assert.Equal(Exec("git", "rev-parse HEAD", repo), commit);
        }

        [Theory]
        [InlineData("README.md")]
        public static async Task GetCommitsSameAsGitExe(string file)
        {
            Assert.False(GitUtility.IsRepo(Path.GetFullPath(file)));

            var repo = GitUtility.FindRepo(Path.GetFullPath(file));
            Assert.NotNull(repo);

            using (var commitsProvider = await GitCommitProvider.Create(repo))
            {
                var pathToRepo = PathUtility.NormalizeFile(file);

                // current branch
                var exe = Exec("git", $"--no-pager log --format=\"%H|%cI|%an|%ae\" -- \"{pathToRepo}\"", repo);
                var lib = commitsProvider.GetCommitHistory(pathToRepo);

                Assert.Equal(
                    exe.Replace("\r", ""),
                    string.Join("\n", lib.Select(c => $"{c.Sha}|{c.Time.ToString("s")}{c.Time.ToString("zzz")}|{c.AuthorName}|{c.AuthorEmail}")));

                // another branch
                exe = Exec("git", $"--no-pager log --format=\"%H|%cI|%an|%ae\" origin/test -- \"{pathToRepo}\"", repo);
                lib = commitsProvider.GetCommitHistory(pathToRepo, "test");

                Assert.Equal(
                    exe.Replace("\r", ""),
                    string.Join("\n", lib.Select(c => $"{c.Sha}|{c.Time.ToString("s")}{c.Time.ToString("zzz")}|{c.AuthorName}|{c.AuthorEmail}")));

                await commitsProvider.SaveCache();
            }
        }

        [Fact]
        public static async Task GitCommandConcurreny()
        {
            var cwd = GitUtility.FindRepo(Path.GetFullPath("README.md"));

            var results = await Task.WhenAll(Enumerable.Range(0, 10).AsParallel().Select(i => GitUtility.Revision(cwd)));

            Assert.True(results.All(r => r.Any()));
        }

        private static string Exec(string name, string args, string cwd)
        {
            var p = Process.Start(new ProcessStartInfo { FileName = name, Arguments = args, WorkingDirectory = cwd, RedirectStandardOutput = true });
            return p.StandardOutput.ReadToEnd().Trim();
        }
    }
}
