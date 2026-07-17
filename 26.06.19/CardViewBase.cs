using UnityEngine;

public abstract class CardViewBase : MonoBehaviour
{
    public abstract void Setup(CardData data);

    public abstract void SetFaceUp(bool value);

    public abstract void SetRotated(bool value);
}