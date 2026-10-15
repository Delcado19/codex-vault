using Windows.Security.Credentials.UI;

namespace WindowsSecretManager.App;

internal static class WindowsHelloGate
{
    public static async Task<bool> VerifyAsync(UiLanguage language, string operation)
    {
        try
        {
            var availability = await UserConsentVerifier.CheckAvailabilityAsync();
            if (availability != UserConsentVerifierAvailability.Available) return DenyUnavailable(language, availability);

            var message = T.Get(language,
                $"Codex Vault: potwierdź operację „{operation}”",
                $"Codex Vault: confirm “{operation}”");
            var result = await UserConsentVerifier.RequestVerificationAsync(message);
            if (result != UserConsentVerificationResult.Verified) return false;
            return true;
        }
        catch
        {
            // No bypass on an unexpected WinRT failure either — losing the biometric
            // check to an exception would be the same hole as the old "continue anyway" prompt.
            return DenyUnavailable(language, null);
        }
    }

    // Fails closed: there is no "continue anyway" path. A missing Hello factor must be fixed
    // in Windows (even a PIN counts), not clicked through from inside the app.
    private static bool DenyUnavailable(UiLanguage language, UserConsentVerifierAvailability? availability)
    {
        var detail = availability switch
        {
            UserConsentVerifierAvailability.NotConfiguredForUser => T.Get(language,
                "Windows Hello nie jest skonfigurowane. Ustaw PIN lub inną metodę Hello w Ustawieniach Windows (Konta → Opcje logowania).",
                "Windows Hello is not set up. Set up a PIN or another Hello method in Windows Settings (Accounts → Sign-in options)."),
            UserConsentVerifierAvailability.DisabledByPolicy => T.Get(language,
                "Windows Hello jest wyłączone przez zasady systemu. Ta operacja jest niedostępna.",
                "Windows Hello is disabled by policy. This operation is unavailable."),
            UserConsentVerifierAvailability.DeviceBusy => T.Get(language,
                "Windows Hello jest obecnie zajęte. Spróbuj ponownie za chwilę.",
                "Windows Hello is currently busy. Try again in a moment."),
            _ => T.Get(language, "Windows Hello nie jest dostępne. Ta operacja jest niedostępna.", "Windows Hello is unavailable. This operation is unavailable.")
        };
        StyledDialog.Show(null, "Codex Vault", detail, language, error: true);
        return false;
    }
}
