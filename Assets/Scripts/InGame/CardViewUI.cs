
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Settings;
using System;
using UnityEngine.EventSystems;

public class CardViewUI : CardViewBase, IPointerClickHandler
{
    [SerializeField] private Image m_artwork;
    [SerializeField] private Image m_frame;
    [SerializeField] private Image m_backFrame;

    [SerializeField] private TMP_Text m_name;
    [SerializeField] private TMP_Text m_description;
    [SerializeField] private TMP_Text m_cost;

    [SerializeField] private GameObject m_front;
    [SerializeField] private GameObject m_back;

    public bool m_debugMode;

    private bool m_isSelected = false;
    private bool m_alterSelect = false;
    private NetworkCard m_networkCard;

    public event Action<CardViewUI> Clicked;
    public event Action<CardViewUI> AlterClicked;
    public override void Setup(CardData data)
    {
        m_artwork.sprite = data.m_artwork;
        m_frame.sprite = data.m_cardFrame;

        m_name.text = LocalizationSettings.StringDatabase.GetLocalizedString("Cards", data.m_nameKey);
        m_description.text = LocalizationSettings.StringDatabase.GetLocalizedString("Cards", data.m_descKey);
        m_cost.text = data.m_cost.ToString();

        m_backFrame.sprite = data.m_cardBack;
    }

    public override void SetFaceUp(bool value = true)
    {
        m_front.SetActive(value);
        m_back.SetActive(!value);
    }

    public override void SetRotated(bool value)
    {
        transform.rotation = Quaternion.Euler(value ? 90 : 0, 0, 0);
    }

    public void SetSelected(bool value)
    {
        m_alterSelect = false;
        m_isSelected = value;
    }
    public void SetAlterSelect(bool value)
    {
        m_isSelected = false;
        m_alterSelect = value;
    }

    public void SetNetworkCard(NetworkCard networkCard)
    {
        m_networkCard = networkCard;
    }

    public NetworkCard GetNetworkCard()
    {
        return m_networkCard;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if(m_debugMode)Debug.Log("CardViewUI OnPointerClick called");

        if (m_networkCard == null)
        {
            Debug.LogWarning("Clicked CardViewUI has no NetworkCard.");
            return;
        }
        string _nameKey = m_networkCard.m_cardData != null ? m_networkCard.m_cardData.m_nameKey : "<no data>";
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            Debug.Log("Card clicked: " + _nameKey);

            Clicked?.Invoke(this);
        }
        if(eventData.button == PointerEventData.InputButton.Right)
        {
            Debug.Log("Card alter clicked " + _nameKey);
            AlterClicked?.Invoke(this);
        }
    }

    private void OnDestroy()
    {
        Clicked = null;
        AlterClicked = null;
    }

    public void SetSelectedColor()
    {
        m_artwork.color = Color.green;
        m_frame.color = Color.green;
        m_backFrame.color = Color.green;
    }
    public void SetAlterSelectedColor()
    {
        m_artwork.color = Color.red;
        m_frame.color = Color.red;
        m_backFrame.color = Color.red;
    }
    public void ClearSelectedColor()
    {
        m_artwork.color = Color.white;
        m_frame.color = Color.white;
        m_backFrame.color = Color.white;
    }
}
