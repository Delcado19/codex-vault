using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using WindowsSecretManager.Core;

namespace WindowsSecretManager.App;

public partial class MainWindow : Window
{
    // Idle timeout before protected operations re-lock; matches the 5-minute
    // default of the idle/session-lock pattern this is modeled on.
    private const int IdleLockTimeoutSeconds = 300;

    private readonly SecretService _service = new(new WindowsCredentialStore());
    private readonly DispatcherTimer _clipboardTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    private readonly DispatcherTimer _idleLockTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private UiLanguage _language = UiLanguage.German;
    private IReadOnlyList<string> _allNames = Array.Empty<string>();
    private bool _showingRecoveryCodes;
    private bool _sessionVerified;
    private long _lastActivityTicks = Environment.TickCount64;
    private uint _copiedClipboardSequence;
    private string? SelectedName => SecretList.SelectedItem as string;

    public MainWindow()
    {
        InitializeComponent();
        DarkTitleBar.Enable(this);
        ApplyLanguage();
        _clipboardTimer.Tick += (_, _) => ClearClipboard();
        _statusTimer.Tick += (_, _) => { _statusTimer.Stop(); ApplyStandardStatus(); };
        _idleLockTimer.Tick += (_, _) => CheckIdleLock();
        _idleLockTimer.Start();
        PreviewMouseMove += (_, _) => RecordActivity();
        PreviewKeyDown += (_, _) => RecordActivity();
        Activated += (_, _) => RecordActivity();
        SystemEvents.SessionSwitch += OnSessionSwitch;
        Loaded += MainWindow_Loaded;
        Closed += (_, _) =>
        {
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            _idleLockTimer.Stop();
            _statusTimer.Stop();
            ClearClipboard();
        };
    }

    private void RecordActivity() => _lastActivityTicks = Environment.TickCount64;

