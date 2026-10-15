using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WindowsSecretManager.Core;

namespace WindowsSecretManager.App;

public partial class ArchiveWindow : Window
{
    private readonly SecretService _service;
    private readonly UiLanguage _language;
    private readonly Func<Task<bool>> _ensureVerified;
    private bool _selectedArchiveProtected;

    public ArchiveWindow(SecretService service, UiLanguage language, Func<Task<bool>> ensureVerified)
    {
        _service = service;
        _language = language;
        _ensureVerified = ensureVerified;
        InitializeComponent();
        DarkTitleBar.Enable(this);
        ApplyLanguage();
        RefreshExportCount();
        ProtectExportCheck.IsChecked = true;
        UpdateExportPasswordState();
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (!await _ensureVerified()) return;
        var protectedArchive = ProtectExportCheck.IsChecked == true;
        var password = ExportPasswordBox.Password;
        var confirmation = ConfirmPasswordBox.Password;
        if (protectedArchive)
        {
            if (password.Length < VaultArchive.MinimumPasswordLength)
            {
                ShowMessage("Passwort ist zu kurz", "Password is too short",
                    $"Verwende ein Passwort mit mindestens {VaultArchive.MinimumPasswordLength} Zeichen.",
                    $"Use a password with at least {VaultArchive.MinimumPasswordLength} characters.");
                return;
            }
            if (!string.Equals(password, confirmation, StringComparison.Ordinal))
            {
                ShowMessage("Passwörter stimmen nicht überein", "Passwords do not match",
                    "Gib in beiden Feldern dasselbe Passwort ein.", "Enter the same password in both fields.");
                return;
            }
        }
        else if (!StyledDialog.Confirm(this,
                     T.Get(_language, "Archiv ohne Passwort", "Archive without a password"),
                     T.Get(_language,
                         "Die Datei wird verschlüsselt und an dieses Windows-Konto auf diesem Gerät gebunden (DPAPI) — sie kann nicht auf einem anderen Konto, einem anderen PC oder nach einer Windows-Neuinstallation geöffnet werden. Um Secrets auf ein anderes System zu übertragen, verwende stattdessen ein passwortgeschütztes Archiv. Fortfahren?",
                         "The file will be encrypted and bound to this Windows account on this device (DPAPI) — it cannot be opened on another account, another PC, or after a Windows reinstall. To move secrets to another system, use a password-protected archive instead. Continue?"),
                     _language)) return;

        var picker = new SaveFileDialog
        {
            Title = T.Get(_language, "Codex-Vault-Archiv speichern", "Save Codex Vault archive"),
            Filter = T.Get(_language, "Codex-Vault-Archiv (*.cvault)|*.cvault", "Codex Vault archive (*.cvault)|*.cvault"),
            FileName = $"CodexVault-{DateTime.Now:yyyy-MM-dd}.cvault",
            AddExtension = true,
            DefaultExt = ".cvault"
        };
        if (picker.ShowDialog(this) != true) return;

        var items = new List<VaultArchiveItem>();
        byte[]? archiveBytes = null;
        try
        {
            foreach (var name in _service.List())
            {
                using var secret = _service.Read(name);
                var value = SecureStringText.Read(secret);
                items.Add(new VaultArchiveItem(name, value));
            }
            archiveBytes = VaultArchive.Create(items, protectedArchive ? password : null);
            File.WriteAllBytes(picker.FileName, archiveBytes);
            StatusText.Text = T.Get(_language,
                $"{items.Count} Einträge exportiert.",
                $"Exported {items.Count} entries.");
            StyledDialog.Show(this, T.Get(_language, "Export abgeschlossen", "Export complete"),
                T.Get(_language,
                    $"{items.Count} Einträge gespeichert in:\n{picker.FileName}",
                    $"Saved {items.Count} entries to:\n{picker.FileName}"), _language);
        }
        catch (Exception ex)
        {
            ShowOperationError(ex, "Das Archiv konnte nicht exportiert werden.", "Could not export the archive.");
        }
        finally
        {
            if (archiveBytes is not null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(archiveBytes);
            for (var index = 0; index < items.Count; index++) items[index] = items[index] with { Value = string.Empty };
            ExportPasswordBox.Clear();
            ConfirmPasswordBox.Clear();
            password = confirmation = string.Empty;
        }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Title = T.Get(_language, "Codex-Vault-Archiv wählen", "Select a Codex Vault archive"),
            Filter = T.Get(_language, "Codex-Vault-Archiv (*.cvault)|*.cvault|Alle Dateien (*.*)|*.*", "Codex Vault archive (*.cvault)|*.cvault|All files (*.*)|*.*")
        };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            var bytes = ReadArchiveFile(picker.FileName);
            VaultArchiveInfo archiveInfo;
            try { archiveInfo = VaultArchive.Inspect(bytes); }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
            _selectedArchiveProtected = archiveInfo.PasswordProtected;
            ImportPathBox.Text = picker.FileName;
            ImportPasswordBox.IsEnabled = _selectedArchiveProtected;
            if (!_selectedArchiveProtected) ImportPasswordBox.Clear();
            var countText = archiveInfo.ItemCount is { } count
                ? T.Get(_language, $"Zu importierende Einträge: {count}.", $"Entries to import: {count}.")
                : T.Get(_language, "Nach Eingabe des Passworts zeigt die App die Anzahl der Einträge vor dem Import an.", "After you enter the password, the entry count will be shown before import.");
            var statusText = _selectedArchiveProtected
                ? T.Get(_language, "Dieses Archiv ist passwortgeschützt.", "This archive is password-protected.")
                : archiveInfo.DeviceProtected
                    ? T.Get(_language, "Dieses Archiv ist an ein Windows-Konto gebunden (DPAPI) — es ist kein Passwort nötig, aber es muss auf demselben Konto geöffnet werden.",
                        "This archive is bound to a Windows account (DPAPI) — no password needed, but it must be opened on that same account.")
                    : T.Get(_language, "Dieses Archiv ist nicht verschlüsselt (altes Format).", "This archive is not encrypted (legacy format).");
            ArchiveInfoText.Text = $"{statusText}\n{countText}";
        }
        catch (Exception ex)
        {
            ImportPathBox.Clear();
            ArchiveInfoText.Text = string.Empty;
            ShowOperationError(ex, "Das Archiv konnte nicht gelesen werden.", "Could not read the archive.");
        }
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var path = ImportPathBox.Text;
        if (string.IsNullOrWhiteSpace(path))
        {
            ShowMessage("Keine Datei ausgewählt", "No file selected",
                "Wähle zuerst ein Archiv zum Importieren aus.", "Select an archive to import first.");
            return;
        }
        // Import can add or replace credentials, so authorize it before the
        // archive is opened and before conflict decisions are collected.
        if (!await _ensureVerified()) return;
        var password = ImportPasswordBox.Password;
        byte[]? bytes = null;
        VaultArchiveContents? archive = null;
        try
        {
            bytes = ReadArchiveFile(path);
            archive = VaultArchive.Open(bytes, _selectedArchiveProtected ? password : null);
            var existing = new HashSet<string>(_service.List(), StringComparer.OrdinalIgnoreCase);
            var conflictCount = archive.Items.Count(item => existing.Contains(item.Name));
            ArchiveInfoText.Text = T.Get(_language,
                $"Archiv geöffnet. Zu importierende Einträge: {archive.Items.Count}.\nVorhandene Namen: {conflictCount}.",
                $"Archive opened. Entries to import: {archive.Items.Count}.\nExisting names: {conflictCount}.");
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            if (!StyledDialog.Confirm(this,
                    T.Get(_language, "Archiv bereit zum Import", "Archive ready to import"),
                    T.Get(_language,
                        $"Das Archiv enthält {archive.Items.Count} Einträge. Namenskonflikte: {conflictCount}.\n\nMit der Konfliktlösung und dem Import fortfahren?",
                        $"The archive contains {archive.Items.Count} entries. Name conflicts: {conflictCount}.\n\nContinue to conflict resolution and import?"),
                    _language)) return;
            var decisions = ResolveConflicts(archive.Items, existing);
            if (decisions is null) return;

            var imported = 0;
            var replaced = 0;
            var skipped = 0;
            foreach (var item in archive.Items)
            {
                var conflict = existing.Contains(item.Name);
                if (conflict && !decisions[item.Name]) { skipped++; continue; }
                using var value = SecureStringText.Create(item.Value);
                _service.Save(item.Name, value, conflict);
                if (conflict) replaced++; else imported++;
            }
            StatusText.Text = T.Get(_language,
                $"Hinzugefügt: {imported}, ersetzt: {replaced}, übersprungen: {skipped}.",
                $"Added: {imported}, replaced: {replaced}, skipped: {skipped}.");
            RefreshExportCount();
            StyledDialog.Show(this, T.Get(_language, "Import abgeschlossen", "Import complete"), StatusText.Text, _language);
        }
        catch (UnauthorizedAccessException)
        {
            ShowMessage("Archiv konnte nicht geöffnet werden", "Could not open archive",
                "Das Passwort ist falsch, das Archiv wurde auf einem anderen Windows-Konto oder Gerät erstellt, oder die Datei ist beschädigt.",
                "The password is incorrect, the archive was created on a different Windows account or device, or the file is damaged.", true);
        }
        catch (Exception ex)
        {
            ShowOperationError(ex, "Das Archiv konnte nicht importiert werden.", "Could not import the archive.");
        }
        finally
        {
            if (bytes is not null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
            ImportPasswordBox.Clear();
            password = string.Empty;
        }
    }

