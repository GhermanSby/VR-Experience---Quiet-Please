using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TouchCloseButton : MonoBehaviour
{
    [Header("Visual Feedback")]
    [SerializeField] private Renderer buttonRenderer;
    [SerializeField] private Color normalColor = new Color(0.8f, 0.2f, 0.2f); // Red
    [SerializeField] private Color touchedColor = Color.green;

    private PopupCard parentCard;
    private bool wasPressed = false;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        parentCard = GetComponentInParent<PopupCard>();

        if (buttonRenderer == null) buttonRenderer = GetComponent<Renderer>();
        if (buttonRenderer != null) buttonRenderer.material.color = normalColor;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (wasPressed) return;
        wasPressed = true;

        if (buttonRenderer != null)
            buttonRenderer.material.color = touchedColor;

        // Triggers the popup's self-destruction
        if (parentCard != null)
        {
            parentCard.Close();
        }
    }
}