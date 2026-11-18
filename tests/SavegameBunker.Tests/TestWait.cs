namespace SavegameBunker.Tests;

/// <summary>
/// The one async polling helper every test class shares. The test host has no
/// <see cref="System.Threading.SynchronizationContext"/>, so <see cref="Progress{T}"/> reports
/// land on the thread pool and can arrive late — this asserts settled state by polling, never a
/// snapshot taken right after a command finishes.
/// </summary>
internal static class TestWait
{
    public static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(5);

        Assert.True(condition(), "Timed out waiting for the expected view model state.");
    }
}