    private Dictionary<string, bool>? ResolveConflicts(IReadOnlyList<VaultArchiveItem> items, HashSet<string> existing)
    {
        var result = items.ToDictionary(item => item.Name, _ => true, StringComparer.OrdinalIgnoreCase);
        var conflicts = items.Where(item => existing.Contains(item.Name)).ToArray();
        var strategy = (ConflictBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "ask";
        if (strategy == "replace") return result;
        if (strategy == "skip")
        {
            foreach (var item in conflicts) result[item.Name] = false;
            return result;
        }
        foreach (var item in conflicts)
        {
            var decision = new ArchiveConflictDialog(item.Name, _language) { Owner = this }.ShowChoice();
            if (decision == ArchiveConflictChoice.Cancel) return null;
            result[item.Name] = decision == ArchiveConflictChoice.Replace;
        }
        return result;
    }

    private void ProtectExportCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (IsInitialized) UpdateExportPasswordState();
    }

    private void UpdateExportPasswordState()
    {
        var enabled = ProtectExportCheck.IsChecked == true;
        ExportPasswordBox.IsEnabled = ConfirmPasswordBox.IsEnabled = enabled;
        ExportWarningBorder.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
        if (!enabled) { ExportPasswordBox.Clear(); ConfirmPasswordBox.Clear(); }
    }

