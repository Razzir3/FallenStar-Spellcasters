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

    public bool HasPreview =>m_currentPreview != null;

    public void Start()
    {
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

        if (m_canvas != null && m_previewRoot == null)
            m_previewRoot = m_canvas.transform as RectTransform;

        if (m_canvas == null || m_previewRoot == null)
            Debug.LogWarning("CardPreviewController: no screen-space canvas found; previews are disabled.");
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
        if (m_canvas == null || m_previewRoot == null) { return; }
        Debug.Log("Tried ShowPrew");
        HidePreview();

        if(networkCard.m_cardData == null)
            { return; }
        Debug.Log("Tried ShowPrew");
        m_currentPreview = Instantiate(m_cardPreviewPrefab, m_previewRoot);
        m_currentPreview.Setup(networkCard.m_cardData);
        m_currentPreview.SetFaceUp(true);

        RectTransform _previewRect = m_currentPreview.GetComponent<RectTransform>();

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

    public void HidePreview()
    {
        if (m_currentPreview != null)
        {
            Destroy(m_currentPreview.gameObject);
            m_currentPreview = null;
        }
    }

    private Camera GetCanvasCamera()
    {
        if (m_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;
        return m_canvas.worldCamera;
    }
}
