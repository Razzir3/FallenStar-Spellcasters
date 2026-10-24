using System.Collections;
using System.Globalization;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public enum CardViewType
{
    Play,
    Discard
}

public class PlayerUI : NetworkBehaviour
{
    #region Variable
    public static PlayerUI Instance;

    [SerializeField]
    private RectTransform m_handContainer;
    [SerializeField] private float m_handCardWidth = 200f;
    private HorizontalLayoutGroup m_handLayout;
    private float m_maxHandSpacing;
    private float m_lastHandWidth = -1f;
    [SerializeField] private RectTransform m_cardViewDock;
    public RectTransform CardViewDock => m_cardViewDock;
    private CardPreviewController m_cardPreviewController;
    private NetworkCard m_hoveredCard;

    public TMP_Text m_hpText;
    public TMP_Text m_lifeText;
    public TMP_Text m_manaText;
    public TMP_Text m_opponentManaText;
    public TMP_Text m_messageText;

    [SerializeField] private LocalizedString m_labelHp;
    [SerializeField] private LocalizedString m_labelLife;
    [SerializeField] private LocalizedString m_labelMana;
    [SerializeField] private LocalizedString m_labelOpponentMana;
    [SerializeField] private LocalizedString m_manaNotEnoughText;
    [SerializeField] private LocalizedString m_handLimitDiscardText;
    [SerializeField] private LocalizedString m_selectCardFirstText = new LocalizedString("UI_table", "notice_select_card_first");
    [SerializeField] private LocalizedString m_confirmTargetFirstText = new LocalizedString("UI_table", "notice_confirm_target_first");
    [SerializeField] private LocalizedString m_opponentTurnText = new LocalizedString("UI_table", "notice_opponent_turn");
    [SerializeField] private LocalizedString m_deckRefilledText = new LocalizedString("UI_table", "notice_deck_refilled");
    [SerializeField] private float m_messageDuration = 2f;
    [SerializeField] private LocalizedString m_playerDeadText;
    [SerializeField] private LocalizedString m_phaseAnouncementText;

    private int m_hp;
    private int m_life;
    private bool m_healthTextDirty = true;
    private bool m_isDeadTextShown;
    private int m_manaCurrent = -1;
    private int m_manaCapacity = -1;
    private int m_opponentManaCurrent = -1;
    private int m_opponentManaCapacity = -1;
    private float m_messageHideTime = -1f;
    private LocalizedString m_activeMessage;

    private PlayerSession m_playerSession;
    private bool m_isLocalUiCleared;
    
    private CardViewUI m_selectedCardView;
    private NetworkCard m_selectedNetworkCard;

    [SerializeField]
    private Transform m_cardContainer;

    [SerializeField]
    private CardViewUI m_cardPrefab;

    private Dictionary<ulong, CardViewUI> m_cardViews = new Dictionary<ulong, CardViewUI>();

    public bool m_debugMode = false;
    #endregion Variable
    private void Awake()
    {
        m_debugMode = DebugModeGuard.Allow(m_debugMode);
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (m_handContainer != null)
        {
            m_handLayout = m_handContainer.GetComponent<HorizontalLayoutGroup>();
            if (m_handLayout != null)
                m_maxHandSpacing = m_handLayout.spacing;
        }
    }

    public override void OnDestroy()
    {
        if (GameMaster.Instance != null)
        {
            GameMaster.Instance.m_gamePhase.OnValueChanged -= OnGamePhaseChanged;
            GameMaster.Instance.m_selectionPhaseId.OnValueChanged -= OnSelectionPhaseChanged;
        }

        if (Instance == this)
        {
            Instance = null;
        }

        base.OnDestroy();
    }

