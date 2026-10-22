using System;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public class ResolutionProfileLayout : MonoBehaviour
{
    [Serializable]
    public class LowRect
    {
        public RectTransform m_target;
        public Vector2 m_anchorMin;
        public Vector2 m_anchorMax;
        public Vector2 m_pivot;
        public Vector2 m_anchoredPosition;
        public Vector2 m_sizeDelta;
        public float m_scale = 1f;
    }

    [SerializeField] private CanvasScaler m_canvasScaler;
    [SerializeField] private Vector2 m_lowReferenceResolution;
    [SerializeField] private LowRect[] m_lowRects = Array.Empty<LowRect>();

    private struct AuthoredRect
    {
        public Vector2 m_anchorMin, m_anchorMax, m_pivot, m_anchoredPosition, m_sizeDelta;
        public Vector3 m_localScale;
    }

    private AuthoredRect[] m_authoredRects;
    private Vector2 m_authoredReferenceResolution;
    private bool m_isLow;

    private void Awake()
    {
        if (m_canvasScaler == null)
            m_canvasScaler = GetComponentInParent<CanvasScaler>();
        if (m_canvasScaler != null)
            m_authoredReferenceResolution = m_canvasScaler.referenceResolution;

        m_authoredRects = new AuthoredRect[m_lowRects.Length];
        bool _hasMissingTarget = false;
        for (int _index = 0; _index < m_lowRects.Length; _index++)
        {
            RectTransform _target = m_lowRects[_index]?.m_target;
            if (_target == null)
            {
                _hasMissingTarget = true;
                continue;
            }
            m_authoredRects[_index] = new AuthoredRect {
                m_anchorMin = _target.anchorMin, m_anchorMax = _target.anchorMax,
                m_pivot = _target.pivot, m_anchoredPosition = _target.anchoredPosition,
                m_sizeDelta = _target.sizeDelta, m_localScale = _target.localScale
            };
        }
        if (_hasMissingTarget)
            Debug.LogWarning("ResolutionProfileLayout: missing rectangle targets are skipped.", this);

        ApplyProfile(UiResolutionProfile.IsLow(Screen.width));
    }

    private void Update()
    {
        bool _isLow = UiResolutionProfile.IsLow(Screen.width);
        if (_isLow != m_isLow)
            ApplyProfile(_isLow);
    }

    private void ApplyProfile(bool isLow)
    {
        m_isLow = isLow;
        if (m_canvasScaler != null && m_lowReferenceResolution.x > 0f && m_lowReferenceResolution.y > 0f)
            m_canvasScaler.referenceResolution = isLow ? m_lowReferenceResolution : m_authoredReferenceResolution;

        for (int _index = 0; _index < m_lowRects.Length; _index++)
        {
            LowRect _low = m_lowRects[_index];
            if (_low?.m_target == null)
                continue;
            RectTransform _target = _low.m_target;
            AuthoredRect _authored = m_authoredRects[_index];
            _target.anchorMin = isLow ? _low.m_anchorMin : _authored.m_anchorMin;
            _target.anchorMax = isLow ? _low.m_anchorMax : _authored.m_anchorMax;
            _target.pivot = isLow ? _low.m_pivot : _authored.m_pivot;
            _target.anchoredPosition = isLow ? _low.m_anchoredPosition : _authored.m_anchoredPosition;
            _target.sizeDelta = isLow ? _low.m_sizeDelta : _authored.m_sizeDelta;
            _target.localScale = isLow ? new Vector3(_low.m_scale, _low.m_scale, 1f) : _authored.m_localScale;
        }
    }
}
