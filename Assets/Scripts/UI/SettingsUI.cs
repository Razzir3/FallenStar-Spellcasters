using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class SettingsUI : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown m_languageDropdown;

    private readonly List<Locale> m_locales = new List<Locale>();
    private bool m_isInitializing;

    private IEnumerator Start()
    {
        yield return LocalizationSettings.InitializationOperation;

        InitializeLanguageDropdown();
    }

    private void InitializeLanguageDropdown()
    {
        if (m_languageDropdown == null)
        {
            Debug.LogError("SettingsUI: language dropdown is missing.");
            return;
        }

        m_locales.Clear();
        m_locales.AddRange(LocalizationSettings.AvailableLocales.Locales);

        List<TMP_Dropdown.OptionData> _options = new List<TMP_Dropdown.OptionData>();

        foreach (Locale _locale in m_locales)
        {
            string _displayName = _locale.Identifier.CultureInfo?.NativeName;
            if (!string.IsNullOrEmpty(_displayName))
                _displayName = char.ToUpper(_displayName[0], System.Globalization.CultureInfo.CurrentCulture) + _displayName.Substring(1);
            else
                _displayName = _locale.LocaleName;

            if(string.IsNullOrEmpty(_displayName))
            {
                Debug.LogWarning($"SettingsUI: Locale '{_locale.Identifier.Code}' has no display name.");
                _displayName = _locale.Identifier.Code;
            }

            _options.Add(new TMP_Dropdown.OptionData(_displayName));
        }

        m_isInitializing = true;

        m_languageDropdown.ClearOptions();
        m_languageDropdown.AddOptions(_options);

        int _selectedIndex = GetCurrentLanguageIndex();
        m_languageDropdown.SetValueWithoutNotify(_selectedIndex);
        m_languageDropdown.RefreshShownValue();

        m_isInitializing = false;

        m_languageDropdown.onValueChanged.RemoveListener(OnLanguageChanged);
        m_languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
    }

    private int GetCurrentLanguageIndex()
    {
        string _savedCode = GameSettingsPrefs.LanguageCode;

        if(!string.IsNullOrEmpty(_savedCode))
        {
            for(int i = 0; i < m_locales.Count; i++)
            {
                if(m_locales[i].Identifier.Code == _savedCode)
                {
                    return i;
                }
            }
        }

        Locale _currentLocale = LocalizationSettings.SelectedLocale;

        for(int i = 0; i<m_locales.Count;i++)
        {
            if (m_locales[i] == _currentLocale)
            {
                return i;
            }
        }

        return 0;
    }

    private void OnLanguageChanged(int index)
    {
        if (m_isInitializing)
            return;

        if ((index < 0 || index >= m_locales.Count))
            return;

        Locale _selectedLocale = m_locales[index];

        LocalizationSettings.SelectedLocale = _selectedLocale;

        GameSettingsPrefs.LanguageCode = _selectedLocale.Identifier.Code;
    }
}