    // Update is called once per frame
    void Update()
    {
        if (m_handContainer != null && m_handContainer.rect.width != m_lastHandWidth)
            UpdateHandSpacing();

        if (m_messageHideTime >= 0f && Time.unscaledTime >= m_messageHideTime)
        {
            if (m_messageText != null)
                m_messageText.text = "";
            m_messageHideTime = -1f;
            m_activeMessage = null;
        }

        if ((GameMaster.Instance == null))
        {
            return;
        }
        if (SessionManager.Instance == null || SessionManager.Instance.LocalPlayer == null)
        {
            if (m_playerSession != null || !m_isLocalUiCleared)
                ClearLocalUI();
            return;
        }
        if (m_playerSession == null)
        {
            m_playerSession =
                SessionManager.Instance.LocalPlayer;

            if (m_playerSession == null)
            {
                if (!m_isLocalUiCleared)
                    ClearLocalUI();
                return;
            }

            m_isLocalUiCleared = false;

            m_playerSession.m_handCards.OnListChanged +=
                UpdateCards;

            m_playerSession.m_boardPlayCards.OnListChanged +=
                UpdateCards;

            m_playerSession.m_boardDiscardCards.OnListChanged +=
                UpdateCards;

            if (GameMaster.Instance != null)
            {
                GameMaster.Instance.m_gamePhase.OnValueChanged -=
                    OnGamePhaseChanged;

                GameMaster.Instance.m_gamePhase.OnValueChanged +=
                    OnGamePhaseChanged;

                GameMaster.Instance.m_selectionPhaseId.OnValueChanged -=
                    OnSelectionPhaseChanged;

                GameMaster.Instance.m_selectionPhaseId.OnValueChanged +=
                    OnSelectionPhaseChanged;
            }

            UpdateCards(default);
        }
        UpdateManaTexts();
        if (m_playerSession.m_lifeCurrent.Value<0)
        {
            if (m_healthTextDirty || !m_isDeadTextShown)
            {
                m_hpText.text = m_playerDeadText.GetLocalizedString();
                m_lifeText.text = ("");
                m_healthTextDirty = false;
                m_isDeadTextShown = true;
            }
            ClearSelectedCard();
            //m_playerSession = null;
        }
        else if (m_playerSession.CharacterObjectId.Value != 0&&m_playerSession.GetCharacter()!=null)
        {
            int _hp = m_playerSession.GetCharacter().m_hpCurrent.Value;
            int _life = m_playerSession.m_lifeCurrent.Value;
            if (m_healthTextDirty || m_isDeadTextShown || m_hp != _hp)
            {
                m_hp = _hp;
                m_hpText.text = m_labelHp.GetLocalizedString(m_hp);
            }
            if (m_healthTextDirty || m_isDeadTextShown || m_life != _life)
            {
                m_life = _life;
                m_lifeText.text = m_labelLife.GetLocalizedString(m_life);
            }
            m_healthTextDirty = false;
            m_isDeadTextShown = false;
        }
    }

    private void UpdateManaTexts()
    {
        int _current = m_playerSession.m_manaCurrent.Value;
        int _capacity = m_playerSession.m_manaCapacity.Value;
        if (m_manaText != null && (_current != m_manaCurrent || _capacity != m_manaCapacity))
        {
            m_manaText.text = m_labelMana.GetLocalizedString(_current, _capacity);
            m_manaCurrent = _current;
            m_manaCapacity = _capacity;
        }

        PlayerSession _opponent = null;
        if (GameMaster.Instance != null && NetworkManager.Singleton != null &&
            NetworkManager.Singleton.SpawnManager != null)
        {
            _opponent = GameMaster.Instance.GetSessionByID(m_playerSession.m_currentOponentNetworkObjectId.Value);
        }

        if (_opponent == null)
        {
            if (m_opponentManaText != null && m_opponentManaText.text.Length > 0)
                m_opponentManaText.text = "";
            m_opponentManaCurrent = m_opponentManaCapacity = -1;
            return;
        }

        _current = _opponent.m_manaCurrent.Value;
        _capacity = _opponent.m_manaCapacity.Value;
        if (m_opponentManaText != null &&
            (_current != m_opponentManaCurrent || _capacity != m_opponentManaCapacity))
        {
            m_opponentManaText.text = m_labelOpponentMana.GetLocalizedString(_current, _capacity);
            m_opponentManaCurrent = _current;
            m_opponentManaCapacity = _capacity;
        }
    }

    private void ResetManaTextCache()
    {
        m_healthTextDirty = true;
        m_manaCurrent = m_manaCapacity = -1;
        m_opponentManaCurrent = m_opponentManaCapacity = -1;
    }

    private void OnSelectedLocaleChanged(Locale locale)
    {
        ResetManaTextCache();
        if (m_messageHideTime > Time.unscaledTime && m_messageText != null && m_activeMessage != null)
            m_messageText.text = m_activeMessage.GetLocalizedString();
    }

