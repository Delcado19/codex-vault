using System.Security;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using WindowsSecretManager.Core;

namespace WindowsSecretManager.App;

public partial class RecoveryCodesDialog : Window
{
    private readonly UiLanguage _language;
    public string SecretName => BuildName();
    public SecureString SecretValue
    {
        get
        {
            var value = CodesBox.Text.Trim();
            try { return ToSecureString(value); }
            finally { CodesBox.Clear(); }
        }
    }

    public RecoveryCodesDialog(UiLanguage language, string? existingName = null, string? existingValue = null)
    {
        _language = language;
        InitializeComponent();
        DarkTitleBar.Enable(this);
        ApplyLanguage();
        if (existingName is not null)
            NameBox.Text = existingName.StartsWith(SecretNames.RecoveryCodesPrefix, StringComparison.Ordinal)
                ? existingName[SecretNames.RecoveryCodesPrefix.Length..]
                : existingName;
        if (existingValue is not null) CodesBox.Text = existingValue;
        UpdatePreview();
        CodesBox.TextChanged += (_, _) => UpdateCount();
        Loaded += (_, _) =>
        {
            if (existingName is null) NameBox.Focus();
            else CodesBox.Focus();
        };
    }

    private string BuildName() => SecretNames.RecoveryCodesPrefix + NameBox.Text.Trim().TrimStart('/', '\\');

    private void NamePart_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (IsInitialized) UpdatePreview();
    }

    private void UpdatePreview() => FullNameText.Text = BuildName();

    private void UpdateCount()
    {
        var count = CodesBox.Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries).Length;
        CountText.Text = T.Get(_language, $"Nicht leere Zeilen: {count}", $"Non-empty lines: {count}");
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Title = T.Get(_language, "Datei mit Wiederherstellungscodes wählen", "Select a recovery codes file"),
            Filter = T.Get(_language, "Textdateien (*.txt)|*.txt|Alle Dateien (*.*)|*.*", "Text files (*.txt)|*.txt|All files (*.*)|*.*")
        };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            var info = new FileInfo(picker.FileName);
            if (info.Length > 8192) throw new InvalidOperationException(T.Get(_language, "Die Datei ist zu groß. Wähle eine Textdatei, die nur die Codes enthält.", "The file is too large. Select a text file containing only the codes."));
            CodesBox.Text = File.ReadAllText(picker.FileName).Trim();
            CodesBox.Focus();
        }
        catch (Exception ex)
        {
            StyledDialog.Show(this, T.Get(_language, "Datei konnte nicht gelesen werden", "Could not read the file"), ex.Message, _language, error: true);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try { SecretNames.Validate(SecretName); }
        catch (ArgumentException) { StyledDialog.Show(this, T.Get(_language, "Ungültiger Name", "Invalid name"), T.Get(_language, "Gib einen gültigen Namen für den Satz an.", "Enter a valid set name."), _language); return; }
        if (string.IsNullOrWhiteSpace(CodesBox.Text)) { StyledDialog.Show(this, T.Get(_language, "Keine Codes", "Missing codes"), T.Get(_language, "Gib die Codes ein oder importiere sie aus einer Datei.", "Enter the codes or import them from a file."), _language); return; }
        if (CodesBox.Text.Trim().Length > 2560) { StyledDialog.Show(this, T.Get(_language, "Zu viele Daten", "Too much data"), T.Get(_language, "Der Codesatz ist zu lang (maximal 2560 Zeichen).", "The recovery-code set is too long (maximum 2560 characters)."), _language); return; }
        DialogResult = true;
    }

    private void ApplyLanguage()
    {
        Title = T.Get(_language, "Wiederherstellungscodes — Codex Vault", "Recovery codes — Codex Vault");
        HeadingText.Text = T.Get(_language, "Wiederherstellungscodes speichern", "Save recovery codes");
        DescriptionText.Text = T.Get(_language, "Gib einen Code pro Zeile ein oder importiere eine Textdatei.", "Enter one code per line or import a text file.");
        NameLabel.Text = T.Get(_language, "Name des Satzes", "Set name");
        CodesLabel.Text = T.Get(_language, "Wiederherstellungscodes", "Recovery codes");
        ImportButton.Content = T.Get(_language, "Aus Datei importieren…", "Import from file…");
        SourceWarningText.Text = T.Get(_language, "⚠ Der Import löscht oder verschlüsselt die Quelldatei nicht. Sichere oder lösche sie selbst, nachdem du die Codes gespeichert hast.", "⚠ Import does not delete or encrypt the source file. Secure or delete it yourself after saving the codes.");
        ClipboardWarningText.Text = T.Get(_language, "Einfügen kann eine Kopie im Verlauf oder der Synchronisierung der Windows-Zwischenablage hinterlassen.", "Pasting may leave a copy in Windows clipboard history or sync.");
        CancelButton.Content = T.Get(_language, "Abbrechen", "Cancel");
        SaveButton.Content = T.Get(_language, "Codes speichern", "Save codes");
        UpdateCount();
    }

    private static SecureString ToSecureString(string value)
    {
        var result = new SecureString();
        foreach (var character in value) result.AppendChar(character);
        result.MakeReadOnly();
        return result;
    }
}
