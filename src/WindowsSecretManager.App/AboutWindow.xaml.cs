using System.Reflection;
using System.Windows;

namespace WindowsSecretManager.App;

public partial class AboutWindow : Window
{
    public AboutWindow(UiLanguage language)
    {
        InitializeComponent();
        DarkTitleBar.Enable(this);
        Title = T.Get(language, "Über — Codex Vault", "About — Codex Vault");
        VersionText.Text = $"{T.Get(language, "Version", "Version")} {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";
        DescriptionText.Text = T.Get(language,
            "Eine fokussierte Oberfläche für Secrets, die im Windows Credential Manager gespeichert sind.",
            "A focused interface for secrets stored by Windows Credential Manager.");
        VaultText.Text = T.Get(language,
            "Windows ist der Tresor; Codex Vault ist eine aufgeräumte Oberfläche für einen ausgewählten Teil davon. Die App legt keine eigene Datenbank an und speichert keine Secrets in ihrem Ordner.",
            "Windows is the vault; Codex Vault is an organized interface to a selected part of it. The app creates no private database and stores no secrets in its folder.");
        ScopeText.Text = T.Get(language,
            "Die App erzwingt Namenskonventionen, verbirgt Werte standardmäßig, verlangt bewusste Aktionen und unterstützt Codex-, geteilte sowie app-eigene Scopes.",
            "The app enforces naming, hides values by default, requires deliberate actions, and supports Codex, shared, and application-owned scopes.");
        AuthorLabel.Text = T.Get(language, "Autor", "Author");
        LicenseLabel.Text = T.Get(language, "Lizenz", "License");
        CloseButton.Content = T.Get(language, "Schließen", "Close");
    }
}