    private void ApplyLanguage()
    {
        Title = T.Get(_language, "Import und Export — Codex Vault", "Import and export — Codex Vault");
        HeadingText.Text = T.Get(_language, "Import und Export", "Import and export");
        DescriptionText.Text = T.Get(_language, "Alle Codex-Vault-Einträge verschieben oder sichern.", "Move or back up every Codex Vault entry.");
        ExportHeading.Text = T.Get(_language, "Export", "Export");
        ExportDescription.Text = T.Get(_language, "Alle Secrets und Wiederherstellungscodes in einer Datei speichern.", "Save all secrets and recovery codes in one file.");
        ProtectExportCheck.Content = T.Get(_language, "Archiv mit Passwort schützen", "Protect archive with a password");
        ExportPasswordLabel.Text = T.Get(_language, "Passwort", "Password");
        ConfirmPasswordLabel.Text = T.Get(_language, "Passwort wiederholen", "Confirm password");
        ExportWarningText.Text = T.Get(_language,
            "Ohne Passwort wird die Datei verschlüsselt, aber nur an dieses Windows-Konto auf diesem Gerät gebunden.",
            "Without a password, the file is encrypted but bound to this Windows account on this device only.");
        ExportButton.Content = T.Get(_language, "Alle Einträge exportieren…", "Export all entries…");
        ImportHeading.Text = T.Get(_language, "Import", "Import");
        ImportDescription.Text = T.Get(_language, "Einträge aus einem .cvault-Archiv wiederherstellen.", "Restore entries from a .cvault archive.");
        BrowseButton.Content = T.Get(_language, "Durchsuchen…", "Browse…");
        ImportPasswordLabel.Text = T.Get(_language, "Archiv-Passwort", "Archive password");
        ConflictLabel.Text = T.Get(_language, "Wenn ein Name schon existiert", "When a name already exists");
        ConflictBox.Items.Clear();
        ConflictBox.Items.Add(new ComboBoxItem { Content = T.Get(_language, "Für jeden Eintrag nachfragen", "Ask for each entry"), Tag = "ask" });
        ConflictBox.Items.Add(new ComboBoxItem { Content = T.Get(_language, "Alle vorhandenen Einträge ersetzen", "Replace all existing entries"), Tag = "replace" });
        ConflictBox.Items.Add(new ComboBoxItem { Content = T.Get(_language, "Alle vorhandenen Einträge überspringen", "Skip all existing entries"), Tag = "skip" });
        ConflictBox.SelectedIndex = 0;
        ImportButton.Content = T.Get(_language, "Archiv importieren", "Import archive");
        CloseButton.Content = T.Get(_language, "Schließen", "Close");
        ImportPasswordBox.IsEnabled = false;
    }

    private void RefreshExportCount()
    {
        try
        {
            var count = _service.List().Count;
            ExportCountText.Text = T.Get(_language,
                $"Zu speichernde Einträge: {count}",
                $"Entries to save: {count}");
        }
        catch
        {
            ExportCountText.Text = T.Get(_language,
                "Die Anzahl der Einträge konnte nicht gelesen werden.",
                "Could not read the entry count.");
        }
    }

    private void ShowOperationError(Exception ex, string german, string english) =>
        StyledDialog.Show(this, T.Get(_language, "Fehler", "Error"), $"{T.Get(_language, german, english)}\n\n{ex.Message}", _language, true);

    private void ShowMessage(string germanTitle, string englishTitle, string german, string english, bool error = false) =>
        StyledDialog.Show(this, T.Get(_language, germanTitle, englishTitle), T.Get(_language, german, english), _language, error);

    private static byte[] ReadArchiveFile(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("The selected archive does not exist.", path);
        if (info.Length == 0 || info.Length > VaultArchive.MaximumSizeBytes)
            throw new InvalidDataException("The archive is empty or larger than 16 MB.");
        return File.ReadAllBytes(path);
    }
}
