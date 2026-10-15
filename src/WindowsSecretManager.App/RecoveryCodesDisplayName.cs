using System.Globalization;
using System.Windows.Data;
using WindowsSecretManager.Core;

namespace WindowsSecretManager.App;

// The RecoveryCodes/ prefix is internal bookkeeping (it is what tells the app
// "this is a recovery-code set, not a secret"), not something the user chose;
// strip it wherever a name is shown, while every operation still uses the real,
// prefixed name underneath.
internal static class RecoveryCodesDisplayName
{
    public static string Strip(string name) =>
        name.StartsWith(SecretNames.RecoveryCodesPrefix, StringComparison.Ordinal)
            ? name[SecretNames.RecoveryCodesPrefix.Length..]
            : name;
}

internal sealed class RecoveryCodesDisplayNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string name ? RecoveryCodesDisplayName.Strip(name) : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
