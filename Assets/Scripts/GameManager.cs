// GameManager.cs  -  the administrator of the experience.
// It decides WHICH virtual subjectiveness is active and runs the flow (VS1, then VS2, in the SAME scene).
// It also keeps the things every system shares: run time, attention (focused / drifting) and pop-up pauses.
//
// It does NOT know about books or shelves. The task belongs to TaskManager, which listens to
// VSChanged, picks its VS1 or VS2 implementation, and reports back with
// MarkTaskActivity() and NotifyTaskCompleted().

using System;
using UnityEngine;

namespace QuietPlease
{
    public enum Phase { Intro, Playing, Between, Finished }
    public enum Attention { Focused, Drifting }

    [DefaultExecutionOrder(-100)] // runs before the VS-aware objects
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Profiles, played in this order")]
        public VSProfile vs1;   // ADHD perspective
        public VSProfile vs2;   // non-ADHD perspective

        [Header("Optional")]
        public Transform playerRig;          // the camera rig root
        public Transform playerStart;        // Anchor_PlayerStart

        public VSProfile Current { get; private set; }
        public Phase Phase { get; private set; } = Phase.Intro;
        public Attention Attention { get; private set; } = Attention.Focused;
        public bool PausedByPopUp => openPopUps > 0;
        public float RunTime { get; private set; }

        // Events other scripts listen to
        public event Action<VSProfile> VSChanged;
        public event Action<Attention> AttentionChanged;
        public event Action Fidgeted;
        public event Action<VSProfile, float> RunFinished; // profile, seconds

        float lastTaskActivity;
        int openPopUps;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start() => StartVS(vs1);

        void Update()
        {
            if (Phase != Phase.Playing) return;
            if (!PausedByPopUp) RunTime += Time.deltaTime;

            // Attention: drifting after a while without task activity (VS1 only, if enabled)
            bool drifting = Current.racingClock &&
                            Time.time - lastTaskActivity > Current.idleSecondsBeforeDrift;
            SetAttention(drifting ? Attention.Drifting : Attention.Focused);
        }

        // ---- flow ----------------------------------------------------------------

        // Also the entry point for a menu: call StartVS(vs1) or StartVS(vs2) to switch.
        public void StartVS(VSProfile profile)
        {
            if (profile == null)
            {
                Debug.LogError("[GameManager] No VS Profile assigned. Create one (Create > Quiet Please > VS Profile) and drag it into vs1 / vs2.");
                return;
            }

            Current = profile;
            RunTime = 0f; openPopUps = 0;
            if (playerRig && playerStart) playerRig.SetPositionAndRotation(playerStart.position, playerStart.rotation);
            lastTaskActivity = Time.time;
            SetAttention(Attention.Focused);
            Phase = Phase.Playing;
            VSChanged?.Invoke(Current);   // TaskManager and every VSBehaviour react to this
            Debug.Log($"[GameManager] Started {Current.displayName}");
        }

        void FinishRun()
        {
            Phase = Current == vs1 ? Phase.Between : Phase.Finished;
            RunFinished?.Invoke(Current, RunTime);
            Debug.Log($"[GameManager] Finished {Current.displayName} in {RunTime:0.0} s");
        }

        // Call this from a "Continue" button or after a short pause between the runs
        public void StartNextVS() { if (Phase == Phase.Between) StartVS(vs2); }

        // ---- reports from the task -------------------------------------------------

        // TaskManager calls this whenever the player works on the task (grabs or shelves a book)
        public void MarkTaskActivity() => lastTaskActivity = Time.time;

        // TaskManager calls this when the last book is on its shelf
        public void NotifyTaskCompleted() { if (Phase == Phase.Playing) FinishRun(); }

        // ---- attention, fidget, pop-ups -------------------------------------------

        public void NotifyFidget()
        {
            if (Current == null || !Current.fidgetRestoresTime) return;
            MarkTaskActivity();
            Fidgeted?.Invoke();
        }

        public void PopUpOpened() => openPopUps++;
        public void PopUpClosed() => openPopUps = Mathf.Max(0, openPopUps - 1);

        void SetAttention(Attention a)
        {
            if (a == Attention) return;
            Attention = a;
            AttentionChanged?.Invoke(a);
        }
    }
}
