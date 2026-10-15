using System.Windows;

namespace WindowsSecretManager.App;

public enum ArchiveConflictChoice { Replace, Skip, Cancel }

public partial class ArchiveConflictDialog : Window
{
    private ArchiveConflictChoice _choice = ArchiveConflictChoice.Cancel;

    public ArchiveConflictDialog(string name, UiLanguage language)
    {
        InitializeComponent();
        DarkTitleBar.Enable(this);
        HeadingText.Text = T.Get(language, "Eintrag existiert bereits", "Entry already exists");
        MessageText.Text = T.Get(language,
            $"Codex Vault enthält bereits:\n{name}\n\nWie soll dieser Konflikt gelöst werden?",
            $"Codex Vault already contains:\n{name}\n\nHow should this conflict be resolved?");
        ReplaceButton.Content = T.Get(language, "Ersetzen", "Replace");
        SkipButton.Content = T.Get(language, "Überspringen", "Skip");
        CancelButton.Content = T.Get(language, "Import abbrechen", "Cancel import");
    }

    public ArchiveConflictChoice ShowChoice()
    {
        ShowDialog();
        return _choice;
    }

    private void Replace_Click(object sender, RoutedEventArgs e) { _choice = ArchiveConflictChoice.Replace; DialogResult = true; }
    private void Skip_Click(object sender, RoutedEventArgs e) { _choice = ArchiveConflictChoice.Skip; DialogResult = true; }
    private void Cancel_Click(object sender, RoutedEventArgs e) { _choice = ArchiveConflictChoice.Cancel; DialogResult = false; }
}
