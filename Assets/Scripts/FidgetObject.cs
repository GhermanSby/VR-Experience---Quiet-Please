using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Oculus.Interaction;

[RequireComponent(typeof(Rigidbody))]
public class FidgetObject : MonoBehaviour
{
    [Header("Settings (VS hooks for later)")]
    public bool isGrabbable = true;
    public bool canFidget = true;

    [Header("Animation")]
    [Tooltip("Animator to trigger when index trigger is pulled.")]
    [SerializeField] private Animator animator;
    [Tooltip("Name of the Trigger parameter in your Animator Controller.")]
    [SerializeField] private string animationTriggerName = "Fidget";

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip fidgetSound;

    [Header("Haptics")]
    [Range(0f, 1f)][SerializeField] private float hapticStrength = 0.7f;
    [Range(0f, 1f)][SerializeField] private float hapticFrequency = 0.8f;
    [SerializeField] private float hapticDuration = 0.08f;

    [Header("Custom Events")]
    [Tooltip("Hook up any extra visual, particle, or counter events here.")]
    public UnityEvent OnFidgeted;

    private Grabbable grabbable;
    private Rigidbody rb;
    private bool isHeld = false;
    private OVRInput.Controller holdingController = OVRInput.Controller.None;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabbable = GetComponent<Grabbable>();

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
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
        if (evt.Type == PointerEventType.Select)
        {
            isHeld = true;
            DetectHand();
        }
        else if (evt.Type == PointerEventType.Unselect || evt.Type == PointerEventType.Cancel)
        {
            isHeld = false;
            holdingController = OVRInput.Controller.None;
        }
    }

    private void DetectHand()
    {
        // Detects whether left or right hand pulled the grip
        if (OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch) > 0.2f)
            holdingController = OVRInput.Controller.RTouch;
        else if (OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.LTouch) > 0.2f)
            holdingController = OVRInput.Controller.LTouch;
        else
            holdingController = OVRInput.Controller.All;
    }

    private void Update()
    {
        if (!isHeld || !canFidget) return;

        // Check index trigger press on whichever controller is holding this object
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, holdingController))
        {
            TriggerFidget();
        }
    }

    private void TriggerFidget()
    {
        // 1. Play Animator trigger (Pen click or Ball squish animation)
        if (animator != null && !string.IsNullOrEmpty(animationTriggerName))
        {
            animator.SetTrigger(animationTriggerName);
        }

        // 2. Play sound
        if (audioSource != null && fidgetSound != null)
        {
            audioSource.PlayOneShot(fidgetSound);
        }

        // 3. Controller vibration
        StartCoroutine(HapticPulse(hapticFrequency, hapticStrength, hapticDuration));

        // 4. Fire event (ready for the Drifting Clock system later)
        OnFidgeted?.Invoke();

        Debug.Log($"<color=yellow>[FidgetItem] Action triggered on: {gameObject.name}</color>");
    }

    private IEnumerator HapticPulse(float frequency, float amplitude, float duration)
    {
        OVRInput.Controller target = holdingController == OVRInput.Controller.None ? OVRInput.Controller.All : holdingController;
        OVRInput.SetControllerVibration(frequency, amplitude, target);
        yield return new WaitForSeconds(duration);
        OVRInput.SetControllerVibration(0f, 0f, target);
    }
}