using SavegameBunker.Models;
using SavegameBunker.Services;
using SavegameBunker.ViewModels;

namespace SavegameBunker.Tests;

/// <summary>
/// Exercises <see cref="AboutDialogViewModel.OpenHelpCommand"/> — the user-guide link the About
/// window carries forward from the old toolbar Help button, with the same three cases that button
/// had: success, failure and the UI-culture pass-through.
/// </summary>
public sealed class AboutDialogViewModelTests
{
    [Fact]
    public async Task Help_link_reports_success_through_the_info_surface()
    {
        var dialog = new RecordingDialogService { HelpGuideResultToReturn = OperationResult.Ok(Strings.Get("str_help_opened")) };
        var vm = new AboutDialogViewModel(dialog);

        await vm.OpenHelpCommand.ExecuteAsync(null);

        var info = Assert.Single(dialog.InfoMessages);
        Assert.Equal(Strings.Get("str_help_opened"), info);
        Assert.Empty(dialog.ErrorMessages);
        Assert.Single(dialog.HelpGuideLanguages);
    }

    [Fact]
    public async Task Help_link_reports_a_failure_as_an_error()
    {
        var dialog = new RecordingDialogService { HelpGuideResultToReturn = OperationResult.Fail(Strings.Format("str_help_failed", "no browser")) };
        var vm = new AboutDialogViewModel(dialog);

        await vm.OpenHelpCommand.ExecuteAsync(null);

        var error = Assert.Single(dialog.ErrorMessages);
        Assert.Equal(Strings.Format("str_help_failed", "no browser"), error);
        Assert.Empty(dialog.InfoMessages);
    }

    [Fact]
    public async Task Help_link_passes_the_ui_culture_through_to_the_service()
    {
        var dialog = new RecordingDialogService();
        var vm = new AboutDialogViewModel(dialog);

        await vm.OpenHelpCommand.ExecuteAsync(null);

        var language = Assert.Single(dialog.HelpGuideLanguages);
        Assert.Equal(
            System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
            language);
    }
}
