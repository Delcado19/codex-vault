using System.Runtime.InteropServices;
using System.Security;
using WindowsSecretManager.Core;

namespace WindowsSecretManager.App;

internal static class SecureStringText
{
    public static string Read(SecureString value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var pointer = Marshal.SecureStringToGlobalAllocUnicode(value);
        using var pin = NativeMemoryLock.Pin(pointer, value.Length * 2);
        try { return Marshal.PtrToStringUni(pointer) ?? string.Empty; }
        finally { Marshal.ZeroFreeGlobalAllocUnicode(pointer); }
    }

    public static SecureString Create(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = new SecureString();
        foreach (var character in value) result.AppendChar(character);
        result.MakeReadOnly();
        return result;
    }
}
