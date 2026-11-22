using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Shelf : MonoBehaviour
{
    [Header("Accepted Colours")]
    [Tooltip("Shelf 1: Green + Blue | Shelf 2: Red")]
    public List<BookColor> acceptedColors = new List<BookColor>();

    [Header("Auto Slot Generation")]
    [Tooltip("Middle anchor of the shelf. If empty, uses this shelf's transform.")]
    [SerializeField] private Transform centerPoint;

    [Tooltip("Distance between book centers (in meters). e.g. 0.08 = 8cm.")]
    [SerializeField] private float slotSpacing = 0.08f;

    public enum SpacingDirection { LeftToRight, FrontToBack }
    [SerializeField] private SpacingDirection spacingDirection = SpacingDirection.LeftToRight;

    [Header("Book Orientation (Relative to Shelf)")]
    [Tooltip("Rotation relative to this shelf. Tweak this until the cyan preview stands books upright on the shelf.")]
    [SerializeField] private Vector3 bookRotationEuler = new Vector3(0f, 90f, 0f);

    [Header("Rejection & Haptics")]
    [Range(0f, 1f)][SerializeField] private float hapticAmplitude = 0.8f;
    [Range(0f, 1f)][SerializeField] private float hapticFrequency = 0.8f;
    [SerializeField] private float hapticDuration = 0.2f;

    [Header("Audio (Optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip rejectSound;

    [Header("Highlight (VS2: lights up while a matching book is held)")]
    [Tooltip("Renderer that gets tinted. Auto-found on this object or its children if left empty.")]
    [SerializeField] private Renderer highlightRenderer;
    [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.3f);

    [Header("State (Auto-Managed)")]
    [SerializeField] private int slotCount = 0;

    private struct SlotTransform
    {
        public Vector3 position;
        public Quaternion rotation;
    }
    private readonly List<SlotTransform> generatedSlots = new List<SlotTransform>();
    private readonly List<Book> placedBooks = new List<Book>();
    private float lastRejectTime;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP / Lit
    private static readonly int ColorId = Shader.PropertyToID("_Color");         // Standard / Built-in
    private MaterialPropertyBlock highlightBlock;

    public bool IsFull => placedBooks.Count >= slotCount;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        if (centerPoint == null) centerPoint = transform;
        if (highlightRenderer == null) highlightRenderer = GetComponentInChildren<Renderer>();
    }

    /// <summary>
    /// Empties the shelf for a new run. The books themselves are moved back by Book.ResetToStart().
    /// </summary>
    public void ClearBooks()
    {
        placedBooks.Clear();
        SetHighlight(false);
    }

    /// <summary>
    /// Tints the shelf (on) or gives it back its normal material colour (off).
    /// </summary>
    public void SetHighlight(bool on)
    {
        if (highlightRenderer == null) return;

        if (!on)
        {
            highlightRenderer.SetPropertyBlock(null);
            return;
        }

        if (highlightBlock == null) highlightBlock = new MaterialPropertyBlock();
        highlightBlock.SetColor(BaseColorId, highlightColor);
        highlightBlock.SetColor(ColorId, highlightColor);
        highlightRenderer.SetPropertyBlock(highlightBlock);
    }

    public void InitializeSlots(int count)
    {
        slotCount = count;
        CalculateSlots();
    }

    /// <summary>
    /// Calculates slot positions & rotations strictly relative to the shelf's orientation.
    /// Does NOT create child objects under the shelf to avoid scale distortion.
    /// </summary>
    private void CalculateSlots()
    {
        generatedSlots.Clear();
        if (slotCount <= 0) return;

        Vector3 worldDir = spacingDirection == SpacingDirection.LeftToRight
            ? centerPoint.right
            : centerPoint.forward;

        for (int i = 0; i < slotCount; i++)
        {
            float offset = (i - (slotCount - 1) * 0.5f) * slotSpacing;

            SlotTransform slot;
            slot.position = centerPoint.position + (worldDir * offset);
            slot.rotation = centerPoint.rotation * Quaternion.Euler(bookRotationEuler);

            generatedSlots.Add(slot);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsFull) return;

        if (other.TryGetComponent<Book>(out Book book))
        {
            if (book.IsShelved) return;

            if (acceptedColors.Contains(book.bookColor))
            {
                // Ask the task first: VS1 can interrupt the attempt with a pop-up, and while a pop-up is
                // open nothing can be shelved. A blocked book stays in the hand; to try again the player
                // takes it out of the shelf zone and puts it back in.
                if (TaskManager.Instance != null && !TaskManager.Instance.CanShelve(book, this)) return;
                AcceptBook(book);
            }
            else
            {
                if (Time.time - lastRejectTime > 0.4f)
                {
                    lastRejectTime = Time.time;
                    RejectBook(book);
                }
            }
        }
    }

    private void AcceptBook(Book book)
    {
        int slotIndex = placedBooks.Count;
        SlotTransform targetSlot = generatedSlots[slotIndex];

        placedBooks.Add(book);

        book.PlaceOnShelf(null);

        book.transform.SetPositionAndRotation(targetSlot.position, targetSlot.rotation);

        TaskManager.Instance?.NotifyBookPlaced(book, this);
    }

    private void RejectBook(Book book)
    {
        StartCoroutine(TriggerHapticFeedback());

        if (audioSource != null && rejectSound != null)
        {
            audioSource.PlayOneShot(rejectSound);
        }

        TaskManager.Instance?.NotifyBookRejected(book, this);
    }

    private IEnumerator TriggerHapticFeedback()
    {
        OVRInput.SetControllerVibration(hapticFrequency, hapticAmplitude, OVRInput.Controller.RTouch);
        OVRInput.SetControllerVibration(hapticFrequency, hapticAmplitude, OVRInput.Controller.LTouch);

        yield return new WaitForSeconds(hapticDuration);

        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
    }

}
