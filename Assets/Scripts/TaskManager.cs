using UnityEngine;
using UnityEngine.Events;

public class TaskManager : MonoBehaviour
{
    public static TaskManager Instance { get; private set; }

    [Header("Task Settings")]
    [SerializeField] private int totalBooksTarget = 3;

    [Header("State")]
    [SerializeField] private int booksPlacedCount = 0;
    [SerializeField] private bool isTaskComplete = false;

    [Header("Events")]
    public UnityEvent<int, int> OnBookCountChanged; // (current, total)
    public UnityEvent OnTaskCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void NotifyBookPlaced(Book book, Shelf shelf)
    {
        booksPlacedCount++;
        Debug.Log($"[Task] Book placed ({book.bookColor}). Total: {booksPlacedCount}/{totalBooksTarget}");
        OnBookCountChanged?.Invoke(booksPlacedCount, totalBooksTarget);

        if (booksPlacedCount >= totalBooksTarget && !isTaskComplete)
        {
            CompleteTask();
        }
    }

    public void NotifyBookRejected(Book book, Shelf shelf)
    {
        Debug.Log($"[Task] Rejection: Book {book.bookColor} placed on invalid shelf.");
    }

    private void CompleteTask()
    {
        isTaskComplete = true;
        Debug.Log("<color=green><b>[Task] ALL BOOKS PLACED - MILESTONE 1 COMPLETE!</b></color>");
        OnTaskCompleted?.Invoke();
    }
}