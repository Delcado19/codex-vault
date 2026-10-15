using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Text.RegularExpressions;

namespace WindowsSecretManager.App;

public partial class RecoveryCodesViewer : Window
{
    private readonly UiLanguage _language;
    private readonly DispatcherTimer _clipboardTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private uint _copiedClipboardSequence;
    private readonly int _columns;

    public RecoveryCodesViewer(string entryName, string value, UiLanguage language)
    {
        _language = language;
        InitializeComponent();
        DarkTitleBar.Enable(this);
        EntryNameText.Text = entryName;
        var parsed = ParseCodes(value);
        _columns = parsed.Columns;
        CodesList.ItemsSource = parsed.Codes;
        Loaded += (_, _) =>
        {
            CodesList.UpdateLayout();
            UpdateCellWidth();
        };
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

    private void CodesList_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateCellWidth();

    private void UpdateCellWidth()
    {
        var panel = FindVisualChild<WrapPanel>(CodesList);
        if (panel is null || CodesList.ActualWidth <= 0) return;
        var availableWidth = Math.Max(120, CodesList.ActualWidth - 20);
        panel.ItemWidth = availableWidth / _columns;
    }

    private static (string[] Codes, int Columns) ParseCodes(string value)
    {
        var lines = value.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
        var rows = lines.Select(line => Regex.Split(line, @"\t+|\s{2,}")
            .Where(cell => cell.Length > 0).ToArray()).ToArray();
        var columns = rows.Length > 0 && rows.All(row => row.Length == 2) ? 2 : 1;
        return columns == 2
            ? (rows.SelectMany(row => row).ToArray(), 2)
            : (lines, 1);
    }

    private static T? FindVisualChild<T>(System.Windows.DependencyObject parent) where T : System.Windows.DependencyObject
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
            if (child is T match) return match;
            var nested = FindVisualChild<T>(child);
            if (nested is not null) return nested;
        }
        return null;
    }

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
