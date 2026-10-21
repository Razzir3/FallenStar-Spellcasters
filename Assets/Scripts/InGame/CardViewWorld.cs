using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;

public class CardViewWorld : CardViewBase
{
    #region Variable

    [SerializeField]
    private SpriteRenderer m_artwork;

    [SerializeField]
    private SpriteRenderer m_frame;

    [SerializeField]
    private GameObject m_front;

    [SerializeField]
    private GameObject m_back;

    [SerializeField]
    private SpriteRenderer m_backFrame;

    [SerializeField]
    private TMP_Text m_name;

    [SerializeField]
    private TMP_Text m_description;

    [SerializeField]
    private TMP_Text m_cost;

    [SerializeField]
    private Transform m_visualRoot;

    private NetworkCard m_networkCard;

    #endregion

    #region Unity Calling Func

    private void OnDestroy()
    {
        if (m_networkCard == null)
            return;

        m_networkCard.m_isRotated.OnValueChanged -=
            OnRotatedChanged;

        m_networkCard.m_isFaceUp.OnValueChanged -=
            OnFaceUpChanged;
    }

    #endregion

    #region Function

    public override void Setup(CardData data)
    {
        m_textData = data;
        if (data == null)
        {
            Debug.LogWarning(
                "CardViewWorld.Setup: CardData is null.");

            return;
        }

        m_artwork.sprite = data.m_artwork;
        m_frame.sprite = data.m_cardFrame;

        ApplyTexts(data);

        m_cost.text =
            data.m_cost.ToString();

        m_backFrame.sprite =
            data.m_cardBack;
    }

    protected override void ApplyTexts(CardData data)
    {
        m_name.text =
            LocalizationSettings.StringDatabase
                .GetLocalizedString(
                    "Cards",
                    data.m_nameKey);

        m_description.text =
            LocalizationSettings.StringDatabase
                .GetLocalizedString(
                    "Cards",
                    data.m_descKey);

    }

    public override void SetFaceUp(bool value)
    {
        m_front.SetActive(value);
        m_back.SetActive(!value);
    }

    public override void SetRotated(bool value)
    {
        if (m_visualRoot == null)
        {
            Debug.LogWarning(
                "CardViewWorld: m_visualRoot is null.");

            return;
        }

        m_visualRoot.localRotation =
            value
                ? Quaternion.Euler(0f, 0f, -90f)
                : Quaternion.identity;
    }

    public void SetNetworkCard(
        NetworkCard networkCard)
    {
        if (m_networkCard != null)
        {
            m_networkCard.m_isRotated.OnValueChanged -=
                OnRotatedChanged;

            m_networkCard.m_isFaceUp.OnValueChanged -=
                OnFaceUpChanged;
        }

        m_networkCard = networkCard;

        if (m_networkCard == null)
            return;

        m_networkCard.m_isRotated.OnValueChanged +=
            OnRotatedChanged;

        m_networkCard.m_isFaceUp.OnValueChanged +=
            OnFaceUpChanged;

        SetRotated(
            m_networkCard.m_isRotated.Value);

        SetFaceUp(
            m_networkCard.m_isFaceUp.Value);
    }

    private void OnRotatedChanged(
        bool previousValue,
        bool newValue)
    {
        SetRotated(newValue);
    }

    private void OnFaceUpChanged(
        bool previousValue,
        bool newValue)
    {
        SetFaceUp(newValue);
    }

    public NetworkCard GetNetworkCard()
    {
        return m_networkCard;
    }

    #endregion
}