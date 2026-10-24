using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardPreviewController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas m_canvas;
    [SerializeField] private RectTransform m_previewRoot;
    [SerializeField] private CardViewUI m_cardPreviewPrefab;

    [Header("Fade (F9)")]
    [SerializeField, Min(0f)] private float m_fadeInDuration = 0.7f;
    [SerializeField, Min(0f)] private float m_fadeOutDuration = 0.6f;

    private CardViewUI m_currentPreview;
    private CanvasGroup m_currentGroup;
    private CardViewUI m_fadingPreview;
    private CanvasGroup m_fadingGroup;
    private NetworkCard m_fadingCard;
    private float m_fadeInElapsed;
    private float m_fadeInStartAlpha;
    private bool m_isFadingIn;
    private float m_fadeOutElapsed;
    private float m_fadeOutStartAlpha;
    private RectTransform m_dock;
    private Vector2 m_lastDockSize;
    private bool m_warnedMissingTargets;

    public bool HasPreview =>m_currentPreview != null;
    public NetworkCard ShownCard { get; private set; }

    private bool TryResolveTargets()
    {
        if (m_dock == null && PlayerUI.Instance != null)
            m_dock = PlayerUI.Instance.CardViewDock;

        if (m_canvas == null && PlayerUI.Instance != null)
        {
            Canvas _hudCanvas = PlayerUI.Instance.GetComponentInParent<Canvas>();
            if (_hudCanvas != null)
                m_canvas = _hudCanvas.rootCanvas;
        }

        if (m_canvas == null)
        {
            foreach (Canvas _canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (_canvas.isRootCanvas && _canvas.renderMode != RenderMode.WorldSpace)
                {
                    m_canvas = _canvas;
                    break;
                }
            }
        }

        if (m_previewRoot == null && m_canvas != null)
            m_previewRoot = m_canvas.transform as RectTransform;

        if (m_canvas != null && m_previewRoot != null)
            return true;

        if (!m_warnedMissingTargets)
        {
            m_warnedMissingTargets = true;
            Debug.LogWarning("CardPreviewController: no screen-space canvas found; previews are disabled.");
        }
        return false;
    }


    public void ShowPreview(NetworkCard networkCard, Vector2 screenPosition)
    {
        if (networkCard == null) { return; }
        if (!TryResolveTargets()) { return; }
        if (m_currentPreview != null && ShownCard == networkCard) { return; }

        CardViewUI _reusedPreview = null;
        CanvasGroup _reusedGroup = null;
        if (networkCard.m_cardData != null && m_fadingPreview != null && m_fadingCard == networkCard)
        {
            _reusedPreview = m_fadingPreview;
            _reusedGroup = m_fadingGroup;
            m_fadingPreview = null;
            m_fadingGroup = null;
            m_fadingCard = null;
        }
        HidePreview();

        if(networkCard.m_cardData == null)
            { return; }
        m_currentPreview = _reusedPreview != null ? _reusedPreview :
            Instantiate(m_cardPreviewPrefab, m_dock != null ? m_dock : m_previewRoot);
        if (_reusedPreview == null)
        {
            m_currentPreview.Setup(networkCard.m_cardData);
            m_currentPreview.SetFaceUp(true);
            m_currentGroup = m_currentPreview.GetComponent<CanvasGroup>();
            if (m_currentGroup == null)
                m_currentGroup = m_currentPreview.gameObject.AddComponent<CanvasGroup>();
            m_currentGroup.alpha = 0f;
        }
        else
        {
            m_currentGroup = _reusedGroup;
        }
        m_fadeInStartAlpha = m_currentGroup.alpha;
        m_fadeInElapsed = 0f;
        m_isFadingIn = m_fadeInDuration > 0f && m_currentGroup.alpha < 1f;
        if (m_fadeInDuration <= 0f)
            m_currentGroup.alpha = 1f;
        ShownCard = networkCard;

        RectTransform _previewRect = m_currentPreview.GetComponent<RectTransform>();

        if (m_dock != null)
        {
            _previewRect.anchorMin = _previewRect.anchorMax = _previewRect.pivot = new Vector2(0f, .5f);
            _previewRect.anchoredPosition = Vector2.zero;
            FitDock();
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(m_previewRoot, screenPosition, GetCanvasCamera(), out Vector2 _locakPoint);

        LayoutRebuilder.ForceRebuildLayoutImmediate(_previewRect);
        Vector2 _size = Vector2.Scale(_previewRect.rect.size, _previewRect.localScale);
        Rect _bounds = m_previewRoot.rect;
        _locakPoint.x = _size.x > _bounds.width ? _bounds.center.x :
            Mathf.Clamp(_locakPoint.x, _bounds.xMin + _size.x * _previewRect.pivot.x,
                _bounds.xMax - _size.x * (1f - _previewRect.pivot.x));
        _locakPoint.y = _size.y > _bounds.height ? _bounds.center.y :
            Mathf.Clamp(_locakPoint.y, _bounds.yMin + _size.y * _previewRect.pivot.y,
                _bounds.yMax - _size.y * (1f - _previewRect.pivot.y));

        _previewRect.localPosition = new Vector3(_locakPoint.x, _locakPoint.y, 0f);
    }

    private void LateUpdate()
    {
        if (m_currentPreview != null && m_dock != null && m_lastDockSize != m_dock.rect.size)
            FitDock();

        if (m_currentPreview != null && m_isFadingIn)
        {
            m_fadeInElapsed += Time.unscaledDeltaTime;
            float _progress = m_fadeInDuration <= 0f ? 1f : Mathf.Clamp01(m_fadeInElapsed / m_fadeInDuration);
            m_currentGroup.alpha = Mathf.Lerp(m_fadeInStartAlpha, 1f, _progress);
            m_isFadingIn = _progress < 1f;
        }
        if (m_fadingPreview != null)
        {
            m_fadeOutElapsed += Time.unscaledDeltaTime;
            float _progress = m_fadeOutDuration <= 0f ? 1f : Mathf.Clamp01(m_fadeOutElapsed / m_fadeOutDuration);
            m_fadingGroup.alpha = Mathf.Lerp(m_fadeOutStartAlpha, 0f, _progress);
            if (_progress >= 1f)
                DestroyFadingPreview();
        }
    }

    private void FitDock()
    {
        RectTransform _view = m_currentPreview.GetComponent<RectTransform>();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_view);
        m_lastDockSize = m_dock.rect.size;
        if (_view.rect.width > 0f && _view.rect.height > 0f)
        {
            float _scale = Mathf.Min(m_lastDockSize.y / _view.rect.height, m_lastDockSize.x / _view.rect.width);
            _view.localScale = Vector3.one * _scale;
        }
    }

    public void HidePreview() => HidePreview(false);

    public void HidePreview(bool immediate)
    {
        if (immediate)
        {
            DestroyPreview(m_currentPreview);
            DestroyFadingPreview();
        }
        else if (m_currentPreview != null)
        {
            DestroyFadingPreview();
            m_fadingPreview = m_currentPreview;
            m_fadingGroup = m_currentGroup;
            m_fadingCard = ShownCard;
            m_fadeOutStartAlpha = m_fadingGroup.alpha;
            m_fadeOutElapsed = 0f;
            if (m_fadeOutDuration <= 0f)
            {
                m_fadingGroup.alpha = 0f;
                DestroyFadingPreview();
            }
        }
        m_currentPreview = null;
        m_currentGroup = null;
        m_isFadingIn = false;
        ShownCard = null;
    }

    private void DestroyFadingPreview()
    {
        DestroyPreview(m_fadingPreview);
        m_fadingPreview = null;
        m_fadingGroup = null;
        m_fadingCard = null;
    }

    private void DestroyPreview(CardViewUI preview)
    {
        if (preview == null)
            return;
        preview.gameObject.SetActive(false);
        preview.transform.SetParent(null, false);
        Destroy(preview.gameObject);
    }

    private void OnDisable() => HidePreview(true);
    private void OnDestroy() => HidePreview(true);

    private Camera GetCanvasCamera()
    {
        if (m_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;
        return m_canvas.worldCamera;
    }
}