    public void ShowManaNotEnough()
    {
        ShowMessage(m_manaNotEnoughText);
    }

    public void ShowHandLimitDiscard()
    {
        ShowMessage(m_handLimitDiscardText);
    }

    public void ShowSelectCardFirst() => ShowMessage(m_selectCardFirstText);
    public void ShowConfirmTargetFirst() => ShowMessage(m_confirmTargetFirstText);
    public void ShowOpponentTurn() => ShowMessage(m_opponentTurnText);
    public void ShowDeckRefilled() => ShowMessage(m_deckRefilledText);

    private void ShowMessage(LocalizedString message)
    {
        if (m_messageText == null)
            return;

        m_activeMessage = message;
        m_messageText.text = message.GetLocalizedString();
        m_messageHideTime = Time.unscaledTime + Mathf.Max(0f, m_messageDuration);
    }
    private void OnSelectionPhaseChanged(int previous, int current)
    {
        if (m_playerSession != null)
        {
            m_playerSession.ClearLocalCardSelection();
            UpdateCards(default);
        }
    }

    private void OnGamePhaseChanged(
    GamePhase previousValue,
    GamePhase newValue)
    {
        UpdateCards(default);
    }

    private void UpdateCards(
        NetworkListEvent<ulong> changeEvent)
    {
        ClearHandUI();

        if (m_playerSession == null)
            return;

        m_playerSession.PruneLocalCardSelection();

        if (GameMaster.Instance == null)
            return;

        switch (GameMaster.Instance.m_gamePhase.Value)
        {
            case GamePhase.GameOver:
                break;

            case GamePhase.None:
            case GamePhase.DrawCards:
            case GamePhase.RoundBreak:
            case GamePhase.RoundEnd:
            case GamePhase.CardSelection:
                {
                    foreach (NetworkCard card
                             in m_playerSession.GetHandCards())
                    {
                        CreateCardView(card);
                    }

                    break;
                }

            case GamePhase.PlayerAction:
            case GamePhase.ResolveCards:
                {
                    foreach (NetworkCard card
                             in m_playerSession.GetBoardPlayCards())
                    {
                        CreateCardView(card);
                    }

                    break;
                }
        }

        ValidateSelectedCard();
        UpdateHandSpacing();
        ValidateHoveredCard();
    }

    private void UpdateHandSpacing()
    {
        if (m_handLayout == null || m_handContainer == null)
            return;

        m_lastHandWidth = m_handContainer.rect.width;

        int _count = m_cardViews.Count;
        float _childWidth = 0f;
        foreach (CardViewUI _view in m_cardViews.Values)
        {
            if (_view == null)
                continue;
            _childWidth = LayoutUtility.GetPreferredWidth(_view.transform as RectTransform);
            break;
        }

        float _step = m_maxHandSpacing + _childWidth;
        if (_count >= 2 && (_count - 1) * _step + m_handCardWidth > m_lastHandWidth)
            _step = (m_lastHandWidth - m_handCardWidth) / (_count - 1);

        m_handLayout.spacing = _step - _childWidth;
    }

    public void OnCardDataReceived(ulong cardNetworkObjectId)
    {
        if (m_playerSession == null)
            return;

        if (m_cardViews.ContainsKey(cardNetworkObjectId))
            return;

        if (!m_playerSession.m_handCards.Contains(cardNetworkObjectId) &&
            !m_playerSession.m_boardPlayCards.Contains(cardNetworkObjectId))
            return;

        UpdateCards(default);
    }

    private void CreateCardView(
    NetworkCard card)
    {
        if (card == null)
            return;

        if (card.m_cardData == null)
            return;

        CardViewUI _view =
            Instantiate(
                m_cardPrefab,
                m_cardContainer);

        _view.Setup(card.m_cardData);
        _view.SetNetworkCard(card);

        if (GameMaster.Instance != null && m_playerSession != null &&
            GameMaster.Instance.m_gamePhase.Value == GamePhase.CardSelection)
        {
            if (m_playerSession.IsCardSelectedToPlay(card))
                _view.SetSelectedColor();
            else if (m_playerSession.IsCardSelectedToDiscard(card))
                _view.SetAlterSelectedColor();
        }

        _view.Clicked -= OnCardClicked;
        _view.Clicked += OnCardClicked;

        _view.AlterClicked -= OnCardAlterClicked;
        _view.AlterClicked += OnCardAlterClicked;

        _view.PointerEntered -= OnCardPointerEntered;
        _view.PointerEntered += OnCardPointerEntered;
        _view.PointerExited -= OnCardPointerExited;
        _view.PointerExited += OnCardPointerExited;

        m_cardViews[card.NetworkObjectId] =
            _view;

        card.SetCurrentView(_view);
    }

