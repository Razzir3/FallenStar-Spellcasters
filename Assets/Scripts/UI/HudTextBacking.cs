using TMPro;
using UnityEngine;
using UnityEngine.UI;

// F14a: a dark box behind one HUD text, following its rendered bounds.
[RequireComponent(typeof(Image))]
public class HudTextBacking : MonoBehaviour
{
    [SerializeField] private TMP_Text m_target;
    [SerializeField] private Vector2 m_padding = new Vector2(16f, 6f);

    private Image m_image;
    private RectTransform m_rect;
    private bool m_hasPlacement;
    private Bounds m_lastBounds;
    private Vector3 m_lastPosition;
    private Vector3 m_lastScale;
    private Quaternion m_lastRotation;
    private Vector2 m_lastPadding;

    private void Awake()
    {
        m_image = GetComponent<Image>();
        m_rect = GetComponent<RectTransform>();
        m_image.raycastTarget = false;
        m_rect.anchorMin = m_rect.anchorMax = m_rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private void LateUpdate()
    {
        bool _visible = m_target != null && m_target.isActiveAndEnabled &&
            !string.IsNullOrWhiteSpace(m_target.text);
        m_image.enabled = _visible;
        if (!_visible)
        {
            m_hasPlacement = false;
            return;
        }

        Bounds _bounds = m_target.textBounds;
        RectTransform _target = m_target.rectTransform;
        Vector3 _position = _target.position;
        Vector3 _scale = _target.lossyScale;
        Quaternion _rotation = _target.rotation;
        if (m_hasPlacement && _bounds == m_lastBounds && _position == m_lastPosition &&
            _scale == m_lastScale && _rotation == m_lastRotation && m_padding == m_lastPadding)
            return;

        m_rect.position = _target.TransformPoint(_bounds.center);
        m_rect.localScale = _target.localScale;
        m_rect.localRotation = _target.localRotation;
        m_rect.sizeDelta = (Vector2)_bounds.size + 2f * m_padding;
        m_lastBounds = _bounds;
        m_lastPosition = _position;
        m_lastScale = _scale;
        m_lastRotation = _rotation;
        m_lastPadding = m_padding;
        m_hasPlacement = true;
    }
}
