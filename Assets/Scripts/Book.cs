using System.Collections;
using UnityEngine;
using Oculus.Interaction;

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

    [Header("Behaviour (later driven by VSProfile)")]
    [Tooltip("VS1: briefly flicker the real colour after grabbing.")]
    public bool flickerOnGrab = false;
    public float flickerDuration = 0.6f;
    public float flickerInterval = 0.08f;

    [Header("State (read-only)")]
    [SerializeField] private bool isHeld;
    [SerializeField] private bool isShelved;

    public bool IsHeld => isHeld;
    public bool IsShelved => isShelved;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP / Lit
    private static readonly int ColorId = Shader.PropertyToID("_Color");         // Standard / Built-in
    private MaterialPropertyBlock block;
    private Rigidbody body;
    private Coroutine flickerRoutine;
    private bool onTable = true;

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

        // Updates the color in the Scene View instantly when changed in Inspector
        if (!Application.isPlaying || (!isHeld && onTable))
        {
            ApplyColor(RealColor);
        }
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        grabbable = GetComponent<Grabbable>();

        if (bookRenderer == null) bookRenderer = GetComponentInChildren<Renderer>();
        if (block == null) block = new MaterialPropertyBlock();

        ApplyColor(RealColor);
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

    // ---- Grab events ----------------------------------------------------

    public void OnGrabbed()
    {
        if (isShelved) return;

        isHeld = true;
        onTable = false;
        StopFlicker();

        if (flickerOnGrab)
            flickerRoutine = StartCoroutine(FlickerThenWhite());
        else
            ApplyColor(heldColor);

        Grabbed?.Invoke(this);
    }

    public void OnReleased()
    {
        isHeld = false;
        StopFlicker();

        ApplyColor(onTable ? RealColor : heldColor);

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

        ApplyColor(RealColor);
        Shelved?.Invoke(this);
    }

    public void RejectFromShelf(Vector3 pushDirection, float force = 1.5f)
    {
        //if (isShelved) return;
        //body.AddForce(pushDirection.normalized * force, ForceMode.VelocityChange);
    }

    // ---- Table detection ------------------------------------------------

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Table"))
        {
            onTable = true;
            if (!isHeld && !isShelved) ApplyColor(RealColor);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Table")) onTable = false;
    }

    // ---- Flicker (low-level behaviour) ------------------------------------

    private IEnumerator FlickerThenWhite()
    {
        float t = 0f;
        bool showReal = true;
        while (t < flickerDuration)
        {
            ApplyColor(showReal ? RealColor : heldColor);
            showReal = !showReal;
            yield return new WaitForSeconds(flickerInterval);
            t += flickerInterval;
        }
        ApplyColor(heldColor);
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
}