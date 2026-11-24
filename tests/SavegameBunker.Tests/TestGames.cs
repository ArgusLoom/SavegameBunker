using System.IO;
using SavegameBunker.Models;

namespace SavegameBunker.Tests;

/// <summary>
/// The two game-with-saves builders the service and coordinator tests share: two names because
/// both shapes take three strings, so an overload would be ambiguous.
/// </summary>
internal static class TestGames
{
    public static Game WithSaveFile(string dir, string fileName = "save.dat", string content = "payload")
    {
        var saveDir = Path.Combine(dir, "saves");
        Directory.CreateDirectory(saveDir);
        File.WriteAllText(Path.Combine(saveDir, fileName), content);

        return new Game
        {
            Id = "test-game",
            DisplayName = "Test Game",
            SaveDirTemplate = saveDir.Replace('\\', '/'),
        };
    }

    public static Game WithNamedSaves(string dir, string id, string displayName)
    {
        var saveDir = Path.Combine(dir, "saves-" + id);
        Directory.CreateDirectory(saveDir);
        File.WriteAllText(Path.Combine(saveDir, "save.dat"), "payload");

        return new Game
        {
            Id = id,
            DisplayName = displayName,
            SaveDirTemplate = saveDir.Replace('\\', '/'),
        };
    }
}
