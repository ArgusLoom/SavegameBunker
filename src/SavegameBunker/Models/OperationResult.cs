namespace SavegameBunker.Models;

/// <summary>
/// Outcome of a filesystem/process operation. Every action reports back so the
/// UI can surface visible feedback (no silent failures per SPEC).
/// </summary>
/// <param name="Cancelled">
/// True when the user declined a confirmation rather than the operation failing. Declining
/// changes nothing, so this is neither a success worth an info dialog nor a failure worth an
/// error one — the caller shows only the status line.
/// </param>
/// <param name="HasWarning">
/// True on a success whose message also says something the user must act on — a backup that left
/// files out, or wrote an archive restore will refuse. A caller that shows only a summary (a bulk
/// run) keys on it to surface the message anyway; it is not set for an informational note.
/// </param>
public sealed record OperationResult(bool Success, string Message, bool Cancelled = false, bool HasWarning = false)
{
    public static OperationResult Ok(string message) => new(true, message);
    public static OperationResult OkWithWarning(string message) => new(true, message, HasWarning: true);
    public static OperationResult Fail(string message) => new(false, message);
    public static OperationResult Cancel(string message) => new(false, message, Cancelled: true);
}