    private CardPreviewController GetCardPreviewController()
    {
        if (m_cardPreviewController == null && m_playerSession != null)
            m_cardPreviewController = m_playerSession.GetComponent<CardPreviewController>();
        return m_cardPreviewController;
    }

    private void OnCardPointerEntered(CardViewUI view)
    {
        NetworkCard _card = view.GetNetworkCard();
        CardPreviewController _controller = GetCardPreviewController();
        if (_card == null || _controller == null)
            return;
        m_hoveredCard = _card;
        Vector2 _position = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        _controller.ShowPreview(_card, _position);
    }

    private void OnCardPointerExited(CardViewUI view)
    {
        NetworkCard _card = view.GetNetworkCard();
        CardPreviewController _controller = GetCardPreviewController();
        if (_controller != null && _controller.ShownCard == _card)
            _controller.HidePreview();
        if (m_hoveredCard == _card)
            m_hoveredCard = null;
    }

    private void ValidateHoveredCard()
    {
        if (ReferenceEquals(m_hoveredCard, null))
            return;
        if (m_hoveredCard != null && m_cardViews.ContainsKey(m_hoveredCard.NetworkObjectId))
            return;
        CardPreviewController _controller = GetCardPreviewController();
        if (_controller != null && _controller.ShownCard == m_hoveredCard)
            _controller.HidePreview();
        m_hoveredCard = null;
    }

    public void OnCardClicked(CardViewUI cardView)
    {
        if (cardView == null)
            return;

        if (m_debugMode)
            Debug.Log("Card clicked: " + cardView.name);

        NetworkCard networkCard = cardView.GetNetworkCard();

        if (networkCard == null)
        {
            Debug.LogWarning("Clicked CardViewUI has no NetworkCard.");
            return;
        }

        if (m_selectedNetworkCard == networkCard)
        {
            networkCard.m_isSelected = false;
            ClearSelectedCard();
            if (m_debugMode)
                Debug.Log("Card deselected");
            return;
        }

        ClearSelectedCard();
        if (GameMaster.Instance.m_gamePhase.Value != GamePhase.CardSelection)
        {
            networkCard.m_isSelected = true;

            SelectCard(cardView, networkCard);
        }
        else
        {
            SelectCardToPlay(cardView, networkCard);
        }
    }
    //todo
    public void OnCardAlterClicked(CardViewUI cardView)
    {
        if (cardView == null)
            return;

        if (m_debugMode)
            Debug.Log("Card Alter clicked: " + cardView.name);

        NetworkCard networkCard = cardView.GetNetworkCard();

        if (networkCard == null)
        {
            Debug.LogWarning("Clicked CardViewUI has no NetworkCard.");
            return;
        }

        if (m_selectedNetworkCard == networkCard)
        {
            networkCard.m_isSelected = false;
            ClearSelectedCard();
            if (m_debugMode)
                Debug.Log("Card deselected");
            return;
        }

        ClearSelectedCard();

        if (GameMaster.Instance.m_gamePhase.Value != GamePhase.CardSelection)
        {
            networkCard.m_isSelected = true;

            SelectCard(cardView, networkCard);
        }
        else
        {
            SelectCardToDiscard(cardView, networkCard);
        }
    }

    private void SelectCard(CardViewUI cardView, NetworkCard networkCard)
    {
        m_selectedCardView = cardView;
        m_selectedNetworkCard = networkCard;
        
        m_selectedCardView.SetSelectedColor();

        m_playerSession.SetSelectedCard(networkCard);

        cardView.SetSelected(true);
    }
    private void SelectCardToPlay(CardViewUI cardView, NetworkCard networkCard)
    {
        if (cardView == null) return;
        if (networkCard == null) return;

        if(m_playerSession.SelectCardToPlay(networkCard))
            cardView.SetSelectedColor();
        else cardView.ClearSelectedColor();
    }
    private void SelectCardToDiscard(CardViewUI cardView, NetworkCard networkCard)
    {
        if (cardView == null) return;
        if (networkCard == null) return;

        if(m_playerSession.SelectCardToDiscard(networkCard))
            cardView.SetAlterSelectedColor();
        else cardView.ClearSelectedColor();
    }

