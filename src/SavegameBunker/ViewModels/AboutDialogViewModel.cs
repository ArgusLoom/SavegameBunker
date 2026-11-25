using System.Globalization;
using System.Reflection;
using CommunityToolkit.Mvvm.Input;
using SavegameBunker.Services;
using CommunityToolkitObservableObject = CommunityToolkit.Mvvm.ComponentModel.ObservableObject;

namespace SavegameBunker.ViewModels;

/// <summary>
/// Backs the About window: version and build date read from the running assembly, plus a link to
/// the user guide reusing <see cref="IDialogService.OpenHelpGuide"/> — the same offline-then-online
/// resolution the toolbar's old Help button used.
/// </summary>
public sealed partial class AboutDialogViewModel : CommunityToolkitObservableObject
{
    private readonly IDialogService _dialog;

    /// <param name="dialog">
    /// The About window's own dialog service, so a help-link error renders inside About rather
    /// than behind it in the main window.
    /// </param>
    public AboutDialogViewModel(IDialogService dialog)
    {
        _dialog = dialog;

        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = informational?.Split('+')[0];
        var versionText = string.IsNullOrWhiteSpace(version)
            ? null
            : Strings.Format("str_about_version", version);

        var buildDate = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildDate")?.Value;
        var buildDateText = string.IsNullOrWhiteSpace(buildDate)
            ? null
            : Strings.Format("str_about_buildDate", buildDate);

        MetaLine = string.Join("  ·  ",
            new[] { versionText, buildDateText, Strings.Get("str_about_copyright") }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    /// <summary>One compact line — version, build date and copyright — instead of three stacked
    /// labels, so the window stays short and the Help link never gets pushed off it again.</summary>
    public string MetaLine { get; }

    /// <summary>
    /// Opens the user guide off the UI thread through <see cref="IDialogService.OpenHelpGuide"/> — resolving
    /// the offline path touches the filesystem. This window has no status line, so the outcome goes
    /// straight to the snackbar/error dialog instead of a StatusMessage.
    /// </summary>
    [RelayCommand]
    private async Task OpenHelpAsync()
    {
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var result = await Task.Run(() => _dialog.OpenHelpGuide(language));

        if (result.Success)
            _dialog.Info(result.Message);
        else
            _dialog.Error(result.Message);
    }
}
