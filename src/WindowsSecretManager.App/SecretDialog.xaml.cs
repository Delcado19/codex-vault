using System.Security;
using System.Windows;
using WindowsSecretManager.Core;

namespace WindowsSecretManager.App;

public partial class SecretDialog : Window
{
    private readonly UiLanguage _language;
    public string SecretName => NameBox.Text.Trim();
    public SecureString SecretValue => ValueBox.SecurePassword.Copy();

    public SecretDialog(string? existingName = null, UiLanguage language = UiLanguage.German)
    {
        _language = language;
        InitializeComponent();
        DarkTitleBar.Enable(this);
        ApplyLanguage(existingName is not null);
        NameBox.Text = existingName ?? string.Empty;
        Loaded += (_, _) =>
        {
            if (existingName is null) NameBox.Focus();
            else ValueBox.Focus();
        };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try { SecretNames.Validate(SecretName); }
        catch (ArgumentException) { StyledDialog.Show(this, T.Get(_language, "Ungültiger Name", "Invalid name"), T.Get(_language, "Gib einen Namen ein (keine Steuerzeichen, maximal 256 Zeichen).", "Enter a name (no control characters, maximum 256 characters)."), _language); return; }
        if (ValueBox.SecurePassword.Length == 0) { StyledDialog.Show(this, T.Get(_language, "Kein Wert", "Missing value"), T.Get(_language, "Das Secret darf nicht leer sein.", "The secret cannot be empty."), _language); return; }
        DialogResult = true;
    }

    private void ApplyLanguage(bool rotating)
    {
        Title = T.Get(_language, rotating ? "Secret ändern — Codex Vault" : "Neues Secret — Codex Vault", rotating ? "Change secret — Codex Vault" : "New secret — Codex Vault");
        HeadingText.Text = T.Get(_language, rotating ? "Sicheres Secret ändern" : "Sicheres Secret speichern", rotating ? "Change a secure secret" : "Save a secure secret");
        DescriptionText.Text = T.Get(_language, "Der Wert geht direkt an den Windows Credential Manager.", "The value goes directly to Windows Credential Manager.");
        NameLabel.Text = T.Get(_language, "Name", "Name");
        ValueLabel.Text = T.Get(_language, "Secret-Wert", "Secret value");
        PrivacyText.Text = T.Get(_language, "Der Wert bleibt verborgen und wird nie in eine Klartextdatei geschrieben.", "The value stays hidden and is never saved to a plain file.");
        ClipboardWarningText.Text = T.Get(_language, "Verlauf und Synchronisierung der Windows-Zwischenablage können ein kopiertes Secret behalten.", "Windows clipboard history and sync may retain a copied secret.");
        CancelButton.Content = T.Get(_language, "Abbrechen", "Cancel");
        SaveButton.Content = T.Get(_language, rotating ? "Änderung speichern" : "Secret speichern", rotating ? "Save change" : "Save secret");
    }
}
