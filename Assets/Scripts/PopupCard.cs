using UnityEngine;
using TMPro;

public class PopupCard : MonoBehaviour
{
    [Header("3D Text References")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;

    [Header("Audio (Optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip spawnSound;
    [SerializeField] private AudioClip closeSound;

    private System.Action onClosedCallback;
    private bool isClosing = false;

    public void Initialize(string title, string message, System.Action onClosed = null)
    {
        if (titleText != null) titleText.text = title;
        if (messageText != null) messageText.text = message;
        onClosedCallback = onClosed;

        if (audioSource != null && spawnSound != null)
        {
            audioSource.PlayOneShot(spawnSound);
        }
    }

    /// <summary>
    /// Self-destruct function called by the touch button.
    /// </summary>
    public void Close() => CloseInternal(true);

    /// <summary>
    /// Removes the popup without the close sound (used when the VS is switched).
    /// </summary>
    public void Dismiss() => CloseInternal(false);

    private void CloseInternal(bool playSound)
    {
        if (isClosing) return;
        isClosing = true;

        if (playSound && closeSound != null)
        {
            // Plays sound at point so it finishes even though the object is destroyed
            AudioSource.PlayClipAtPoint(closeSound, transform.position);
        }

        onClosedCallback?.Invoke();
        PopupManager.Instance?.UnregisterPopup(this);

        Destroy(gameObject);
    }
}