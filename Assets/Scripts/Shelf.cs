using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Shelf : MonoBehaviour
{
    [Header("Accepted Colours")]
    [Tooltip("Shelf 1: Green + Blue | Shelf 2: Red")]
    public List<BookColor> acceptedColors = new List<BookColor>();

    [Header("Auto Slot Generation")]
    [Tooltip("Anchor for the middle of the shelf. If empty, uses this shelf's transform.")]
    [SerializeField] private Transform centerPoint;

    [Tooltip("Number of books this shelf can hold.")]
    [Min(1)]
    [SerializeField] private int slotCount = 2;

    [Tooltip("Distance between book centers (in meters). e.g. 0.06 = 6cm.")]
    [SerializeField] private float slotSpacing = 0.06f;

    [Tooltip("Local axis to space books along. Usually (1, 0, 0) for Left-to-Right.")]
    [SerializeField] private Vector3 spacingAxis = Vector3.right;

    [Header("Book Orientation")]
    [Tooltip("Local rotation applied to books when placed (in Euler angles). Tweak this to stand books upright.")]
    [SerializeField] private Vector3 bookRotationEuler = new Vector3(0f, 90f, 0f);

    [Header("Rejection")]
    [SerializeField] private Vector3 rejectDirection = Vector3.back;
    [SerializeField] private float rejectForce = 1.8f;

    [Header("VS2 Hooks (Phase 2 Prep)")]
    [SerializeField] private Renderer shelfRenderer;
    [SerializeField] private Material defaultMaterial;
    [SerializeField] private Material highlightMaterial;

    private readonly List<Transform> generatedSlots = new List<Transform>();
    private readonly List<Book> placedBooks = new List<Book>();

    public bool IsFull => placedBooks.Count >= slotCount;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        if (centerPoint == null)
            centerPoint = transform;

        CreateSnapSlots();
    }

    /// <summary>
    /// Generates snap slot transforms dynamically, evenly spaced around centerPoint.
    /// </summary>
    private void CreateSnapSlots()
    {
        // Container to keep the hierarchy tidy
        GameObject container = new GameObject("_GeneratedSlots");
        container.transform.SetParent(centerPoint, false);

        generatedSlots.Clear();

        for (int i = 0; i < slotCount; i++)
        {
            // Calculates offset centered around 0 (e.g. for 2 items: -0.5 and +0.5)
            float offset = (i - (slotCount - 1) * 0.5f) * slotSpacing;
            Vector3 localPos = spacingAxis.normalized * offset;

            GameObject slot = new GameObject($"Slot_{i}");
            slot.transform.SetParent(container.transform, false);
            slot.transform.localPosition = localPos;
            slot.transform.localRotation = Quaternion.Euler(bookRotationEuler);

            generatedSlots.Add(slot.transform);
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
                AcceptBook(book);
            }
            else
            {
                RejectBook(book);
            }
        }
    }

    private void AcceptBook(Book book)
    {
        int slotIndex = placedBooks.Count;
        Transform targetSlot = generatedSlots[slotIndex];

        placedBooks.Add(book);
        book.PlaceOnShelf(targetSlot);

        TaskManager.Instance?.NotifyBookPlaced(book, this);
    }

    private void RejectBook(Book book)
    {
        Vector3 pushDir = transform.TransformDirection(rejectDirection.normalized);
        book.RejectFromShelf(pushDir, rejectForce);
        TaskManager.Instance?.NotifyBookRejected(book, this);
    }

    // ---- Phase 2 Hooks (Shelf Highlight for VS2) ----
    public void SetHighlight(bool active)
    {
        if (shelfRenderer == null || highlightMaterial == null || defaultMaterial == null) return;
        shelfRenderer.material = active ? highlightMaterial : defaultMaterial;
    }

    // ---- Editor Visualizer (Preview book slots in Scene View) ------------

    private void OnDrawGizmosSelected()
    {
        Transform center = centerPoint != null ? centerPoint : transform;
        Gizmos.color = Color.cyan;

        Vector3 previewBookSize = new Vector3(0.04f, 0.22f, 0.16f); // Approx book dimensions

        for (int i = 0; i < slotCount; i++)
        {
            float offset = (i - (slotCount - 1) * 0.5f) * slotSpacing;
            Vector3 worldPos = center.TransformPoint(spacingAxis.normalized * offset);
            Quaternion worldRot = center.rotation * Quaternion.Euler(bookRotationEuler);

            // Draw a preview box and directional forward arrow for each book slot
            Matrix4x4 prevMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(worldPos, worldRot, Vector3.one);

            Gizmos.DrawWireCube(Vector3.zero, previewBookSize);
            Gizmos.DrawRay(Vector3.zero, Vector3.forward * 0.1f); // Points in the spine direction

            Gizmos.matrix = prevMatrix;
        }
    }
}