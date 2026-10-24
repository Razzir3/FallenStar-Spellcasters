using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class NoticeUI : MonoBehaviour
{
    [SerializeField] private GameObject m_root;
    [SerializeField] private TMP_Text m_text;
    [SerializeField] private float m_duration = 6f;

    private LocalizedString m_notice;
    private string m_noticeKey;
    private bool m_isShown;

    private void OnEnable()
    {
        if (m_root == null && transform.childCount > 0)
            m_root = transform.GetChild(0).gameObject;
        PlayerNotice.Changed += Refresh;
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        Refresh();
    }

    private void OnDisable()
    {
        PlayerNotice.Changed -= Refresh;
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
    }

    private void Update()
    {
        if (m_isShown && !PlayerNotice.IsActive(m_duration))
            SetShown(false);
    }

    private void OnSelectedLocaleChanged(Locale locale) => Refresh();

    private void Refresh()
    {
        bool _active = PlayerNotice.IsActive(m_duration);
        if (_active && m_text != null)
        {
            if (m_notice == null || m_noticeKey != PlayerNotice.Key)
            {
                m_noticeKey = PlayerNotice.Key;
                m_notice = new LocalizedString("UI_table", m_noticeKey);
            }
            m_text.text = m_notice.GetLocalizedString();
        }
        SetShown(_active);
    }

    private void SetShown(bool shown)
    {
        m_isShown = shown;
        if (m_root != null)
            m_root.SetActive(shown);
    }
}
