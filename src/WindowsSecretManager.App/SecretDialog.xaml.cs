using System.Security;
using System.Windows;
using WindowsSecretManager.Core;

namespace WindowsSecretManager.App;

public partial class SecretDialog : Window
{
    private readonly UiLanguage _language;
    public string SecretName => BuildName();
    public SecureString SecretValue => ValueBox.SecurePassword.Copy();

    public SecretDialog(string? existingName = null, UiLanguage language = UiLanguage.German)
    {
        _language = language;
        InitializeComponent();
        DarkTitleBar.Enable(this);
        ApplyLanguage(existingName is not null);
        if (existingName is not null)
        {
            SetName(existingName);
        }
        else NameBox.Text = "GMX/ImapPassword";
        Loaded += (_, _) =>
        {
            if (existingName is null) NameBox.Focus();
            else ValueBox.Focus();
        };
    }

    private string BuildName()
    {
        var suffix = NameBox.Text.Trim().TrimStart('/', '\\');
        if (SharedScope.IsChecked == true) return SecretNames.SharedPrefix + suffix;
        if (ApplicationScope.IsChecked == true) return OwnerBox.Text.Trim().TrimEnd('/') + "/" + suffix;
        return SecretNames.Prefix + suffix;
    }

    private void Scope_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;
        OwnerPanel.Visibility = ApplicationScope.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        UpdatePreview();
    }

    private void NamePart_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (IsInitialized) UpdatePreview();
    }

    private void UpdatePreview() => FullNameText.Text = BuildName();

    private void SetName(string name)
    {
        if (name.StartsWith(SecretNames.Prefix, StringComparison.Ordinal))
        {
            CodexScope.IsChecked = true;
            NameBox.Text = name[SecretNames.Prefix.Length..];
        }
        else if (name.StartsWith(SecretNames.SharedPrefix, StringComparison.Ordinal))
        {
            SharedScope.IsChecked = true;
            NameBox.Text = name[SecretNames.SharedPrefix.Length..];
        }
        else
        {
            ApplicationScope.IsChecked = true;
            var separator = name.IndexOf('/');
            OwnerBox.Text = separator > 0 ? name[..separator] : "Vendor.Application";
            NameBox.Text = separator > 0 ? name[(separator + 1)..] : name;
        }
        OwnerPanel.Visibility = ApplicationScope.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        UpdatePreview();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try { SecretNames.Validate(SecretName); }
        catch (ArgumentException) { StyledDialog.Show(this, T.Get(_language, "Ungültiger Name", "Invalid name"), T.Get(_language, "Gib einen gültigen Namen im gewählten Scope an.", "Enter a valid name in the selected scope."), _language); return; }
        if (ValueBox.SecurePassword.Length == 0) { StyledDialog.Show(this, T.Get(_language, "Kein Wert", "Missing value"), T.Get(_language, "Das Secret darf nicht leer sein.", "The secret cannot be empty."), _language); return; }
        DialogResult = true;
    }

    private void ApplyLanguage(bool rotating)
    {
        Title = T.Get(_language, rotating ? "Secret ändern — Codex Vault" : "Neues Secret — Codex Vault", rotating ? "Change secret — Codex Vault" : "New secret — Codex Vault");
        HeadingText.Text = T.Get(_language, rotating ? "Sicheres Secret ändern" : "Sicheres Secret speichern", rotating ? "Change a secure secret" : "Save a secure secret");
        DescriptionText.Text = T.Get(_language, "Der Wert geht direkt an den Windows Credential Manager.", "The value goes directly to Windows Credential Manager.");
        ScopeLabel.Text = T.Get(_language, "Secret-Umgebung", "Secret environment");
        CodexScope.Content = T.Get(_language, "Codex-Projekte", "Projects in Codex") + " — Codex.Shared/*";
        SharedScope.Content = T.Get(_language, "Geteilt", "Shared") + " — SharedSecrets/*";
        ApplicationScope.Content = T.Get(_language, "Anwendung", "Application") + " — Vendor.Application/*";
        OwnerLabel.Text = T.Get(_language, "Hersteller und Anwendung", "Vendor and application");
        NameLabel.Text = T.Get(_language, "Name innerhalb der gewählten Umgebung", "Name within the selected environment");
        ValueLabel.Text = T.Get(_language, "Secret-Wert", "Secret value");
        PrivacyText.Text = T.Get(_language, "Der Wert bleibt verborgen und wird nie in eine Klartextdatei geschrieben.", "The value stays hidden and is never saved to a plain file.");
        ClipboardWarningText.Text = T.Get(_language, "Verlauf und Synchronisierung der Windows-Zwischenablage können ein kopiertes Secret behalten.", "Windows clipboard history and sync may retain a copied secret.");
        CancelButton.Content = T.Get(_language, "Abbrechen", "Cancel");
        SaveButton.Content = T.Get(_language, rotating ? "Änderung speichern" : "Secret speichern", rotating ? "Save change" : "Save secret");
    }
}