    public void OnSelectionRejected()
    {
        ClearSelectedCard();

        foreach (CardViewUI cardView in m_cardViews.Values)
        {
            if (cardView != null)
            {
                cardView.ClearSelectedColor();
            }
        }

        ShowManaNotEnough();
    }

    public void ClearSelectedCard()
    {
        if (m_selectedNetworkCard != null)
        {
            m_selectedNetworkCard.m_isSelected = false;
        }
        if (m_selectedCardView != null)
        {
            m_selectedCardView.SetSelected(false);
            m_selectedCardView.ClearSelectedColor();
        }
        m_selectedCardView = null;
        m_selectedNetworkCard = null;

        if (m_playerSession != null)
        {
            m_playerSession.ClearSelectedCard();
        }
    }

    private void ValidateSelectedCard()
    {
        if (m_selectedNetworkCard == null)
            return;
        if(!m_cardViews.ContainsKey(m_selectedNetworkCard.NetworkObjectId))
        {
            ClearSelectedCard();
        }
    }

    public void ClearHandUI()
    {
        if(m_debugMode)Debug.Log("Clearing hand UI");
        foreach (var pair in m_cardViews)
        {
            pair.Value.Clicked -= OnCardClicked;
            pair.Value.AlterClicked -= OnCardAlterClicked;
            pair.Value.PointerEntered -= OnCardPointerEntered;
            pair.Value.PointerExited -= OnCardPointerExited;
            if (pair.Value != null)
            {
                pair.Value.gameObject.SetActive(false);
                Destroy(pair.Value.gameObject);
            }
            
        }

        m_cardViews.Clear();

        if (m_cardContainer != null)
        {
            for (int i = m_cardContainer.childCount - 1; i >= 0; i--)
            {
                m_cardContainer.GetChild(i).gameObject.SetActive(false);
                Destroy(m_cardContainer.GetChild(i).gameObject);
            }
        }
    }
    private void OnEnable()
    {
        ResetManaTextCache();
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.OnServerStopped += OnServerStopped;
        }
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnServerStopped -= OnServerStopped;
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
        {
            ClearLocalUI();
            return;
        }

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            ClearLocalUI();
        }
    }

    private void OnServerStopped(bool obj)
    {
        ClearLocalUI();
    }

    public void ClearLocalUI()
    {
        m_isLocalUiCleared = true;
        CardPreviewController _controller = GetCardPreviewController();
        if (_controller != null)
            _controller.HidePreview();
        m_hoveredCard = null;
        m_cardPreviewController = null;
        if (m_playerSession != null)
        {
            m_playerSession.m_handCards.OnListChanged -=
                UpdateCards;

            m_playerSession.m_boardPlayCards.OnListChanged -=
                UpdateCards;

            m_playerSession.m_boardDiscardCards.OnListChanged -=
                UpdateCards;
        }

        if (GameMaster.Instance != null)
        {
            GameMaster.Instance.m_gamePhase.OnValueChanged -=
                OnGamePhaseChanged;

            GameMaster.Instance.m_selectionPhaseId.OnValueChanged -=
                OnSelectionPhaseChanged;
        }

        m_playerSession = null;

        if (m_hpText != null)
            m_hpText.text = "";

        if (m_lifeText != null)
            m_lifeText.text = "";

        if (m_manaText != null)
            m_manaText.text = "";
        if (m_opponentManaText != null)
            m_opponentManaText.text = "";
        if (m_messageText != null)
            m_messageText.text = "";
        ResetManaTextCache();
        m_messageHideTime = -1f;
        m_activeMessage = null;

        m_selectedNetworkCard = null;
        m_selectedCardView = null;

        ClearHandUI();
        UpdateHandSpacing();
    }
    public void ConfirmTurn()
    {
        PlayerSession _session = m_playerSession;
        if (_session == null && SessionManager.Instance != null)
            _session = SessionManager.Instance.LocalPlayer;

        if (_session == null)
        {
            Debug.LogWarning( "ConfirmTurn failed: local PlayerSession is null.");

            return;
        }

        _session.ConfirmTurn();
    }
}
