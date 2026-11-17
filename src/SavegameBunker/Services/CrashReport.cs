namespace SavegameBunker.Services;

/// <summary>
/// Composes what the user reads when an unexpected failure is caught by one of the two
/// application-wide handlers wired in <c>App.OnStartup</c>.
/// </summary>
/// <remarks>
/// This exists as a pure function so the wording is testable without a dispatcher: no xUnit host
/// runs a real WPF message loop, so the handlers themselves are a walkthrough item, but the
/// sentence they show is not. The wording obeys three rules — plain language with no
/// implementation vocabulary, it names what failed in the user's terms and carries the failure's
/// own message, and it says SavegameBunker is still running. It deliberately says <em>nothing</em>
/// about whether the failed action wrote anything, because at this level that is unknowable and a
/// reassuring guess is worse than silence.
/// </remarks>
public static class CrashReport
{
    /// <summary>
    /// Describes <paramref name="ex"/> for a user, attributing it to <paramref name="source"/> —
    /// a short phrase naming what was happening, not a type or method name.
    /// </summary>
    public static string Describe(Exception ex, string source)
    {
        ArgumentNullException.ThrowIfNull(ex);

        return Strings.Format("str_crash_unexpected", source, Detail(ex));
    }

    /// <summary>
    /// The innermost message, because a wrapped exception's outer message is usually the generic
    /// one ("One or more errors occurred") while the inner one says what actually went wrong.
    /// Never a stack trace: a user cannot act on a stack frame, and pasting one into a dialog is
    /// how an application tells someone it does not respect them.
    /// </summary>
    private static string Detail(Exception ex)
    {
        var innermost = ex;
        while (innermost.InnerException is not null)
            innermost = innermost.InnerException;

        var message = innermost.Message;

        return string.IsNullOrWhiteSpace(message)
            ? Strings.Get("str_crash_noDetail")
            : message;
    }
}
