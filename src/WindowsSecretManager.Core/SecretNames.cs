namespace WindowsSecretManager.Core;

public static class SecretNames
{
    // Kept so WindowsCredentialStore.ListNames still recognizes secrets saved by
    // older versions of the app under these prefixes; naming itself is free-form
    // now, Comment == "Managed by Codex Vault" is what identifies our own entries.
    public const string Prefix = "Codex.Shared/";
    public const string SharedPrefix = "SharedSecrets/";
    public const string RecoveryCodesPrefix = "RecoveryCodes/";

    public static string Normalize(string name) => (name ?? string.Empty).Trim();

    public static bool IsRecoveryCodesName(string name) =>
        name.StartsWith(RecoveryCodesPrefix, StringComparison.Ordinal) && name.Length > RecoveryCodesPrefix.Length;

    public static void Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("The name cannot be empty.", nameof(name));
        if (name.Length > 256)
            throw new ArgumentException("The name is too long (maximum 256 characters).", nameof(name));
        if (name.Any(char.IsControl))
            throw new ArgumentException("The name must not contain control characters.", nameof(name));
    }
}
