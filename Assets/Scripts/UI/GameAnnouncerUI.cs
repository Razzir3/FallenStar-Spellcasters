using UnityEngine;
using TMPro;
using Unity.Netcode;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using System.Collections.Generic;

public enum PlayerColorId
{
    Red = 0,
    Blue = 1
}

public class GameAnnouncerUI : MonoBehaviour
{
    [SerializeField] private TMP_Text m_timeText;

    private const string UI_TABLE = "UI_table";
    private readonly Dictionary<string, LocalizedString> m_localizedStrings = new();
    private readonly HashSet<string> m_truncatedTexts = new();
    private bool m_hasAnnouncerState;
    private GamePhase m_lastPhase;
    private int m_lastSeconds, m_lastTeam;
    private ulong m_lastTargetId;
    private bool m_lastHasTarget, m_lastIsLocalPlayer;
    private Locale m_lastLocale;

    [Header("Announcer Text Format")]
    [SerializeField, Min(1)] private int m_maxVisibleLines = 2;

    [SerializeField] private bool m_autoSize = true;
    [SerializeField] private float m_minFontSize = 14f;
    [SerializeField] private float m_maxFontSize = 72f;

    [SerializeField] private bool m_useEllipsis = true;

    private void Awake()
    {
        ApplyAnnouncerTextFormat();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyAnnouncerTextFormat();
    }
#endif

    private void Update()
    {
        UpdateAnnounceText();
    }

    private void UpdateAnnounceText()
    {
        if(m_timeText == null || GameMaster.Instance == null)
        {
            m_hasAnnouncerState = false;
            return;
        }

        GamePhase _currentPhase = GameMaster.Instance.m_gamePhase.Value;

        PlayerSession _targetSession = GetTargetSession(_currentPhase);
        int _seconds = GameMaster.Instance.GetPhaseSecondsLeft();
        bool _hasTarget = _targetSession != null;
        ulong _targetId = _hasTarget ? _targetSession.NetworkObjectId : 0;
        int _teamId = _hasTarget ? _targetSession.TeamId.Value : -1;
        bool _isLocalPlayer = _hasTarget && SessionManager.Instance != null &&
            SessionManager.Instance.LocalPlayer != null &&
            _targetId == SessionManager.Instance.LocalPlayer.NetworkObjectId;
        Locale _locale = LocalizationSettings.SelectedLocale;

        if (m_hasAnnouncerState && m_lastPhase == _currentPhase && m_lastSeconds == _seconds &&
            m_lastHasTarget == _hasTarget && m_lastTargetId == _targetId && m_lastTeam == _teamId &&
            m_lastIsLocalPlayer == _isLocalPlayer && m_lastLocale == _locale)
            return;

        m_hasAnnouncerState = true;
        m_lastPhase = _currentPhase;
        m_lastSeconds = _seconds;
        m_lastHasTarget = _hasTarget;
        m_lastTargetId = _targetId;
        m_lastTeam = _teamId;
        m_lastIsLocalPlayer = _isLocalPlayer;
        m_lastLocale = _locale;

        string _phaseText = GetLocalized(GetPhaseLocalizationKey(_currentPhase));
        string _timerText = _seconds.ToString("00");

        if(_targetSession == null)
        {
            SetAnnouncerText($"{_phaseText} {_timerText}\n");
            return;
        }

        string _colorName = GetLocalized(GetPlayerColorLocalizationKey(_teamId));
        string _colorHex = GetPlayerColorHex(_teamId);

        string _prefixKey = "";
        string _coloredPartKey = "";

        switch(_currentPhase)
        {
            case GamePhase.RespawnWait:
                _prefixKey = "Announcer.RespawnPrefix";
                _coloredPartKey = _isLocalPlayer ? "Announcer.YourRespawnColoredPart" : "Announcer.OpponentRespawnColoredPart";
                break;
            case GamePhase.PlayerAction:
                _prefixKey = "Announcer.TurnPrefix";
                _coloredPartKey = _isLocalPlayer ? "Announcer.YourTurnColoredPart" : "Announcer.OpponentTurnColoredPart";
                break;
            default:
                break;
        }

        string _prefix = GetLocalized(_prefixKey);
        string _coloredPart = GetLocalized(_coloredPartKey, _colorName);

        SetAnnouncerText(
            $"{_phaseText} {_timerText}\n" +
            $"{_prefix}<color={_colorHex}>{_coloredPart}</color>");
    }

