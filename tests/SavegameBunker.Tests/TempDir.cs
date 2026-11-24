using System.IO;

namespace SavegameBunker.Tests;

/// <summary>
/// The one fixture every test class shares: creates a fresh directory under
/// <c>%TEMP%/SavegameBunkerTests/&lt;guid&gt;</c> and always runs the full cleanup — deny-rule
/// removal, junction removal, then a recursive delete — so a test that adds a deny rule or a
/// junction later cannot leave its directory behind.
/// </summary>
internal sealed class TempDir : IDisposable
{
    public string DirectoryPath { get; }

    public TempDir()
    {
        DirectoryPath = Path.Combine(Path.GetTempPath(), "SavegameBunkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
    }

    public void Dispose()
    {
        try
        {
            TestFileSystem.RemoveDenyRules(DirectoryPath);
            TestFileSystem.RemoveJunctions(DirectoryPath);
            Directory.Delete(DirectoryPath, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory must not fail the test run.
        }
        catch (UnauthorizedAccessException)
        {
            // A leftover temp directory must not fail the test run.
        }
    }
}