    private void CheckIdleLock()
    {
        if (_sessionVerified && Environment.TickCount64 - _lastActivityTicks >= IdleLockTimeoutSeconds * 1000L)
            LockSession();
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock) LockSession();
    }

    private void LockSession()
    {
        if (!_sessionVerified) return;
        _sessionVerified = false;
        RecordActivity();
        ShowTemporaryStatus(
            "Geschützte Aktionen wurden erneut gesperrt (Inaktivität oder Bildschirmsperre).",
            "Protected operations were locked again (idle or screen lock).");
    }

    private void RefreshList(string? select = null)
    {
        try
        {
            _allNames = _service.List()
                .Where(name => SecretNames.IsRecoveryCodesName(name) == _showingRecoveryCodes)
                .ToArray();
            ApplyFilter(select);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e) => RefreshList();

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (_showingRecoveryCodes) { AddRecoveryCodes(); return; }
        var dialog = new SecretDialog(null, _language) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        using var secret = dialog.SecretValue;
        try
        {
            var name = _service.Save(dialog.SecretName, secret, false);
            RefreshList(name);
            ShowTemporaryStatus("Secret hinzugefügt.", "Secret added.");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void AddRecoveryCodes()
    {
        var dialog = new RecoveryCodesDialog(_language) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        using var secret = dialog.SecretValue;
        try
        {
            var name = _service.Save(dialog.SecretName, secret, false);
            RefreshList(name);
            ShowTemporaryStatus("Wiederherstellungscodes hinzugefügt.", "Recovery codes added.");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void RecoveryCodes_Click(object sender, RoutedEventArgs e)
    {
        _showingRecoveryCodes = !_showingRecoveryCodes;
        FilterBox.Clear();
        ApplyLanguage();
        RefreshList();
    }

    private async void Rotate_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedName is not { } name) return;
        if (!await EnsureSessionVerifiedAsync()) return;
        if (SecretNames.IsRecoveryCodesName(name))
        {
            using var currentSecret = ReadOrReport(name); if (currentSecret is null) return;
            var currentValue = SecureStringText.Read(currentSecret);
            try
            {
                var codesDialog = new RecoveryCodesDialog(_language, name, currentValue) { Owner = this };
                if (codesDialog.ShowDialog() != true) return;
                using var codes = codesDialog.SecretValue;
                var updatedName = _service.Update(name, codesDialog.SecretName, codes);
                RefreshList(updatedName);
                StatusText.Text = T.Get(_language, "Codesatz aktualisiert.", "Recovery code set updated.");
            }
            catch (Exception ex) { ShowError(ex); }
            finally { currentValue = string.Empty; }
            return;
        }
        using var currentSecretValue = ReadOrReport(name); if (currentSecretValue is null) return;
        var currentValueText = SecureStringText.Read(currentSecretValue);
        try
        {
            var dialog = new SecretDialog(name, _language, currentValueText) { Owner = this };
            if (dialog.ShowDialog() != true) return;
            using var secret = dialog.SecretValue;
            var updatedName = _service.Update(name, dialog.SecretName, secret);
            RefreshList(updatedName);
            StatusText.Text = T.Get(_language, "Secret aktualisiert.", "Secret updated.");
        }
        catch (Exception ex) { ShowError(ex); }
        finally { currentValueText = string.Empty; }
    }

    private async void Reveal_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedName is not { } name) return;
        if (!await EnsureSessionVerifiedAsync()) return;
        using var secret = ReadOrReport(name); if (secret is null) return;
        var value = SecureStringText.Read(secret);
        try
        {
            if (SecretNames.IsRecoveryCodesName(name) || value.Contains('\r') || value.Contains('\n'))
                new RecoveryCodesViewer(name, value, _language) { Owner = this }.ShowDialog();
            else
                StyledDialog.Show(this, $"{T.Get(_language, "Wert", "Value")} — {name}", value, _language);
        }
        finally { value = string.Empty; }
    }

    private async void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedName is not { } name) return;
        if (!await EnsureSessionVerifiedAsync()) return;
        using var secret = ReadOrReport(name); if (secret is null) return;
        var value = SecureStringText.Read(secret);
        try { Clipboard.SetText(value); _copiedClipboardSequence = GetClipboardSequenceNumber(); _clipboardTimer.Stop(); _clipboardTimer.Start(); StatusText.Text = T.Get(_language, "Kopiert. Die Zwischenablage wird in 30 Sekunden geleert.", "Copied. The clipboard will be cleared in 30 seconds."); }
        finally { value = string.Empty; }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedName is not { } name) return;
        if (!StyledDialog.Confirm(this, T.Get(_language, "Löschen bestätigen", "Confirm deletion"), $"{T.Get(_language, "Diesen Eintrag wirklich löschen?", "Are you sure you want to delete this entry?")}\n\n{name}", _language)) return;
        try { _service.Delete(name); RefreshList(); StatusText.Text = T.Get(_language, "Eintrag gelöscht.", "Entry deleted."); }
        catch (Exception ex) { ShowError(ex); }
    }

    private SecureString? ReadOrReport(string name) { try { return _service.Read(name); } catch (Exception ex) { ShowError(ex); return null; } }
    private async Task<bool> EnsureSessionVerifiedAsync()
    {
        if (_sessionVerified) return true;
        _sessionVerified = await WindowsHelloGate.VerifyAsync(_language,
            T.Get(_language, "Zugriff auf geschützte Aktionen", "access protected operations"));
        if (_sessionVerified)
        {
            RecordActivity();
            ShowTemporaryStatus(
                "Geschützte Aktionen sind freigeschaltet, bis die App schließt, die Sitzung gesperrt wird oder du inaktiv bist.",
                "Protected operations are unlocked until the app closes, the session locks, or you go idle.");
        }
        return _sessionVerified;
    }
    private void ClearClipboard() { _clipboardTimer.Stop(); if (_copiedClipboardSequence == 0 || GetClipboardSequenceNumber() != _copiedClipboardSequence) return; try { Clipboard.Clear(); _copiedClipboardSequence = 0; StatusText.Text = T.Get(_language, "Zwischenablage geleert.", "Clipboard cleared."); } catch { StatusText.Text = T.Get(_language, "Zwischenablage konnte nicht geleert werden; kopiere einen anderen Wert.", "Could not clear the clipboard; copy another value."); } }
    private void ShowError(Exception ex) => StyledDialog.Show(this, T.Get(_language, "Fehler", "Error"), ex.Message, _language, error: true);
    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshList();
    private void Archive_Click(object sender, RoutedEventArgs e)
    {
        new ArchiveWindow(_service, _language, EnsureSessionVerifiedAsync) { Owner = this }.ShowDialog();
        RefreshList(SelectedName);
    }
    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) { if (IsInitialized) ApplyFilter(SelectedName); }
    private void ApplyFilter(string? select = null)
    {
        var query = FilterBox.Text.Trim();
        var visible = string.IsNullOrEmpty(query)
            ? _allNames
            : _allNames.Where(name => name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        SecretList.ItemsSource = visible;
        if (select is not null && visible.Contains(select)) SecretList.SelectedItem = select;
        _statusTimer.Stop();
        ApplyStandardStatus(visible.Count);
    }

    private void ShowTemporaryStatus(string german, string english)
    {
        _statusTimer.Stop();
        StatusText.Text = T.Get(_language, german, english);
        _statusTimer.Start();
    }

    private void ApplyStandardStatus()
    {
        var query = FilterBox.Text.Trim();
        var visibleCount = string.IsNullOrEmpty(query)
            ? _allNames.Count
            : _allNames.Count(name => name.Contains(query, StringComparison.OrdinalIgnoreCase));
        ApplyStandardStatus(visibleCount);
    }

    private void ApplyStandardStatus(int visibleCount)
    {
        var query = FilterBox.Text.Trim();
        StatusText.Text = string.IsNullOrEmpty(query)
            ? T.Get(_language, $"Anzahl Einträge: {_allNames.Count}", $"Entries: {_allNames.Count}")
            : T.Get(_language, $"Ergebnisse: {visibleCount} von {_allNames.Count}", $"Results: {visibleCount} of {_allNames.Count}");
    }
    private void SecretList_SelectionChanged(object sender, SelectionChangedEventArgs e) { var enabled = SelectedName is not null; RotateButton.IsEnabled = RevealButton.IsEnabled = CopyButton.IsEnabled = DeleteButton.IsEnabled = enabled; }

    private void GermanButton_Click(object sender, RoutedEventArgs e)
    {
        _language = UiLanguage.German;
        ApplyLanguage();
        RefreshList(SelectedName);
    }

    private void EnglishButton_Click(object sender, RoutedEventArgs e)
    {
        _language = UiLanguage.English;
        ApplyLanguage();
        RefreshList(SelectedName);
    }

    private void ApplyLanguage()
    {
        SubtitleText.Text = T.Get(_language, "Sicherer Tresor für geteilte Secrets", "Secure vault for shared secrets");
        ListHeadingText.Text = _showingRecoveryCodes ? T.Get(_language, "Sätze von Wiederherstellungscodes", "Recovery code sets") : T.Get(_language, "Gespeicherte Anmeldedaten", "Stored credentials");
        NamesOnlyText.Text = _showingRecoveryCodes ? T.Get(_language, "Codes bleiben verborgen, bis sie angezeigt werden", "Codes stay hidden until revealed") : T.Get(_language, "Es sind nur Namen sichtbar", "Only names are visible");
        AddButton.Content = _showingRecoveryCodes ? T.Get(_language, "＋  Wiederherstellungscodes hinzufügen", "＋  Add recovery codes") : T.Get(_language, "＋  Secret hinzufügen", "＋  Add secret");
        RecoveryCodesButton.Content = _showingRecoveryCodes ? T.Get(_language, "Secrets", "Secrets") : T.Get(_language, "Wiederherstellungscodes", "Recovery codes");
        RotateButton.Content = T.Get(_language, "↻  Ändern", "↻  Edit");
        RevealButton.Content = _showingRecoveryCodes ? T.Get(_language, "◉  Codes anzeigen", "◉  Reveal codes") : T.Get(_language, "◉  Anzeigen", "◉  Reveal");
        CopyButton.Content = T.Get(_language, "▣  Kopieren", "▣  Copy");
        CopyButton.Visibility = _showingRecoveryCodes ? Visibility.Collapsed : Visibility.Visible;
        DeleteButton.Content = T.Get(_language, "Löschen", "Delete");
        RefreshButton.Content = T.Get(_language, "Aktualisieren", "Refresh");
        ArchiveButton.Content = T.Get(_language, "Import / Export", "Import / Export");
        FilterLabel.Text = T.Get(_language, "Suchen", "Search");
        AboutText.Text = T.Get(_language, "Über", "About");
        ApplyStandardStatus();
        GermanButton.Background = _language == UiLanguage.German ? (System.Windows.Media.Brush)FindResource("Accent") : (System.Windows.Media.Brush)FindResource("Surface");
        EnglishButton.Background = _language == UiLanguage.English ? (System.Windows.Media.Brush)FindResource("Accent") : (System.Windows.Media.Brush)FindResource("Surface");
    }

    private void AboutLink_Click(object sender, RoutedEventArgs e) => new AboutWindow(_language) { Owner = this }.ShowDialog();

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
}
