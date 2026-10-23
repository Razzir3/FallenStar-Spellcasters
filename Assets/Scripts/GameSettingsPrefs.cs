using UnityEngine;

public static class GameSettingsPrefs
{
    public const string LanguageCodeKey = "setting.language_code";

    public static string LanguageCode
    {
        get
        {
            return PlayerPrefs.GetString(LanguageCodeKey, string.Empty);
        }
        set
        {
            PlayerPrefs.SetString(LanguageCodeKey, value);
            PlayerPrefs.Save();
        }
    }


}
