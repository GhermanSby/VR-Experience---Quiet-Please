using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(Collider))]
public class Shelf : MonoBehaviour
{
    [Header("Accepted Colours")]
    [Tooltip("Shelf 1: Green + Blue | Shelf 2: Red")]
    public List<BookColor> acceptedColors = new List<BookColor>();

    [Header("Snap Slots")]
    [Tooltip("Transforms where books sit when placed. Assign 2 slots for Shelf 1, 1 slot for Shelf 2.")]
    [SerializeField] private List<Transform> snapSlots = new List<Transform>();

    [Header("Rejection")]
    [SerializeField] private Vector3 rejectDirection = Vector3.back;
    [SerializeField] private float rejectForce = 1.8f;

    [Header("VS2 Hooks (Phase 2 Prep)")]
    [SerializeField] private Renderer shelfRenderer;
    [SerializeField] private Material defaultMaterial;
    [SerializeField] private Material highlightMaterial;

    private readonly List<Book> placedBooks = new List<Book>();

    public bool IsFull => placedBooks.Count >= snapSlots.Count;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
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
        Transform targetSlot = snapSlots[slotIndex];

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
}