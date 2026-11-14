using UnityEngine;

namespace QuietPlease
{
    public class RoomAudioController : MonoBehaviour
    {
        [Header("Master Volume Cap (Lower this if too overwhelming)")]
        [Range(0f, 1f)] public float masterVolume = 0.45f;
        [Range(0f, 1f)] public float vs2QuietMultiplier = 0.4f; // VS2 is much calmer

        [Header("1. Clock Pair (Alternates in VS1, Normal only in VS2)")]
        public AudioSource clockNormal;   // Drag 'Clock 1' here
        public AudioSource clockHorror;   // Drag 'CLock 2' here
        [Range(0f, 1f)] public float clockMaxVolume = 0.5f;
        [Tooltip("Seconds before switching between Normal and Horror clock in VS1")]
        public float clockSwitchInterval = 12f;

        [Header("2. Street Pair (Alternates in VS1, Normal only in VS2)")]
        public AudioSource streetNormal;  // Drag 'Street sound 1' here
        public AudioSource streetHorns;   // Drag 'Street Sound 2' here
        [Range(0f, 1f)] public float streetMaxVolume = 0.4f;
        [Tooltip("Seconds before switching between Normal street and Horns in VS1")]
        public float streetSwitchInterval = 15f;

        [Header("3. Fan & Squeaky Shoes")]
        public AudioSource fanSource;     // Drag 'Fan' here
        [Range(0f, 1f)] public float fanMaxVolume = 0.3f;

        public AudioSource squeakyShoes;  // Drag 'Sqeaqy shoes' here
        [Range(0f, 1f)] public float shoesMaxVolume = 0.9f; // Boosted so you hear it!
        [Tooltip("How often footsteps fade in/out in VS1 (seconds)")]
        public float shoesInterval = 10f;

        [Header("Transition Smoothness")]
        public float crossfadeSpeed = 1.2f;

        private float duckTimer = 0f;

        void Start()
        {
            // Force all sources to loop and play continuously so they never cut off
            EnsurePlayingLoop(clockNormal);
            EnsurePlayingLoop(clockHorror);
            EnsurePlayingLoop(streetNormal);
            EnsurePlayingLoop(streetHorns);
            EnsurePlayingLoop(fanSource);
            EnsurePlayingLoop(squeakyShoes);
        }

        void OnEnable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.BookPickedUp += OnBookPickedUp;
                GameManager.Instance.Fidgeted += OnFidgeted;
            }
        }

        void OnDisable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.BookPickedUp -= OnBookPickedUp;
                GameManager.Instance.Fidgeted -= OnFidgeted;
            }
        }

        void OnBookPickedUp(Book b)
        {
            if (GameManager.Instance.Current != null && GameManager.Instance.Current.duckOnBookPickup)
                duckTimer = 2.5f;
        }

        void OnFidgeted()
        {
            duckTimer = 3.0f; // Fidgeting calms the room down briefly
        }

        void Update()
        {
            // Check if we are in ADHD (VS1) or Typical (VS2)
            bool isADHD = true;
            float profileAmbient = 1f;

            if (GameManager.Instance != null && GameManager.Instance.Current != null)
            {
                isADHD = GameManager.Instance.Current.extraSources; // true in VS1, false in VS2
                profileAmbient = GameManager.Instance.Current.ambientVolume;
            }

            // Calculate overall multiplier
            float globalMult = masterVolume * profileAmbient;
            if (!isADHD) globalMult *= vs2QuietMultiplier;

            if (duckTimer > 0f)
            {
                duckTimer -= Time.deltaTime;
                globalMult *= 0.25f;
            }

            // --- 1. ALTERNATE CLOCKS (Never play both at full volume together) ---
            // In VS1, alternates back and forth every clockSwitchInterval seconds
            bool useHorrorClock = isADHD && (Mathf.FloorToInt(Time.time / clockSwitchInterval) % 2 == 1);
            float targetClockNormal = (useHorrorClock ? 0f : clockMaxVolume) * globalMult;
            float targetClockHorror = (useHorrorClock ? clockMaxVolume : 0f) * globalMult;

            FadeTo(clockNormal, targetClockNormal);
            FadeTo(clockHorror, targetClockHorror);

            // --- 2. ALTERNATE STREET SOUNDS (Offset timer so it doesn't switch at the exact same second as the clock) ---
            bool useHornsStreet = isADHD && (Mathf.FloorToInt((Time.time + 5f) / streetSwitchInterval) % 2 == 1);
            float targetStreetNormal = (useHornsStreet ? 0f : streetMaxVolume) * globalMult;
            float targetStreetHorns = (useHornsStreet ? streetMaxVolume : 0f) * globalMult;

            FadeTo(streetNormal, targetStreetNormal);
            FadeTo(streetHorns, targetStreetHorns);

            // --- 3. FAN (Steady background hum) ---
            FadeTo(fanSource, fanMaxVolume * globalMult);

            // --- 4. SQUEAKY SHOES (VS1 only, comes and goes so it grabs attention) ---
            bool shoesActive = isADHD && (Mathf.FloorToInt(Time.time / shoesInterval) % 2 == 0);
            float targetShoes = (shoesActive ? shoesMaxVolume : 0f) * masterVolume; // Uses masterVolume directly so it stays clear!
            FadeTo(squeakyShoes, targetShoes);
        }

        void EnsurePlayingLoop(AudioSource src)
        {
            if (src == null) return;
            src.loop = true;
            src.volume = 0f; // Start at 0 and fade in smoothly
            if (!src.isPlaying) src.Play();
        }

        void FadeTo(AudioSource src, float targetVolume)
        {
            if (src == null) return;
            if (!src.isPlaying) src.Play(); // Safety restart if Unity ever stops it
            src.volume = Mathf.MoveTowards(src.volume, Mathf.Clamp01(targetVolume), Time.deltaTime * crossfadeSpeed);
        }
    }
}