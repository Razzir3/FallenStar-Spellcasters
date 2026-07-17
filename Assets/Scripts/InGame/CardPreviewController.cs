using UnityEditor.Localization.Plugins.XLIFF.V20;
using UnityEngine;
using UnityEngine.EventSystems;

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
        m_canvas = FindFirstObjectByType<Canvas>();
        m_previewRoot = m_canvas.GetComponent<RectTransform>();
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

        _previewRect.anchoredPosition = _locakPoint;
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
