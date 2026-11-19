using UnityEngine;

public class FidgetTracker : MonoBehaviour
{
    public static FidgetTracker Instance { get; private set; }

    [Tooltip("How many seconds after the last click/squeeze the player still counts as fidgeting")]
    [SerializeField] private float activeWindow = 2f;

    private float lastFidgetTime = -999f;

    public int TotalFidgetCount { get; private set; }
    public bool IsFidgeting => Time.time - lastFidgetTime <= activeWindow;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void ReportFidget()
    {
        lastFidgetTime = Time.time;
        TotalFidgetCount++; // handy later for logging
    }
}