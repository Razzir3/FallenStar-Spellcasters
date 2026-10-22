using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardPreviewController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas m_canvas;
    [SerializeField] private RectTransform m_previewRoot;
    [SerializeField] private CardViewUI m_cardPreviewPrefab;

    private CardViewUI m_currentPreview;
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

    public void TogglePreview(NetworkCard networkCard, Vector2 screenPosition)
    {
        if (m_currentPreview != null)
        {
            HidePreview();
            return;
        }

        ShowPreview(networkCard, screenPosition);
    }
    public void ShowPreview(NetworkCard networkCard, Vector2 screenPosition)
    {
        if (networkCard == null) { return; }
        if (!TryResolveTargets()) { return; }
        if (m_currentPreview != null && ShownCard == networkCard) { return; }
        HidePreview();

        if(networkCard.m_cardData == null)
            { return; }
        m_currentPreview = Instantiate(m_cardPreviewPrefab, m_dock != null ? m_dock : m_previewRoot);
        m_currentPreview.Setup(networkCard.m_cardData);
        m_currentPreview.SetFaceUp(true);
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

    public void HidePreview()
    {
        if (m_currentPreview != null)
        {
            m_currentPreview.gameObject.SetActive(false);
            Destroy(m_currentPreview.gameObject);
            m_currentPreview = null;
        }
        ShownCard = null;
    }

    private Camera GetCanvasCamera()
    {
        if (m_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;
        return m_canvas.worldCamera;
    }
}