    private PlayerSession GetTargetSession(GamePhase phase)
    {
        NetworkBehaviourReference _targetReference = null;
        switch(phase)
        {
            case GamePhase.RespawnWait:
                _targetReference = GameMaster.Instance.m_waitingRespawnPlayer.Value;
                break;
            case GamePhase.PlayerAction:
                _targetReference = GameMaster.Instance.m_activePlayer.Value;
                break;
        }

        if(_targetReference.TryGet(out PlayerSession _session))
        {
            return _session;
        }
        return null;
    }

    private string GetPhaseLocalizationKey(GamePhase phase)
    {
        return phase switch
        { GamePhase.None =>"Phase.None",
          GamePhase.RoundBreak => "Phase.RoundBreak",
          GamePhase.DrawCards => "Phase.DrawCards",
          GamePhase.CardSelection => "Phase.CardSelection",
          GamePhase.PlayerAction => "Phase.PlayerAction",
          GamePhase.ResolveCards => "Phase.ResolveCards",
          GamePhase.RespawnWait => "Phase.RespawnWait",
          GamePhase.RoundEnd => "Phase.RoundEnd",
          GamePhase.GameOver => "Phase.GameOver",
          _ => throw new System.ArgumentOutOfRangeException(nameof(phase), phase, null)
        };
    }
    private string GetPlayerColorLocalizationKey(int teamId)
    {
        return teamId switch
        {
            0 => "PlayerColor.Red",
            1 => "PlayerColor.Blue",
            _ => "PlayerColor.Red"
        };
    }

    private string GetPlayerColorHex( int teamId)
    {
        return teamId switch
        {
            0 => "#E84A4A",
            1 => "#4A7CFF",
            _ => "#FFFFFF"
        };
    }

    private string GetLocalized(string key, params object[] args)
    {
        if (!m_localizedStrings.TryGetValue(key, out LocalizedString _localizedString))
        {
            _localizedString = new LocalizedString(UI_TABLE, key);
            m_localizedStrings.Add(key, _localizedString);
        }
        _localizedString.Arguments = args;
        return _localizedString.GetLocalizedString();
    }

    private void SetAnnouncerText(string value)
    {
        if (m_timeText == null)
            return;

        m_timeText.text = NormalizeAnnouncerLines(value);

        ApplyAnnouncerTextFormat();

        m_timeText.ForceMeshUpdate();
        if (m_timeText.isTextTruncated && m_truncatedTexts.Add(m_timeText.text))
            Debug.LogWarning($"Announcer text truncated: {m_timeText.text}");
    }

    private void ApplyAnnouncerTextFormat()
    { 
        if(m_timeText == null)
            return;

        m_timeText.textWrappingMode = TextWrappingModes.NoWrap;
        m_timeText.maxVisibleLines = m_maxVisibleLines;

        m_timeText.enableAutoSizing = m_autoSize;
        m_timeText.fontSizeMin = m_minFontSize;
        m_timeText.fontSizeMax = m_maxFontSize;

        m_timeText.overflowMode = m_useEllipsis ? TextOverflowModes.Ellipsis : TextOverflowModes.Truncate;
    }

    private string NormalizeAnnouncerLines(string value)
    {
        if(string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        value = value.Replace("\r\n", "\n").Replace("\r", "\n");

        string[] _lines = value.Split('\n');

        if(m_maxVisibleLines <= 1)
        {
            return string.Join(" ", _lines);
        }

        if(_lines.Length <= m_maxVisibleLines)
        {
            return value;
        }

        string[] _result = new string[m_maxVisibleLines];

        for(int i = 0; i < m_maxVisibleLines - 1;i++)
        {
            _result[i] = _lines[i];
        }

        _result[m_maxVisibleLines - 1] = string.Join(" ", _lines, m_maxVisibleLines - 1, _lines.Length - (m_maxVisibleLines - 1));

        return string.Join("\n", _result);
    }
}
