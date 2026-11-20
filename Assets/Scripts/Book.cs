using System.Collections;
using UnityEngine;
using Oculus.Interaction;
using System.Collections.Generic;

public enum BookColor { Green, Blue, Red }

[RequireComponent(typeof(Rigidbody))]
public class Book : MonoBehaviour
{
    [Header("Identity")]
    public BookColor bookColor = BookColor.Green;

    [Header("Rendering")]
    [Tooltip("Renderer whose material shows the colour. Auto-found if left empty.")]
    [SerializeField] private Renderer bookRenderer;
    [SerializeField] private Color heldColor = Color.white;

    [Header("Color Palette (Adjust Hues for your Material)")]
    [SerializeField] private Color greenPreset = new Color(0.22f, 0.65f, 0.32f); // Classic bookcloth green
    [SerializeField] private Color bluePreset = new Color(0.18f, 0.42f, 0.82f); // Royal bookcloth blue
    [SerializeField] private Color redPreset = new Color(0.82f, 0.20f, 0.20f); // Crimson red

    [Header("Behaviour (set by TaskManager's VS1 / VS2 mode at the start of a run)")]
    [Tooltip("VS2: the book keeps its real colour while held. Off (VS1): it turns white, so you have to remember the colour.")]
    public bool keepColorWhenHeld = false;
    [Tooltip("VS1: flicker through colours right after grabbing, before turning white.")]
    public bool flickerOnGrab = false;
    public float flickerDuration = 0.6f;
    public float flickerInterval = 0.08f;

    [Header("Extra Glitch Colors (Other colors appearing in flicker)")]
    [SerializeField]
    private Color[] extraGlitchColors = new Color[]
    {
        new Color(0.95f, 0.80f, 0.20f),
        new Color(0.70f, 0.25f, 0.85f),
        new Color(0.95f, 0.45f, 0.15f),
        new Color(0.20f, 0.80f, 0.80f)
    };

    [Header("State (read-only)")]
    [SerializeField] private bool isHeld;
    [SerializeField] private bool isShelved;

    [Tooltip("Plain white material (same shader, same UVs) shown while the book is held and during the flicker")]
    [SerializeField] private Material heldMaterial;

    private Material originalMaterial;
    public bool IsHeld => isHeld;
    public bool IsShelved => isShelved;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP / Lit
    private static readonly int ColorId = Shader.PropertyToID("_Color");         // Standard / Built-in
    private MaterialPropertyBlock block;
    private Rigidbody body;
    private Coroutine flickerRoutine;
    private bool onTable = true;

    // Where the book was when the scene started, so a new run can put it back
    private Vector3 startPosition;
    private Quaternion startRotation;
    private bool startKinematic;

    // Meta Grabbable reference
    private Grabbable grabbable;

    public System.Action<Book> Grabbed;
    public System.Action<Book> Released;
    public System.Action<Book> Shelved;

    // ---- Live Editor Updates (Instant visual preview) -------------------

    private void OnValidate()
    {
        // Automatically find renderer if not assigned
        if (bookRenderer == null) bookRenderer = GetComponentInChildren<Renderer>();
        if (block == null) block = new MaterialPropertyBlock();
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        grabbable = GetComponent<Grabbable>();

        startPosition = transform.position;
        startRotation = transform.rotation;
        startKinematic = body.isKinematic;

        if (bookRenderer == null) bookRenderer = GetComponentInChildren<Renderer>();
        if (block == null) block = new MaterialPropertyBlock();

        if (bookRenderer != null) originalMaterial = bookRenderer.sharedMaterial;
        ShowReal();
    }

    private void OnEnable()
    {
        if (grabbable != null)
            grabbable.WhenPointerEventRaised += HandlePointerEvent;
    }

    private void OnDisable()
    {
        if (grabbable != null)
            grabbable.WhenPointerEventRaised -= HandlePointerEvent;
    }

    private void HandlePointerEvent(PointerEvent evt)
    {
        switch (evt.Type)
        {
            case PointerEventType.Select:
                OnGrabbed();
                break;
            case PointerEventType.Unselect:
            case PointerEventType.Cancel:
                OnReleased();
                break;
        }
    }

    // ---- Colour helpers -------------------------------------------------

    public Color RealColor
    {
        get
        {
            switch (bookColor)
            {
                case BookColor.Green: return greenPreset;
                case BookColor.Blue: return bluePreset;
                default: return redPreset;
            }
        }
    }

