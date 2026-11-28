using UnityEngine;
using UnityEngine.InputSystem;

namespace QuietPlease
{
    public class RoomAudioController : MonoBehaviour
    {
        [Header("Scene References")]
        public DigitalClock digitalClock;

        [Header("Master Volume Cap")]
        [Range(0f, 1f)] public float masterVolume = 0.25f;
        [Range(0f, 1f)] public float vs2QuietMultiplier = 0.25f;

        [Header("1. Clock Pair (Horror in VS1 when Red, Normal ONLY in VS2)")]
        public AudioSource clockNormal;
        public AudioSource clockHorror;
        [Range(0f, 1f)] public float clockMaxVolume = 0.5f;

        [Header("2. Street Pair (Horns in VS1, Soft Calm ONLY in VS2)")]
        public AudioSource streetNormal;
        public AudioSource streetHorns;
        [Range(0f, 1f)] public float streetMaxVolume = 0.4f;
        public float streetSwitchInterval = 15f;

        [Header("3. Fan & Squeaky Shoes (Shoes ONLY in VS1)")]
        public AudioSource fanSource;
        [Range(0f, 1f)] public float fanMaxVolume = 0.3f;

        public AudioSource squeakyShoes;
        [Range(0f, 1f)] public float shoesMaxVolume = 0.6f;
        public float shoesInterval = 10f;

        [Header("Transition & Debug")]
        public float crossfadeSpeed = 3.0f;
        [Tooltip("Press V in Play Mode to test switching between VS1 (ADHD) and VS2 (Non-ADHD)")]
        public Key debugToggleKey = Key.V;
        [SerializeField] private bool isVS1 = true;

        private bool manualOverride = false;

        private void Awake()
        {
            SilenceAll();
        }

        private void Start()
        {
            if (digitalClock == null)
                digitalClock = FindFirstObjectByType<DigitalClock>();

            EnsurePlayingLoop(clockNormal);
            EnsurePlayingLoop(clockHorror);
            EnsurePlayingLoop(streetNormal);
            EnsurePlayingLoop(streetHorns);
            EnsurePlayingLoop(fanSource);
            EnsurePlayingLoop(squeakyShoes);
        }

        private void SilenceAll()
        {
            if (clockNormal != null) clockNormal.volume = 0f;
            if (clockHorror != null) clockHorror.volume = 0f;
            if (streetNormal != null) streetNormal.volume = 0f;
            if (streetHorns != null) streetHorns.volume = 0f;
            if (fanSource != null) fanSource.volume = 0f;
            if (squeakyShoes != null) squeakyShoes.volume = 0f;
        }

        private void Update()
        {
            // Uses Unity's new Input System (safe with Meta XR)
            if (Keyboard.current != null && Keyboard.current[debugToggleKey].wasPressedThisFrame)
            {
                manualOverride = true;
                isVS1 = !isVS1;
                Debug.Log($"[RoomAudioController] Manual toggle -> {(isVS1 ? "VS1 (ADHD / Chaotic)" : "VS2 (Non-ADHD / Calm)")}");
            }
            else if (!manualOverride && GameManager.Instance != null && GameManager.Instance.Current != null)
            {
                isVS1 = GameManager.Instance.Current.extraSources;
            }

            float globalMult = masterVolume * (isVS1 ? 1f : vs2QuietMultiplier);

            // Duck volume while player is actively fidgeting
            if (FidgetTracker.Instance != null && FidgetTracker.Instance.IsFidgeting)
                globalMult *= 0.3f;

            // --- 1. CLOCK ---
            bool useHorrorClock = isVS1 && (digitalClock != null && digitalClock.IsFast);
            FadeTo(clockNormal, (useHorrorClock ? 0f : clockMaxVolume) * globalMult);
            FadeTo(clockHorror, (useHorrorClock ? clockMaxVolume : 0f) * globalMult);

            // --- 2. STREET ---
            bool useHornsStreet = isVS1 && (Mathf.FloorToInt((Time.time + 5f) / streetSwitchInterval) % 2 == 1);
            FadeTo(streetNormal, (useHornsStreet ? 0f : streetMaxVolume) * globalMult);
            FadeTo(streetHorns, (useHornsStreet ? streetMaxVolume : 0f) * globalMult);

            // --- 3. FAN & SQUEAKY SHOES ---
            FadeTo(fanSource, fanMaxVolume * globalMult);

            bool shoesActive = isVS1 && (Mathf.FloorToInt(Time.time / shoesInterval) % 2 == 0);
            FadeTo(squeakyShoes, (shoesActive ? shoesMaxVolume : 0f) * globalMult);
        }

        private void EnsurePlayingLoop(AudioSource src)
        {
            if (src == null) return;
            src.loop = true;
            src.volume = 0f;
            if (!src.isPlaying) src.Play();
        }

        private void FadeTo(AudioSource src, float targetVolume)
        {
            if (src == null) return;
            if (!src.isPlaying) src.Play();
            src.volume = Mathf.MoveTowards(src.volume, Mathf.Clamp01(targetVolume), Time.deltaTime * crossfadeSpeed);
        }
    }
}