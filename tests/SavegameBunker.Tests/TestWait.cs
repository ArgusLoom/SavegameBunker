namespace SavegameBunker.Tests;

/// <summary>
/// The one async polling helper every test class shares. The test host has no
/// <see cref="System.Threading.SynchronizationContext"/>, so <see cref="Progress{T}"/> reports
/// land on the thread pool and can arrive late — this asserts settled state by polling, never a
/// snapshot taken right after a command finishes.
/// </summary>
internal static class TestWait
{
    /// <summary>The ordinary budget for view-model state that settles without real disk work.</summary>
    public const int DefaultTimeoutMs = 3000;

    /// <summary>
    /// For waits that cover a real zip write and verify, which under full-suite parallel load
    /// were observed to need more than the default — a named budget, not a cure for a flake.
    /// </summary>
    public const int RealIoTimeoutMs = 15000;

    /// <summary>
    /// The one kind of fixed wait the suite allows — proving something did NOT happen, where
    /// there is no state to poll for.
    /// </summary>
    public const int NegativeProofDelayMs = 300;

    public static async Task WaitForAsync(Func<bool> condition, int timeoutMs = DefaultTimeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(5);

        Assert.True(condition(), "Timed out waiting for the expected view model state.");
    }
}
