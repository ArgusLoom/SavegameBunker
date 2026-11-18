using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SavegameBunker.Services;
using SavegameBunker.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace SavegameBunker;

public sealed partial class App : Application
{
    /// <summary>
    /// The fixed brand accent colour, handed to <see cref="ApplicationAccentColorManager"/> by
    /// <see cref="ApplyTheme"/> — at startup (outside High Contrast) and again on every
    /// <see cref="ToggleTheme"/>, because
    /// re-applying the theme resets WPF-UI's accent resources.
    /// </summary>
    /// <remarks>
    /// Read from <c>App.xaml</c> rather than restated here: the same colour drives the accent-tinted
    /// checkmark in <c>Styles.xaml</c>, and two hand-kept copies of one brand colour drift.
    /// </remarks>
    private static Color AccentColor =>
        Current.Resources["SavegameBunkerAccentColor"] is Color color
            ? color
            : Color.FromRgb(0x4D, 0xD3, 0x00);

    /// <summary>
    /// The dialog service, held from the moment it is constructed so the unhandled-exception
    /// handlers can report on the blocking surface failures are reported on. Null only during the
    /// window between startup and the window existing — the handlers fall back to a plain message
    /// box there, because the one rule is that the user always sees something.
    /// </summary>
    private IDialogService? _dialogService;

    /// <summary>
    /// The theme currently applied by <see cref="ApplyTheme"/> — read by <see cref="ToggleTheme"/>
    /// so it knows which way to flip. Not persisted (no
    /// <c>settings.json</c>, see CLAUDE.md): every launch re-derives the starting theme from the
    /// Windows setting via <see cref="ApplicationThemeManager.GetSystemTheme"/>.
    /// </summary>
    public static ApplicationTheme CurrentTheme { get; private set; } = ApplicationTheme.Dark;

    /// <summary>
    /// True when startup applied the fixed dark High Contrast dictionary. The toolbar's
    /// theme toggle hides itself in that case — Light/Dark is not a choice while the OS
    /// accessibility setting is in force.
    /// </summary>
    public static bool IsHighContrastActive { get; private set; }

    /// <summary>
    /// Applies <paramref name="theme"/> (Light or Dark — High Contrast is startup-only, handled
    /// separately in <see cref="OnStartup"/>) and reapplies the accent-brush overrides on top, since
    /// WPF-UI's own <see cref="ApplicationThemeManager.Apply(ApplicationTheme, WindowBackdropType, bool)"/> resets them to its derived ramp.
    /// Safe to call again after startup — <see cref="ToggleTheme"/> is the only other caller.
    /// </summary>
    private static void ApplyTheme(ApplicationTheme theme)
    {
        // ApplicationThemeManager.Apply tears Mica down and puts it back (RemoveBackdrop →
        // ApplyBackdrop). Without freezing paints, that gap is a visible flash of the window
        // chrome, then a second flash when the accent brushes are overwritten. WM_SETREDRAW
        // holds the pixels until every resource write has landed, then one RedrawWindow.
        using (WindowRedraw.Suspend(Current.MainWindow))
        {
            ApplicationThemeManager.Apply(theme, WindowBackdropType.Mica, updateAccent: false);
            ApplicationAccentColorManager.Apply(AccentColor, theme);

            // WPF-UI derives a whole ramp of accent shades from the input colour — separate keys for
            // the ContentDialog primary button, list selection, hover/pressed and text-on-accent — and
            // several of those derived shades read paler than the brand green itself. Repointed at one
            // accent brush so every accent surface in the app (buttons, hover, selected rows, accent
            // text, the empty-state link) shows the same colour, not a ramp.
            //
            // Light uses SavegameBunkerAccentLightBrush (a darker green, App.xaml) instead of the full-
            // brightness SavegameBunkerAccentBrush: the bright accent is too light/low-contrast against a
            // white ground — this is the one thing that actually differs between the two themes here.
            var accentBrush = theme == ApplicationTheme.Light
                ? Current.Resources["SavegameBunkerAccentLightBrush"]
                : Current.Resources["SavegameBunkerAccentBrush"];
            foreach (var key in new[]
                     {
                     "AccentButtonBackground",
                     "SystemAccentColorBrush",
                     "SystemAccentColorPrimaryBrush",
                     "SystemAccentColorSecondaryBrush",
                     "SystemAccentColorTertiaryBrush",
                     "AccentFillColorDefaultBrush",
                     "AccentFillColorPrimaryBrush",
                     "AccentFillColorSecondaryBrush",
                     "AccentFillColorTertiaryBrush",
                     "ListBoxItemSelectedBackgroundThemeBrush",
                     "ProgressBarForeground",
                     "SavegameBunkerLinkBrush",
                 })
            {
                Current.Resources[key] = accentBrush;
            }

            // The bright accent green fails contrast with a light selected-item text colour in either
            // theme — fixed dark text stays legible on it regardless of which theme is active, so this
            // does not need to flip with Light vs Dark the way TextFillColorInverseBrush would.
            Current.Resources["ListBoxItemSelectedForegroundThemeBrush"] =
                Current.Resources["SavegameBunkerOnAccentTextBrush"];

            CurrentTheme = theme;
        }
    }

