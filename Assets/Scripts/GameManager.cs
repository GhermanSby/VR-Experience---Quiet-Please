// GameManager.cs  -  runs the experience: VS1 then VS2 in the SAME scene,
// tracks task progress (books shelved), attention (focused / drifting) and pop-up pauses,
// and tells every VS-aware object which profile is active.

using System;
using System.Collections.Generic;
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
        public int BooksShelved { get; private set; }
        public int BooksTotal => books.Count;
        public bool PausedByPopUp => openPopUps > 0;
        public float RunTime { get; private set; }

        // Events other scripts listen to
        public event Action<VSProfile> VSChanged;
        public event Action<int, int> ProgressChanged;     // shelved, total
        public event Action<Attention> AttentionChanged;
        public event Action Fidgeted;
        public event Action<Book> BookPickedUp;
        public event Action<Book> BookReleased;
        public event Action<VSProfile, float> RunFinished; // profile, seconds

        readonly List<Book> books = new List<Book>();
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

        public void StartVS(VSProfile profile)
        {
            Current = profile;
            BooksShelved = 0; RunTime = 0f; openPopUps = 0;
            foreach (var b in books) b.ResetToStart();
            if (playerRig && playerStart) playerRig.SetPositionAndRotation(playerStart.position, playerStart.rotation);
            lastTaskActivity = Time.time;
            SetAttention(Attention.Focused);
            Phase = Phase.Playing;
            VSChanged?.Invoke(Current);
            ProgressChanged?.Invoke(BooksShelved, BooksTotal);
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

        // ---- registration & task -------------------------------------------------

        public void Register(Book b) { if (!books.Contains(b)) books.Add(b); }

        public void NotifyBookPickedUp(Book b) { MarkTaskActivity(); BookPickedUp?.Invoke(b); }
        public void NotifyBookReleased(Book b) { BookReleased?.Invoke(b); }

        public void NotifyBookShelved(Book b)
        {
            BooksShelved++;
            MarkTaskActivity();
            ProgressChanged?.Invoke(BooksShelved, BooksTotal);
            if (BooksShelved >= BooksTotal) FinishRun();
        }

        public void MarkTaskActivity() => lastTaskActivity = Time.time;

        // ---- attention, fidget, pop-ups -------------------------------------------

        public void NotifyFidget()
        {
            if (!Current.fidgetRestoresTime) return;
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
