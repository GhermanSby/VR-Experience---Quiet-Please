using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class TaskManager : MonoBehaviour
{
    public static TaskManager Instance { get; private set; }

    [Header("Book Setup")]
    [Tooltip("Optional: Parent object containing all books. If empty, scans the entire scene.")]
    [SerializeField] private Transform booksContainer;

    [Header("Task Progress (Read-Only)")]
    [SerializeField] private int totalBooks = 0;
    [SerializeField] private int placedBooksCount = 0;
    [SerializeField] private bool isTaskComplete = false;

    [Header("Completion Feedback")]
    [Tooltip("Object to turn ON when complete (e.g. 'Task Complete' 3D Text or Green Light).")]
    [SerializeField] private GameObject completionIndicator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip completionSound;
    public UnityEvent OnTaskCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (completionIndicator != null)
            completionIndicator.SetActive(false);
    }

    private void Start()
    {
        SetupTaskAndShelves();
    }

    /// <summary>
    /// Finds all books and instructs shelves to create the exact number of needed slots.
    /// </summary>
    private void SetupTaskAndShelves()
    {
        // 1. Gather all books
        Book[] allBooks = booksContainer != null
            ? booksContainer.GetComponentsInChildren<Book>()
            : Object.FindObjectsByType<Book>(FindObjectsSortMode.None);

        totalBooks = allBooks.Length;
        placedBooksCount = 0;

        // 2. Count books per color
        Dictionary<BookColor, int> colorCounts = new Dictionary<BookColor, int>();
        foreach (Book book in allBooks)
        {
            if (!colorCounts.ContainsKey(book.bookColor))
                colorCounts[book.bookColor] = 0;
            colorCounts[book.bookColor]++;
        }

        // 3. Configure shelves automatically
        Shelf[] shelves = Object.FindObjectsByType<Shelf>(FindObjectsSortMode.None);
        foreach (Shelf shelf in shelves)
        {
            int requiredSlots = 0;
            foreach (BookColor color in shelf.acceptedColors)
            {
                if (colorCounts.TryGetValue(color, out int count))
                {
                    requiredSlots += count;
                }
            }
            shelf.InitializeSlots(requiredSlots);
        }

        Debug.Log($"<color=cyan>[TaskManager] Initialized task with {totalBooks} books across {shelves.Length} shelves.</color>");
    }

    public void NotifyBookPlaced(Book book, Shelf shelf)
    {
        placedBooksCount++;
        Debug.Log($"[TaskManager] Book placed. Progress: {placedBooksCount}/{totalBooks}");

        if (placedBooksCount >= totalBooks && !isTaskComplete)
        {
            CompleteTask();
        }
    }

    public void NotifyBookRejected(Book book, Shelf shelf)
    {
        Debug.Log($"[TaskManager] Wrong placement attempt for {book.bookColor} book.");
    }

    private void CompleteTask()
    {
        isTaskComplete = true;
        Debug.Log("<color=green><b>[TaskManager] ★ ALL BOOKS PLACED! TASK COMPLETE! ★</b></color>");

        // 1. Show Visual Feedback
        if (completionIndicator != null)
        {
            completionIndicator.SetActive(true);
        }

        // 2. Play Audio Feedback
        if (audioSource != null && completionSound != null)
        {
            audioSource.PlayOneShot(completionSound);
        }

        // 3. Fire custom events
        OnTaskCompleted?.Invoke();
    }
}