    private void ApplyColor(Color c)
    {
        if (bookRenderer == null) return;
        if (block == null) block = new MaterialPropertyBlock();

        bookRenderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, c);
        block.SetColor(ColorId, c);
        bookRenderer.SetPropertyBlock(block);
    }

    // Table / shelf: the material exactly as authored, no tint
    private void ShowReal()
    {
        if (bookRenderer == null) return;
        bookRenderer.SetPropertyBlock(null);
        if (originalMaterial != null) bookRenderer.sharedMaterial = originalMaterial;
    }

    // Held: full white, no cover color
    private void ShowHeld()
    {
        if (bookRenderer == null) return;
        bookRenderer.SetPropertyBlock(null);
        if (heldMaterial != null) bookRenderer.sharedMaterial = heldMaterial;
    }
    private List<Color> GetFullFlickerPalette()
    {
        List<Color> palette = new List<Color>
        {
            greenPreset,
            bluePreset,
            redPreset,
            heldColor
        };

        if (extraGlitchColors != null && extraGlitchColors.Length > 0)
        {
            palette.AddRange(extraGlitchColors);
        }

        return palette;
    }

    // ---- Grab events ----------------------------------------------------

    public void OnGrabbed()
    {
        if (isShelved) return;

        isHeld = true;
        onTable = false;
        StopFlicker();

        if (keepColorWhenHeld)
            ShowReal();                                   // VS2: keep the authored material
        else if (flickerOnGrab)
            flickerRoutine = StartCoroutine(FlickerThroughAllColors());
        else
            ShowHeld(); 

        Grabbed?.Invoke(this);
    }

    public void OnReleased()
    {
        isHeld = false;
        StopFlicker();

        if (onTable || keepColorWhenHeld) ShowReal(); else ShowHeld();
        Released?.Invoke(this);
    }

    // ---- Called by shelves ----------------------------------------------

    public void PlaceOnShelf(Transform snapPoint)
    {
        isShelved = true;
        isHeld = false;
        StopFlicker();

        if (grabbable != null) grabbable.enabled = false;

        body.isKinematic = true;
#if UNITY_6000_0_OR_NEWER
        body.linearVelocity = Vector3.zero;
#else
        body.velocity = Vector3.zero;
#endif
        body.angularVelocity = Vector3.zero;

        if (snapPoint != null)
        {
            transform.SetPositionAndRotation(snapPoint.position, snapPoint.rotation);
        }

        ShowReal();
        Shelved?.Invoke(this);
    }


    // ---- Table detection ------------------------------------------------

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Table"))
        {
            onTable = true;
            if (!isHeld && !isShelved) ShowReal();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Table")) onTable = false;
    }

    // ---- Flicker (low-level behaviour) ------------------------------------

    private IEnumerator FlickerThroughAllColors()
    {
        ShowHeld(); // switch to the white material first, then tint it
        float t = 0f;
        List<Color> palette = GetFullFlickerPalette();
        Color lastColor = RealColor;

        while (t < flickerDuration)
        {
            // Losujemy kolor z palety tak, aby nie powtórzyæ tego samego pod rz¹d
            Color nextColor;
            int attempts = 0;
            do
            {
                nextColor = palette[Random.Range(0, palette.Count)];
                attempts++;
            }
            while (nextColor == lastColor && attempts < 10);

            lastColor = nextColor;
            ApplyColor(nextColor);

            yield return new WaitForSeconds(flickerInterval);
            t += flickerInterval;
        }

        ShowHeld();
        flickerRoutine = null;
    }

    private void StopFlicker()
    {
        if (flickerRoutine != null)
        {
            StopCoroutine(flickerRoutine);
            flickerRoutine = null;
        }
    }

    // ---- Called by TaskManager at the start of every run -----------------

    public void ResetToStart()
    {
        StopFlicker();
        isHeld = false;
        isShelved = false;
        onTable = true;

        if (grabbable != null) grabbable.enabled = true;

        body.isKinematic = startKinematic;
        transform.SetPositionAndRotation(startPosition, startRotation);
        if (!body.isKinematic)
        {
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = Vector3.zero;
#else
            body.velocity = Vector3.zero;
#endif
            body.angularVelocity = Vector3.zero;
        }

        ShowReal();
    }
}