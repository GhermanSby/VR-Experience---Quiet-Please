// TaskManager.cs  -  owns the book task: the books, the shelves, the progress and the completion.
//
// The GameManager decides which virtual subjectiveness is active. The TaskManager listens to that
// and swaps its implementation: TaskModeVS1 (ADHD) or TaskModeVS2 (non-ADHD). Everything that is
// the same in both (counting, shelf slots, completion feedback) stays here; everything that differs
// lives in the TaskMode files.
//
// Without a GameManager in the scene the task still runs, using "Standalone VS" below.

using System;
using System.Collections.Generic;
using QuietPlease;
using UnityEngine;
using UnityEngine.Events;

public class TaskManager : MonoBehaviour
{
    public static TaskManager Instance { get; private set; }

    [Header("Book Setup")]
    [Tooltip("Optional: Parent object containing all books. If empty, scans the entire scene.")]
    [SerializeField] private Transform booksContainer;

    [Header("Virtual Subjectiveness")]
    [Tooltip("Only used when the scene has no GameManager. With a GameManager, its active VS Profile decides.")]
    [SerializeField] private VSType standaloneVS = VSType.VS1_ADHD;

    [Header("Task Progress (Read-Only)")]
    [SerializeField] private int totalBooks = 0;
    [SerializeField] private int placedBooksCount = 0;
    [SerializeField] private bool isTaskComplete = false;
    [SerializeField] private string activeMode = "";

    [Header("Completion Feedback")]
    [Tooltip("Object to turn ON when complete (e.g. 'Task Complete' 3D Text or Green Light).")]
    [SerializeField] private GameObject completionIndicator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip completionSound;
    public UnityEvent OnTaskCompleted;

    public int BooksPlaced => placedBooksCount;
    public int BooksTotal => totalBooks;
    public bool IsComplete => isTaskComplete;
    public IReadOnlyList<Book> Books => books;
    public IReadOnlyList<Shelf> Shelves => shelves;

    // Events other scripts listen to (UI, sound system, ...)
    public event Action<int, int> ProgressChanged;   // placed, total
    public event Action<Book> BookGrabbed;
    public event Action<Book> BookReleased;
    public event Action TaskCompleted;

    private readonly List<Book> books = new List<Book>();
    private readonly List<Shelf> shelves = new List<Shelf>();
    private TaskMode mode;
    private bool isSetUp;
    private GameManager subscribedTo;

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

    private void OnEnable()
    {
        subscribedTo = GameManager.Instance;
        if (subscribedTo != null) subscribedTo.VSChanged += BeginTask;
    }

    private void OnDisable()
    {
        if (subscribedTo != null) subscribedTo.VSChanged -= BeginTask;
        subscribedTo = null;
    }

    private void Start()
    {
        // The GameManager starts first and normally has already begun the task through VSChanged.
        if (mode == null)
        {
            GameManager gm = GameManager.Instance;
            BeginTask(gm != null ? gm.Current : null);
        }
    }

    private void OnDestroy()
    {
        foreach (Book book in books)
        {
            if (book == null) continue;
            book.Grabbed -= HandleBookGrabbed;
            book.Released -= HandleBookReleased;
        }
        if (Instance == this) Instance = null;
    }

    // ---- start / restart ---------------------------------------------------

    /// <summary>
    /// Resets the task and switches to the implementation for this profile's VS.
    /// Called through GameManager.VSChanged; a null profile means "use Standalone VS".
    /// </summary>
    public void BeginTask(VSProfile profile)
    {
        SetupTaskAndShelves();

        if (mode != null) mode.End();

        placedBooksCount = 0;
        isTaskComplete = false;
        if (completionIndicator != null) completionIndicator.SetActive(false);
        foreach (Shelf shelf in shelves) shelf.ClearBooks();
        foreach (Book book in books) book.ResetToStart();

        mode = CreateMode(profile != null ? profile.type : standaloneVS);
        activeMode = mode.GetType().Name;
        mode.Begin(this, profile);

        Debug.Log($"<color=cyan>[TaskManager] Task started with {activeMode}: {totalBooks} books across {shelves.Count} shelves.</color>");
        ProgressChanged?.Invoke(placedBooksCount, totalBooks);
    }

    private static TaskMode CreateMode(VSType type)
    {
        switch (type)
        {
            case VSType.VS2_Typical: return new TaskModeVS2();
            default: return new TaskModeVS1();
        }
    }

    /// <summary>
    /// Finds all books and instructs shelves to create the exact number of needed slots. Runs once.
    /// </summary>
    private void SetupTaskAndShelves()
    {
        if (isSetUp) return;
        isSetUp = true;

        // 1. Gather all books
        Book[] allBooks = booksContainer != null
            ? booksContainer.GetComponentsInChildren<Book>()
            : FindObjectsByType<Book>();

        books.AddRange(allBooks);
        totalBooks = books.Count;

        // 2. Count books per color, and listen to each book being grabbed / released
        Dictionary<BookColor, int> colorCounts = new Dictionary<BookColor, int>();
        foreach (Book book in books)
        {
            if (!colorCounts.ContainsKey(book.bookColor))
                colorCounts[book.bookColor] = 0;
            colorCounts[book.bookColor]++;

            book.Grabbed += HandleBookGrabbed;
            book.Released += HandleBookReleased;
        }

        // 3. Configure shelves automatically
        shelves.AddRange(FindObjectsByType<Shelf>());
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
    }

    // ---- what happens during the task ---------------------------------------

    private void HandleBookGrabbed(Book book)
    {
        GameManager.Instance?.MarkTaskActivity();
        mode?.OnBookGrabbed(book);
        BookGrabbed?.Invoke(book);
    }

    private void HandleBookReleased(Book book)
    {
        mode?.OnBookReleased(book);
        BookReleased?.Invoke(book);
    }

    // Called by Shelf
    public void NotifyBookPlaced(Book book, Shelf shelf)
    {
        placedBooksCount++;
        Debug.Log($"[TaskManager] Book placed. Progress: {placedBooksCount}/{totalBooks}");

        GameManager.Instance?.MarkTaskActivity();
        ProgressChanged?.Invoke(placedBooksCount, totalBooks);
        mode?.OnBookPlaced(book, shelf);

        if (placedBooksCount >= totalBooks && !isTaskComplete)
        {
            CompleteTask();
        }
    }

    // Called by Shelf
    public void NotifyBookRejected(Book book, Shelf shelf)
    {
        Debug.Log($"[TaskManager] Wrong placement attempt for {book.bookColor} book.");
        mode?.OnBookRejected(book, shelf);
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

        // 3. Fire custom events, then report to the administrator
        TaskCompleted?.Invoke();
        OnTaskCompleted?.Invoke();
        GameManager.Instance?.NotifyTaskCompleted();
    }
}
