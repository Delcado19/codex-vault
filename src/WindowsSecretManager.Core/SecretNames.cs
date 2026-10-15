namespace WindowsSecretManager.Core;

public static class SecretNames
{
    public const string Prefix = "Codex.Shared/";
    public const string SharedPrefix = "SharedSecrets/";
    public const string RecoveryCodesPrefix = "RecoveryCodes/";

    public static readonly IReadOnlyList<string> Suggested = new[]
    {
        "Codex.Shared/GMX/ImapPassword",
        "Codex.Shared/AI/GeminiApiKey",
        "Codex.Shared/AI/OpenAIApiKey"
    };

    public static string Normalize(string name)
    {
        name = (name ?? string.Empty).Trim();
        if (!IsSupported(name))
            name = Prefix + name.TrimStart('/', '\\');
        return name;
    }

    public static bool IsSupported(string name) =>
        (name.StartsWith(Prefix, StringComparison.Ordinal) && name.Length > Prefix.Length) ||
        (name.StartsWith(SharedPrefix, StringComparison.Ordinal) && name.Length > SharedPrefix.Length) ||
        (name.StartsWith(RecoveryCodesPrefix, StringComparison.Ordinal) && name.Length > RecoveryCodesPrefix.Length) ||
        IsApplicationName(name);

    public static bool IsRecoveryCodesName(string name) =>
        name.StartsWith(RecoveryCodesPrefix, StringComparison.Ordinal) && name.Length > RecoveryCodesPrefix.Length;

    public static bool IsApplicationName(string name)
    {
        var slash = name.IndexOf('/');
        if (slash < 3 || slash == name.Length - 1) return false;
        var owner = name[..slash];
        return owner.Contains('.') && owner.All(c => char.IsLetterOrDigit(c) || c is '.' or '-');
    }

    public static void Validate(string name)
    {
        if (!IsSupported(name))
            throw new ArgumentException("The name does not match a supported secret scope.", nameof(name));
        if (name.Length > 256)
            throw new ArgumentException("The name is too long (maximum 256 characters).", nameof(name));
        if (name.Any(char.IsControl))
            throw new ArgumentException("The name must not contain control characters.", nameof(name));
    }
}
