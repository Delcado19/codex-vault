using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WindowsSecretManager.App;

public partial class RecoveryCodesViewer : Window
{
    private readonly UiLanguage _language;
    private readonly DispatcherTimer _clipboardTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private uint _copiedClipboardSequence;

    public RecoveryCodesViewer(string entryName, string value, UiLanguage language)
    {
        _language = language;
        InitializeComponent();
        DarkTitleBar.Enable(this);
        EntryNameText.Text = RecoveryCodesDisplayName.Strip(entryName);
        CodesList.ItemsSource = ParseCodes(value);
        _clipboardTimer.Tick += (_, _) => ClearClipboard();
        Closed += (_, _) =>
        {
            _clipboardTimer.Stop();
            ClearClipboard();
            CodesList.ItemsSource = null;
        };
        ApplyLanguage();
    }

    private void CodesList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        CopySelectedButton.IsEnabled = CodesList.SelectedItem is string;

    private static string[] ParseCodes(string value) =>
        value.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();

    private void CopySelected_Click(object sender, RoutedEventArgs e)
    {
        if (CodesList.SelectedItem is not string code) return;
        Clipboard.SetText(code);
        _copiedClipboardSequence = GetClipboardSequenceNumber();
        _clipboardTimer.Stop();
        _clipboardTimer.Start();
        StatusText.Text = T.Get(_language, "Code kopiert. Die Zwischenablage wird in 30 Sekunden geleert.", "Code copied. The clipboard will be cleared in 30 seconds.");
    }

    private void ClearClipboard()
    {
        _clipboardTimer.Stop();
        if (_copiedClipboardSequence == 0 || GetClipboardSequenceNumber() != _copiedClipboardSequence) return;
        try
        {
            Clipboard.Clear();
            _copiedClipboardSequence = 0;
            StatusText.Text = T.Get(_language, "Zwischenablage geleert.", "Clipboard cleared.");
        }
        catch
        {
            StatusText.Text = T.Get(_language, "Zwischenablage konnte nicht geleert werden; kopiere einen anderen Wert.", "Could not clear the clipboard; copy another value.");
        }
    }

    private void ApplyLanguage()
    {
        Title = T.Get(_language, "Wiederherstellungscodes — Codex Vault", "Recovery codes — Codex Vault");
        HeadingText.Text = T.Get(_language, "Satz von Wiederherstellungscodes", "Recovery code set");
        InstructionText.Text = T.Get(_language, "Wähle einen Code aus, um nur diesen Wert zu kopieren.", "Select one code to copy only that value.");
        StatusText.Text = T.Get(_language, $"Anzahl Codes: {CodesList.Items.Count}", $"Codes: {CodesList.Items.Count}");
        CloseButton.Content = T.Get(_language, "Schließen", "Close");
        CopySelectedButton.Content = T.Get(_language, "Auswahl kopieren", "Copy selected");
    }

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
}