    /// <summary>
    /// Called by the toolbar's theme toggle (session-only, no persistence — see
    /// <see cref="CurrentTheme"/>). Flips between Light and Dark; never touches High Contrast.
    /// </summary>
    public static void ToggleTheme()
    {
        ApplyTheme(CurrentTheme == ApplicationTheme.Dark ? ApplicationTheme.Light : ApplicationTheme.Dark);
    }

    /// <summary>
    /// Composition root. Resolves which catalog to load, loads it, and shows the window.
    /// </summary>
    /// <remarks>
    /// Startup has no fatal path: an absent catalog is seeded and a malformed one is
    /// reported into the running window, because a application that exits with a dialog is
    /// useless to a user who then has nowhere to fix the problem. The try/catch below is a last
    /// resort for genuinely unexpected failures, not the normal error route.
    /// </remarks>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // Load the authored strings here, on the UI thread, before the status sweep can reach them
        // from four worker threads at once. See Strings' remarks for what went wrong without this.
        Strings.Preload();

        try
        {
            // Windows High Contrast is an OS-declared accessibility requirement, not a
            // SavegameBunker theme choice — it is followed instead of the fixed dark palette. Detection
            // is at startup only; a mode change while running is not handled in v1.
            //
            // SCOPE: only DARK contrast schemes are supported.
            // ApplicationTheme.HighContrast applies a single, dark high-contrast dictionary, so a
            // user on a LIGHT contrast scheme still gets a dark window. Copying SystemColors over
            // individual theme keys was tried and reverted — it left some surfaces light and the
            // window ground dark, which reads worse than a consistently dark window. Fixing this
            // properly means replacing the shell's high-contrast dictionary, not tinting it.
            // Recorded in README.md's Known limitations.
            if (ApplicationThemeManager.IsSystemHighContrast())
            {
                ApplicationThemeManager.Apply(ApplicationTheme.HighContrast, WindowBackdropType.None, updateAccent: false);
                UseSystemColoursForAccent();
                IsHighContrastActive = true;
            }
            else
            {
                // No in-app theme setting is persisted (no settings.json, CLAUDE.md), so the
                // starting theme is re-derived from the Windows setting on every launch. The
                // toolbar's theme toggle can still flip it for the running session via ApplyTheme.
                var startingTheme = ApplicationThemeManager.GetSystemTheme() == SystemTheme.Light
                    ? ApplicationTheme.Light
                    : ApplicationTheme.Dark;
                ApplyTheme(startingTheme);
            }

#if DEBUG
            // The debug-only self-test switches are not catalog paths. Filtered here rather than in
            // ResolvePath, and only in the build where those switches exist at all.
            var args = e.Args.Where(a => !a.StartsWith("--selftest-", StringComparison.Ordinal)).ToArray();
#else
            var args = e.Args;
#endif
            var path = GameCatalog.ResolvePath(args);
            var load = await GameCatalog.LoadAsync(path);

            var window = new MainWindow();

            // No DI container — constructed directly as plain locals, matching every other
            // SavegameBunker service. Wired immediately after construction: MainWindow's named
            // RootDialogHost/RootSnackbarPresenter elements already exist once InitializeComponent
            // returns, which happens inside `new MainWindow()` above, so there is no need to wait
            // for the window's Loaded event the way a DI-based startup would.
            var contentDialogService = new ContentDialogService();
            contentDialogService.SetDialogHost(window.RootDialogHost);

            var snackbarService = new SnackbarService();
            snackbarService.SetSnackbarPresenter(window.RootSnackbarPresenter);

            var dialogService = new DialogService(window, contentDialogService, snackbarService);
            _dialogService = dialogService;

            var vm = new MainViewModel(load, dialogService);
            window.DataContext = vm;
            window.Show();

            await vm.ReportLoad();

            // One prompt, at most once per session, about games whose saves are
            // not covered by a backup. Armed here and decided by the first full status sweep that
            // settles — see MainViewModel.PromptUnsecuredGamesOnce. Nothing is persisted.
            vm.RequestStartupUnsecuredPrompt();

#if DEBUG
            RunCrashSelfTest(e.Args);
#endif
        }
        catch (Exception ex)
        {
            // Qualified: Wpf.Ui.Controls also declares a MessageBox/MessageBoxButton, and this
            // last-resort path (startup before any window exists) intentionally keeps the native
            // dialog rather than picking up WPF-UI's.
            System.Windows.MessageBox.Show(
                $"Failed to start SavegameBunker:\n\n{ex}",
                "SavegameBunker", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>
    /// Repoints the accent resources at the OS's own high-contrast colours. The fixed green is SavegameBunker's brand choice, and a brand choice is exactly what a
    /// user running High Contrast has asked the system to stop honouring — their palette wins.
    /// </summary>
    /// <remarks>
    /// Scoped to DARK contrast schemes (see <c>OnStartup</c>). On a light scheme the accent is
    /// substituted correctly but the window ground stays dark, so the result is a dark blue icon on
    /// a dark ground — which is why that combination is documented as unsupported rather than
    /// quietly shipped.
    /// <para>
    /// Done here rather than by a theme-aware brush because High Contrast is applied once at
    /// startup and never toggled: the Light/Dark accent swap in <see cref="ApplyTheme"/> does not
    /// run under it, so this one substitution is all the accent needs.
    /// </para>
    /// <para>
    /// The hover and press grounds go <em>transparent</em> rather than being repainted in system
    /// colours. SavegameBunker's tinted hover ground is drawn by its own style, but the control's
    /// chrome underneath is WPF-UI's, and the two cannot be assumed to agree on which pair of
    /// colours is in force: pairing a system foreground with a ground this style does not reliably
    /// own produced dark-on-dark text, invisible until someone looked. Standing aside lets the
    /// high-contrast theme draw the whole state itself, which is the one arrangement guaranteed to
    /// be internally consistent.
    /// </para>
    /// </remarks>
    private static void UseSystemColoursForAccent()
    {
        // HotTrack rather than Highlight: it is the palette's "this element is special" colour and
        // stays legible against the window ground, where Highlight is meant to sit behind text.
        Current.Resources["SavegameBunkerAccentBrush"] = SystemColors.HotTrackBrush;
        Current.Resources["SavegameBunkerAccentHoverBrush"] = Brushes.Transparent;
        Current.Resources["SavegameBunkerAccentPressedBrush"] = Brushes.Transparent;
        Current.Resources["SavegameBunkerAccentContrastBrush"] = SystemColors.WindowTextBrush;
    }

    /// <summary>
    /// Anything that reaches the dispatcher unhandled is reported and swallowed, so an
    /// unexpected failure in one action can never take the process down and lose the user's place.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Handled FIRST, before anything that could itself throw. Composing the report reads the
        // string dictionary; if that ever failed, doing it first would skip this line and take the
        // process down — a handler that can throw is worse than no handler.
        e.Handled = true;
        ReportSafely(e.Exception, "str_crash_source_action");
    }

    /// <summary>
    /// A faulted task nobody awaited is observed and reported rather than left to the
    /// finalizer. Marking it observed is what keeps the default escalation policy from ending the
    /// process at an unpredictable, collection-timed moment.
    /// </summary>
    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // Observed FIRST, for the same reason Handled is set first above: this is the call that
        // keeps the escalation policy from ending the process, so nothing that could throw may
        // run before it.
        e.SetObserved();

        var exception = e.Exception;

        // The event arrives on a finalizer thread; every reporting route touches the UI.
        Dispatcher.BeginInvoke(() => ReportSafely(exception, "str_crash_source_background"));
    }

