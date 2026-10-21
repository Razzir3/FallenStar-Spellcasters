using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class DisplaySettingsUI : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown m_displayModeDropdown;
    [SerializeField] private TMP_Dropdown m_resolutionDropdown;
    [SerializeField] private int m_minWidth = 1024;
    [SerializeField] private int m_minHeight = 720;

    private readonly LocalizedString m_fullscreenText = new("UI_table", "option_fullscreen");
    private readonly LocalizedString m_windowedText = new("UI_table", "option_windowed");
    private List<Vector2Int> m_resolutions = new();
    private bool m_isInitialized;
    private int m_lastWidth;
    private int m_lastHeight;
    private FullScreenMode m_lastMode;

    private IEnumerator Start()
    {
        yield return LocalizationSettings.InitializationOperation;

        if (m_displayModeDropdown == null)
            Debug.LogError("DisplaySettingsUI: display-mode dropdown is missing.");
        if (m_resolutionDropdown == null)
            Debug.LogError("DisplaySettingsUI: resolution dropdown is missing.");

        RefreshDropdowns(new Vector2Int(Screen.width, Screen.height), Screen.fullScreenMode);
        m_isInitialized = true;
        if (isActiveAndEnabled)
            AddListeners();
    }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        if (m_isInitialized)
        {
            RefreshDropdowns(new Vector2Int(Screen.width, Screen.height), Screen.fullScreenMode);
            AddListeners();
        }
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        if (m_displayModeDropdown != null)
            m_displayModeDropdown.onValueChanged.RemoveListener(OnDisplayModeChanged);
        if (m_resolutionDropdown != null)
            m_resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
    }

    private void AddListeners()
    {
        if (m_displayModeDropdown != null)
        {
            m_displayModeDropdown.onValueChanged.RemoveListener(OnDisplayModeChanged);
            m_displayModeDropdown.onValueChanged.AddListener(OnDisplayModeChanged);
        }
        if (m_resolutionDropdown != null)
        {
            m_resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
            m_resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        }
    }

    private void Update()
    {
        if (!m_isInitialized)
            return;

        int _width = Screen.width;
        int _height = Screen.height;
        FullScreenMode _mode = Screen.fullScreenMode;
        if (_width != m_lastWidth || _height != m_lastHeight || _mode != m_lastMode)
            RefreshDropdowns(new Vector2Int(_width, _height), _mode);
    }

    private void RefreshDropdowns(Vector2Int current, FullScreenMode mode)
    {
        m_lastWidth = current.x;
        m_lastHeight = current.y;
        m_lastMode = mode;
        FillDisplayModes(mode == FullScreenMode.Windowed ? 1 : 0);
        FillResolutions(current, mode == FullScreenMode.Windowed);
    }

    private void FillDisplayModes(int index)
    {
        if (m_displayModeDropdown == null)
            return;

        m_displayModeDropdown.ClearOptions();
        m_displayModeDropdown.AddOptions(new List<string> {
            m_fullscreenText.GetLocalizedString(), m_windowedText.GetLocalizedString()
        });
        m_displayModeDropdown.SetValueWithoutNotify(index);
        m_displayModeDropdown.RefreshShownValue();
    }

    private void FillResolutions(Vector2Int current, bool windowed)
    {
        m_resolutions = BuildResolutionList(GetAvailableResolutions(), GetDisplaySize(), windowed,
            current, new Vector2Int(m_minWidth, m_minHeight));
        if (m_resolutionDropdown == null)
            return;

        var _options = new List<string>();
        foreach (Vector2Int _resolution in m_resolutions)
            _options.Add($"{_resolution.x} × {_resolution.y}");
        m_resolutionDropdown.ClearOptions();
        m_resolutionDropdown.AddOptions(_options);
        m_resolutionDropdown.SetValueWithoutNotify(m_resolutions.IndexOf(current));
        m_resolutionDropdown.RefreshShownValue();
    }

    private static List<Vector2Int> GetAvailableResolutions()
    {
        var _available = new List<Vector2Int>();
        foreach (Resolution _resolution in Screen.resolutions)
            _available.Add(new Vector2Int(_resolution.width, _resolution.height));
        return _available;
    }

    private static Vector2Int GetDisplaySize()
    {
        DisplayInfo _display = Screen.mainWindowDisplayInfo;
        return new Vector2Int(_display.width, _display.height);
    }

    private static bool FitsDisplay(Vector2Int resolution, Vector2Int display, bool windowed)
    {
        return windowed ? resolution.x < display.x && resolution.y < display.y :
            resolution.x <= display.x && resolution.y <= display.y;
    }

    public static List<Vector2Int> BuildResolutionList(IEnumerable<Vector2Int> available,
        Vector2Int display, bool windowed, Vector2Int current, Vector2Int min)
    {
        var _unique = new HashSet<Vector2Int>();
        foreach (Vector2Int _resolution in available)
        {
            if (_resolution.x >= min.x && _resolution.y >= min.y && FitsDisplay(_resolution, display, windowed))
                _unique.Add(_resolution);
        }
        // The actual size is a display-only exception to the filters (e.g. a command-line size).
        _unique.Add(current);
        var _result = new List<Vector2Int>(_unique);
        _result.Sort((_left, _right) => {
            int _width = _right.x.CompareTo(_left.x);
            return _width != 0 ? _width : _right.y.CompareTo(_left.y);
        });
        return _result;
    }

    private void OnDisplayModeChanged(int index)
    {
        if (index < 0 || index > 1)
            return;

        bool _windowed = index == 1;
        Vector2Int _display = GetDisplaySize();
        Vector2Int _current = new(Screen.width, Screen.height);
        Vector2Int _target = _windowed ? _current : _display;
        List<Vector2Int> _available = BuildResolutionList(GetAvailableResolutions(), _display, _windowed,
            _current, new Vector2Int(m_minWidth, m_minHeight));
        if (_windowed && !FitsDisplay(_current, _display, true))
        {
            // Do not retain a full-monitor size merely because the truthful list includes it.
            foreach (Vector2Int _resolution in _available)
            {
                if (_resolution.x >= m_minWidth && _resolution.y >= m_minHeight && FitsDisplay(_resolution, _display, true))
                {
                    _target = _resolution;
                    break;
                }
            }
        }

        Screen.SetResolution(_target.x, _target.y,
            _windowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow);
        FillDisplayModes(index);
        FillResolutions(_target, _windowed);
    }

    private void OnResolutionChanged(int index)
    {
        if (index < 0 || index >= m_resolutions.Count)
            return;

        bool _windowed = m_displayModeDropdown != null ? m_displayModeDropdown.value == 1 :
            Screen.fullScreenMode == FullScreenMode.Windowed;
        Vector2Int _resolution = m_resolutions[index];
        Screen.SetResolution(_resolution.x, _resolution.y,
            _windowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow);
    }

    private void OnLocaleChanged(Locale _)
    {
        if (m_isInitialized && m_displayModeDropdown != null)
            FillDisplayModes(m_displayModeDropdown.value);
    }
}
