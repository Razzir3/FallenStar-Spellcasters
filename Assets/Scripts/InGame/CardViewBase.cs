using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public abstract class CardViewBase : MonoBehaviour
{
    protected CardData m_textData;

    protected virtual void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    protected virtual void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnLocaleChanged(Locale _)
    {
        if (m_textData != null)
            ApplyTexts(m_textData);
    }

    protected abstract void ApplyTexts(CardData data);

    public abstract void Setup(CardData data);

    public abstract void SetFaceUp(bool value);

    public abstract void SetRotated(bool value);
}