    /// <summary>
    /// Composes and shows the report, absorbing any failure in the reporting machinery itself.
    /// Composing reads the authored string dictionary, and showing touches a dialog service — both
    /// can fail. Neither is allowed to escalate: this method runs only when something has already
    /// gone wrong, and a crash while reporting a crash costs the user their session for nothing.
    /// The fallback repeats what little is known without going through the dictionary at all.
    /// </summary>
    private void ReportSafely(Exception exception, string sourceKey)
    {
        string description;
        try
        {
            description = CrashReport.Describe(exception, Strings.Get(sourceKey));
        }
        catch
        {
            description = $"Something went wrong.\n\n{exception.Message}\n\nSavegameBunker is still running.";
        }

        try
        {
            Report(description);
        }
        catch
        {
            // Even the message box failed. There is no third surface, and throwing from here would
            // defeat the whole guarantee — the application stays up, silently, which is still
            // better than gone.
        }
    }

    /// <summary>
    /// One rule: the user always sees something. Through the dialog service once it exists, so the
    /// report lands on the same blocking surface as every other failure; through a plain message
    /// box before then.
    /// </summary>
    private void Report(string description)
    {
        if (_dialogService is { } dialogService)
        {
            dialogService.Error(description);
            return;
        }

        System.Windows.MessageBox.Show(
            description, "SavegameBunker", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
    }

    /// <summary>
    /// Freezes WM paints on a window for the duration of a theme swap, then invalidates once.
    /// No-ops when the window has no HWND yet (startup, before Show).
    /// </summary>
    private sealed class WindowRedraw : IDisposable
    {
        private const int WmSetRedraw = 0x000B;
        private const uint RdwInvalidate = 0x0001;
        private const uint RdwErase = 0x0004;
        private const uint RdwAllChildren = 0x0080;
        private const uint RdwFrame = 0x0400;
        private const uint RdwUpdateNow = 0x0100;

        private readonly IntPtr _hwnd;

        private WindowRedraw(IntPtr hwnd) => _hwnd = hwnd;

        public static IDisposable Suspend(Window? window)
        {
            var hwnd = window is null ? IntPtr.Zero : new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return Null.Instance;

            SendMessage(hwnd, WmSetRedraw, IntPtr.Zero, IntPtr.Zero);
            return new WindowRedraw(hwnd);
        }

        public void Dispose()
        {
            SendMessage(_hwnd, WmSetRedraw, new IntPtr(1), IntPtr.Zero);
            RedrawWindow(
                _hwnd,
                IntPtr.Zero,
                IntPtr.Zero,
                RdwInvalidate | RdwErase | RdwFrame | RdwAllChildren | RdwUpdateNow);
        }

        private sealed class Null : IDisposable
        {
            internal static readonly Null Instance = new();
            public void Dispose() { }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprc, IntPtr hrgn, uint flags);
    }

#if DEBUG
    /// <summary>
    /// Makes both unhandled-exception routes demonstrable on demand, because a handler nobody has watched fire
    /// is not evidence. Debug-only by construction — the switches cannot exist in a Release build.
    /// </summary>
    private void RunCrashSelfTest(string[] args)
    {
        if (args.Contains("--selftest-crash-dispatcher"))
        {
            // Queued rather than thrown inline: thrown here it would land in OnStartup's own
            // last-resort catch, which is the pre-window contract, not the one under test.
            Dispatcher.BeginInvoke(() => throw new InvalidOperationException(
                "Self-test: a deliberate failure on the dispatcher."));
        }

        if (args.Contains("--selftest-crash-unobserved"))
        {
            FaultAndForget();

            // The unobserved event fires only when a faulted task is finalized, so without forcing
            // a collection there is nothing to observe and the guarantee stays untestable.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    /// <summary>
    /// Faults a task in its own scope and drops the reference, so it is unreachable the moment
    /// this returns. Inlining this into the caller would keep the local alive to the end of the
    /// method and the collection below would find nothing to finalize.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FaultAndForget() =>
        _ = Task.Run(() => throw new InvalidOperationException(
            "Self-test: a deliberate failure on a task nobody awaited."));
#endif
}
