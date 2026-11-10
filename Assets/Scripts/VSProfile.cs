// VSProfile.cs  -  one asset per virtual subjectiveness.
// Create with: right-click in Project > Create > Quiet Please > VS Profile
// Make two: "VS1_ADHD" and "VS2_Typical". Every VS-dependent value lives here,
// so the scene stays the same and only the profile changes (same VE, different VS).

using UnityEngine;

namespace QuietPlease
{
    public enum VSType { VS1_ADHD, VS2_Typical }

    [CreateAssetMenu(menuName = "Quiet Please/VS Profile", fileName = "VSProfile")]
    public class VSProfile : ScriptableObject
    {
        public VSType type;
        public string displayName = "VS1 · Managing";

        [Header("Listener (audio mapping)")]
        [Range(0f, 1f)] public float ambientVolume = 1f;   // overall level of clock, fan, traffic, footsteps
        public bool volumeDrift = true;                     // volumes rise and fall on their own
        [Range(0f, 1f)] public float driftAmount = 0.6f;    // how far volumes drift (0..1 of base volume)
        public float driftSpeed = 0.15f;                    // how fast they drift
        public bool extraSources = true;                    // VS1-only sounds play
        public bool duckOnBookPickup = false;               // VS2: other sounds soften briefly when a book is picked up

        [Header("Virtual hands (hand mapping)")]
        public bool handOvershoot = true;                   // overshoot + inertia
        public float handStiffness = 60f;                   // spring stiffness (lower = more lag)
        [Range(0f, 1f)] public float handDamping = 0.35f;   // < 1 overshoots; 1 = no overshoot

        [Header("Grab")]
        public bool grabAnything = true;                    // VS1: books, pen, ball. VS2: books only

        [Header("Time (clock behaviour)")]
        public bool racingClock = true;
        public float driftTimeScale = 4f;                   // in-game minutes per real minute while attention drifts
        public float idleSecondsBeforeDrift = 6f;           // no task activity for this long = attention drifts
        public bool fidgetRestoresTime = true;

        [Header("Pop-ups")]
        public bool popUps = true;
        public Vector2 popUpInterval = new Vector2(15f, 30f); // seconds between pop-ups (min, max)
        public int maxPopUps = 2;

        [Header("Guidance")]
        public bool highlightMatchingShelf = false;         // VS2: shelf lights up when you hold a book
    }
}
