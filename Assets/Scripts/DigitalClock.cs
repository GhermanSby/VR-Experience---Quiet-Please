using TMPro;
using UnityEngine;

public enum ClockMode
{
    AlwaysNormal,          // 1:1 the whole time (VS2)
    DriftUnlessFidgeting   // "crazy", but normal while fidgeting (VS1)
}

public class DigitalClock : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text display;

    [Header("Mode")]
    [SerializeField] private ClockMode mode = ClockMode.DriftUnlessFidgeting;

    [Header("Start time")]
    [SerializeField] private int startHour = 14;
    [SerializeField] private int startMinute = 0;
    [SerializeField] private bool showSeconds = true;

    [Header("Drift")]
    [Tooltip("Slowest possible speed (1 = real time)")]
    [SerializeField] private float minSpeed = 0.2f;
    [Tooltip("Fastest possible speed")]
    [SerializeField] private float maxSpeed = 3f;
    [Tooltip("Random seconds between speed changes")]
    [SerializeField] private Vector2 speedChangeInterval = new Vector2(3f, 8f);
    [Tooltip("How quickly the speed blends toward the new target")]
    [SerializeField] private float speedSmoothing = 2f;

    [Header("Resync while fidgeting")]
    [Tooltip("Clock seconds per real second used to catch up to real time")]
    [SerializeField] private float catchUpRate = 2f;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color fastColor = new Color(1f, 0.45f, 0.45f);
    [SerializeField] private Color slowColor = new Color(0.45f, 0.65f, 1f);
    [Tooltip("How far the speed can be from 1 and still count as normal (stays white)")]
    [SerializeField] private float normalTolerance = 0.1f;

    private float realSeconds;
    private float shownSeconds;
    private float currentSpeed = 1f;
    private float targetSpeed = 1f;
    private float nextSpeedChangeTime;
    private int lastShownWholeSecond = -1;

    public ClockMode Mode => mode;
    public void SetMode(ClockMode newMode) => mode = newMode;

    private void Start()
    {
        realSeconds = shownSeconds = startHour * 3600f + startMinute * 60f;
        UpdateText(true);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        realSeconds += dt;

        bool fidgeting = FidgetTracker.Instance != null && FidgetTracker.Instance.IsFidgeting;
        bool drifting = mode == ClockMode.DriftUnlessFidgeting && !fidgeting;

        if (drifting)
        {
            if (Time.time >= nextSpeedChangeTime)
            {
                // Pick clearly slow or clearly fast, never close to 1
                targetSpeed = Random.value < 0.5f
                    ? Random.Range(minSpeed, 0.6f)
                    : Random.Range(1.6f, maxSpeed);
                nextSpeedChangeTime = Time.time + Random.Range(speedChangeInterval.x, speedChangeInterval.y);
            }

            currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, speedSmoothing * dt);
            shownSeconds += dt * currentSpeed;
        }
        else
        {
            targetSpeed = 1f;
            currentSpeed = Mathf.Lerp(currentSpeed, 1f, speedSmoothing * dt);
            shownSeconds += dt;

            // Slowly pull the displayed time back to the real time
            float diff = realSeconds - shownSeconds;
            float maxStep = catchUpRate * dt;
            shownSeconds += Mathf.Clamp(diff, -maxStep, maxStep);
        }

        UpdateColor();
        UpdateText(false);
        UpdateText(false);
    }

    private void UpdateText(bool force)
    {
        int total = Mathf.FloorToInt(shownSeconds);
        if (!force && total == lastShownWholeSecond) return;
        lastShownWholeSecond = total;

        total = ((total % 86400) + 86400) % 86400;
        int h = total / 3600;
        int m = (total % 3600) / 60;
        int s = total % 60;

        display.text = showSeconds ? $"{h:00}:{m:00}:{s:00}" : $"{h:00}:{m:00}";
    }

    private void UpdateColor()
    {
        Color target = normalColor;

        if (currentSpeed > 1f + normalTolerance)
        {
            float t = Mathf.InverseLerp(1f + normalTolerance, maxSpeed, currentSpeed);
            target = Color.Lerp(normalColor, fastColor, t);
        }
        else if (currentSpeed < 1f - normalTolerance)
        {
            float t = Mathf.InverseLerp(1f - normalTolerance, minSpeed, currentSpeed);
            target = Color.Lerp(normalColor, slowColor, t);
        }

        display.color = target;
    }
}