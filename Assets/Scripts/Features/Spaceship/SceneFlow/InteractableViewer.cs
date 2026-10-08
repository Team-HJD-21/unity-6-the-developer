using TMPro;
using UnityEngine;

public abstract class InteractableViewer : MonoBehaviour
{
    [SerializeField] protected TextMeshPro stateText;

    private bool isNearby;

    protected abstract string ActionText { get; }

    protected virtual void Start()
    {
        Refresh();
    }

    public void ShowPrompt(bool visible)
    {
        isNearby = visible;
        Refresh();
    }

    protected void ShowState(string state)
    {
        if (stateText != null)
        {
            stateText.text = isNearby ? $"{state}\n[E] {ActionText}" : state;
        }
    }

    public abstract void Interact();
    public abstract void Refresh();
}
