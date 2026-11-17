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
public sealed record OperationResult(bool Success, string Message, bool Cancelled = false)
{
    public static OperationResult Ok(string message) => new(true, message);
    public static OperationResult Fail(string message) => new(false, message);
    public static OperationResult Cancel(string message) => new(false, message, Cancelled: true);
}
