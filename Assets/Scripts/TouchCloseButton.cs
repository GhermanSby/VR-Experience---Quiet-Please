using Oculus.Interaction.Input;
using QuietPlease;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TouchCloseButton : MonoBehaviour
{
    [Header("Visual Feedback")]
    [SerializeField] private Renderer buttonRenderer;
    [SerializeField] private Color normalColor = new Color(0.8f, 0.2f, 0.2f); // Red
    [SerializeField] private Color touchedColor = Color.green;

    private PopupCard parentCard;
    private Collider buttonCollider;
    private bool wasPressed = false;

    private void Awake()
    {
        buttonCollider = GetComponent<Collider>();
        buttonCollider.isTrigger = true;
        parentCard = GetComponentInParent<PopupCard>();

        if (buttonRenderer == null) buttonRenderer = GetComponent<Renderer>();
        if (buttonRenderer != null) buttonRenderer.material.color = normalColor;
    }

    // Pressed by the VIRTUAL index fingertip (GameManager's hand mapping). In VS1 that fingertip
    // overshoots with the virtual hand, which is what makes this small button hard to hit.
    private void Update()
    {
        if (wasPressed) return;

        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        if (IsTouchedByFingertip(gm.handMapping, Handedness.Left) ||
            IsTouchedByFingertip(gm.handMapping, Handedness.Right))
        {
            Press();
        }
    }

    private bool IsTouchedByFingertip(HandMapping hands, Handedness side)
    {
        if (!buttonCollider.enabled || !hands.TryGetFingertip(side, out Vector3 tip)) return false;

        float radius = hands.fingertipTouchRadius;
        return (buttonCollider.ClosestPoint(tip) - tip).sqrMagnitude <= radius * radius;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (wasPressed) return;

        // Pop-ups appear right where the player holds a book (in front of the shelf).
        // Only the hand may close one, never a book passing through the button.
        if (other.GetComponentInParent<Book>() != null) return;

        Press();
    }

    private void Press()
    {
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