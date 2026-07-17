using UnityEngine;
using System;
using TMPro;

[RequireComponent(typeof(TMP_Text))]
public class LocalizedTMPFormat : MonoBehaviour
{
    [SerializeField] private TMP_Text m_text;

    [Header("Line Rules")]
    [Min(1)]
    [SerializeField] private int m_maxLines = 1;

    [Header("Font Size")]
    [SerializeField] private bool m_autoSize = true;
    [SerializeField] private float m_minFontSize = 14f;
    [SerializeField] private float m_maxFontSize = 72f;

    [Header("Overflow")]
    [SerializeField] private bool m_useEllipsis = true;

    private void Reset()
    {
        m_text = GetComponent<TMP_Text>();
    }

    private void Awake()
    {
        if(m_text == null)
        {
            m_text = GetComponent<TMP_Text>();
        }

        ApplyFormat();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if(m_text == null)
        {
            m_text = GetComponent<TMP_Text>();
        }

        ApplyFormat();
    }
#endif

    public void SetText(string value)
    {
        if(m_text == null)
            {
                m_text = GetComponent<TMP_Text>();
            }
        m_text.text = NormalizeLines(value);
        ApplyFormat();
    }

    private void ApplyFormat()
    {
        if (m_text == null)
            return;

        m_text.textWrappingMode = TextWrappingModes.NoWrap;
        m_text.maxVisibleLines = m_maxLines;

        m_text.enableAutoSizing = m_autoSize;
        m_text.fontSizeMin = m_minFontSize;
        m_text.fontSizeMax = m_maxFontSize;

        m_text.overflowMode = m_useEllipsis ? TextOverflowModes.Ellipsis : TextOverflowModes.Truncate;

        m_text.ForceMeshUpdate();
    }

    private string NormalizeLines(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        value = value.Replace("\r\n", "\n").Replace("\r", "\n");

        string[] _lines = value.Split('\n');

        if ((m_maxLines <=1))
        {
            return string.Join(" ", _lines);
        }
        if(_lines.Length <= m_maxLines)
        {
            return value;
        }

        string[] _result = new string[m_maxLines];

        for(int i = 0; i< m_maxLines; i++)
        {
            _result[i] = _lines[i];
        }

        _result[m_maxLines - 1] = string.Join(" ", _lines, m_maxLines - 1, _lines.Length - m_maxLines + 1);

        return string.Join("\n", _result);
    }
}
