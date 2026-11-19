using UnityEngine;

public class FidgetToy : MonoBehaviour
{
    [SerializeField] private AudioSource activateSound; // pen click / ball squeak

    [Header("Feedback")]
    [SerializeField] private Renderer toyRenderer;
    [SerializeField] private Color activeColor = new Color(0.3f, 1f, 0.4f);
    [Tooltip("Seconds to fade back to the original color")]
    [SerializeField] private float fadeBackTime = 0.4f;

    private Material mat;
    private int colorProp;
    private Color baseColor;
    private float flashTimer;

    private bool isHeld;

    private void Awake()
    {
        if (toyRenderer == null) toyRenderer = GetComponentInChildren<Renderer>();
        mat = toyRenderer.material; // instance, so other objects are not affected

        // URP/Lit uses _BaseColor, Standard uses _Color
        colorProp = mat.HasProperty("_BaseColor") ? Shader.PropertyToID("_BaseColor") : Shader.PropertyToID("_Color");
        baseColor = mat.GetColor(colorProp);
    }

    // Hook to the wrapper's When Select() / When Unselect()
    public void OnGrabbed() => isHeld = true;
    public void OnReleased() => isHeld = false;

    private void Update()
    {
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(flashTimer / fadeBackTime);
            mat.SetColor(colorProp, Color.Lerp(activeColor, baseColor, t));
        }

        if (!isHeld) return;

        bool triggerPressed =
            OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch) ||
            OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch);

        if (triggerPressed) Activate();
    }

    private void Activate()
    {
        if (activateSound != null) activateSound.Play();
        if (FidgetTracker.Instance != null) FidgetTracker.Instance.ReportFidget();

        flashTimer = fadeBackTime;
        mat.SetColor(colorProp, activeColor);
    }
}