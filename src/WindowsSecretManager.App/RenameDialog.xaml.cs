using System.Windows;
using WindowsSecretManager.Core;

namespace WindowsSecretManager.App;

public partial class RenameDialog : Window
{
    private readonly UiLanguage _language;
    public string NewName => NameBox.Text.Trim();

    public RenameDialog(string currentName, UiLanguage language)
    {
        _language = language;
        InitializeComponent();
        DarkTitleBar.Enable(this);
        NameBox.Text = currentName;
        Title = T.Get(language, "Umbenennen — Codex Vault", "Rename — Codex Vault");
        HeadingText.Text = T.Get(language, "Secret-Namen ändern", "Rename the secret");
        DescriptionText.Text = T.Get(language, "Der Wert des Secrets bleibt unverändert.", "The secret value will remain unchanged.");
        NameLabel.Text = T.Get(language, "Neuer vollständiger Name", "New full name");
        ExamplesText.Text = "Codex.Shared/*   ·   SharedSecrets/*   ·   Vendor.Application/*";
        WarningText.Text = T.Get(language, "Die App legt einen Eintrag unter dem neuen Namen an und löscht dann den alten. Anwendungen, die den alten Namen verwenden, müssen aktualisiert werden.", "The app will create an entry under the new name and then delete the old one. Applications using the old name must be updated.");
        CancelButton.Content = T.Get(language, "Abbrechen", "Cancel");
        SaveButton.Content = T.Get(language, "Umbenennen", "Rename");
        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try { SecretNames.Validate(NewName); }
        catch (ArgumentException) { StyledDialog.Show(this, T.Get(_language, "Ungültiger Name", "Invalid name"), T.Get(_language, "Gib einen vollständigen Namen in einem der unterstützten Scopes an.", "Enter a full name in one of the supported scopes."), _language); return; }
        DialogResult = true;
    }
}
