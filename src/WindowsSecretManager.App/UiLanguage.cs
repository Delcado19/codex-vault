namespace WindowsSecretManager.App;

public enum UiLanguage { German, English }

public static class T
{
    public static string Get(UiLanguage lang, string german, string english) =>
        lang == UiLanguage.German ? german : english;
}
