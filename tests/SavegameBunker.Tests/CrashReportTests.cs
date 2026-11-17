using SavegameBunker.Services;

namespace SavegameBunker.Tests;

/// <summary>
/// The automated half of the unhandled-exception reporting. The handlers themselves need a real dispatcher loop and are a
/// walkthrough item by the project's own convention; the sentence they show is a pure function and
/// is pinned here.
/// </summary>
public class CrashReportTests
{
    [Fact]
    public void The_report_carries_the_failures_own_message()
    {
        var text = CrashReport.Describe(new InvalidOperationException("The backup folder is read-only."), "backing up");

        Assert.Contains("The backup folder is read-only.", text);
    }

    [Fact]
    public void The_report_names_what_was_happening()
    {
        var text = CrashReport.Describe(new InvalidOperationException("boom"), "backing up");

        Assert.Contains("backing up", text);
    }

    [Fact]
    public void The_report_says_the_application_is_still_running()
    {
        var text = CrashReport.Describe(new InvalidOperationException("boom"), "backing up");

        Assert.Contains("still running", text);
    }

    [Fact]
    public void The_report_never_dumps_a_stack_frame()
    {
        Exception caught;
        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        var text = CrashReport.Describe(caught, "backing up");

        Assert.False(string.IsNullOrEmpty(caught.StackTrace), "the exception needs a stack trace for this test to mean anything");
        Assert.DoesNotContain("   at ", text);
        Assert.DoesNotContain(nameof(CrashReportTests), text);
    }

    [Fact]
    public void A_wrapped_failure_reports_the_innermost_message_not_the_generic_wrapper()
    {
        var inner = new UnauthorizedAccessException("Access to the save folder is denied.");
        var outer = new AggregateException("One or more errors occurred.", inner);

        var text = CrashReport.Describe(outer, "backing up");

        Assert.Contains("Access to the save folder is denied.", text);
        Assert.DoesNotContain("One or more errors occurred.", text);
    }

    [Fact]
    public void A_failure_with_an_empty_message_still_produces_a_readable_report()
    {
        var text = CrashReport.Describe(new EmptyMessageException(), "backing up");

        Assert.Contains("backing up", text);
        Assert.Contains("still running", text);
        Assert.Contains("No further detail was given.", text);
    }

    [Fact]
    public void The_report_uses_no_implementation_vocabulary()
    {
        var text = CrashReport.Describe(new InvalidOperationException("boom"), "backing up");

        Assert.DoesNotContain("Exception", text);
        Assert.DoesNotContain("null", text);
        Assert.DoesNotContain("dispatcher", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_null_failure_is_rejected_rather_than_reported()
    {
        Assert.Throws<ArgumentNullException>(() => CrashReport.Describe(null!, "backing up"));
    }

    /// <summary>An exception whose message really is empty — most built-in types substitute one.</summary>
    private sealed class EmptyMessageException : Exception
    {
        public override string Message => string.Empty;
    }
}
