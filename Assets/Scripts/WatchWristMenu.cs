using QuietPlease;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

[DisallowMultipleComponent]
public sealed class WatchWristMenu : MonoBehaviour
{
    private enum ActionMode
    {
        StartProfile,
        QuitApplication
    }

    [Serializable]
    private sealed class WatchButtonAction
    {
        [SerializeField] private string buttonName;
        [SerializeField] private Sprite icon;
        [SerializeField] private ActionMode action = ActionMode.StartProfile;
        [SerializeField] private VSProfile profile;

        [NonSerialized] public GameObject ButtonObject;
        [NonSerialized] public Collider TouchCollider;
        [NonSerialized] public Image IconImage;            // set by ApplyIcon, used to dim inactive VS buttons
        [NonSerialized] public SpriteRenderer IconSprite;
        [SerializeField] private Vector3 controllerTipOffset = new Vector3(0f, -0.01f, 0.05f); // tune in Inspector


        public WatchButtonAction(string name) => buttonName = name;
        public string ButtonName => buttonName;
        public Sprite Icon => icon;
        public VSProfile Profile => action == ActionMode.StartProfile ? profile : null;

        public void Execute(WatchWristMenu context)
        {
            Debug.Log($"[Watch] Pressed {buttonName}; action={action}.", context);

            if (action == ActionMode.QuitApplication)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                return;
            }

            if (profile == null)
            {
                Debug.LogWarning($"[Watch] No VSProfile assigned to {buttonName}.", context);
                return;
            }

            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[Watch] No GameManager in the scene.", context);
                return;
            }

            GameManager.Instance.StartVS(profile);
        }
    }

    [Header("Buttons")]
    [Tooltip("Each entry has an icon and either starts a VS profile or quits the game. " +
             "Button Name must match a child object of the watch.")]
    [SerializeField]
    private WatchButtonAction[] buttons =
    {
        new WatchButtonAction("button1"),
        new WatchButtonAction("button2")
    };

    [Header("Wrist placement")]
    [SerializeField] private Vector3 positionOffset = new Vector3(0f, 0.025f, 0f);
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;
    [SerializeField, Min(0.01f)] private float positionSmoothTime = 0.045f;
    [SerializeField, Min(0f)] private float rotationSharpness = 24f;

    [Header("Watch-view gesture")]
    [Tooltip("The wrist must be no lower than this distance below the HMD.")]
    [SerializeField, Min(0.1f)] private float maximumHeightBelowHmd = 0.62f;
    [SerializeField, Min(0.1f)] private float maximumDistanceFromHmd = 0.9f;
    [SerializeField, Range(5f, 80f)] private float maximumViewAngle = 38f;
    [Tooltip("The plane of the hand must face the HMD within this angle.")]
    [SerializeField, Range(10f, 85f)] private float maximumHandPlaneAngle = 48f;
    [SerializeField, Min(0f)] private float showHoldTime = 0.16f;
    [SerializeField, Min(0f)] private float hideHoldTime = 0.35f;

    [Header("Generated 3D icons")]
    [Tooltip("Used only when a button has no UI Image or assigned SpriteRenderer child.")]
    [SerializeField, Min(0f)] private float iconSurfaceGap = 0.0015f;
    [SerializeField, Min(0.001f)] private float iconWorldSize = 0.035f;

    [Header("Controller glove touch")]
    [SerializeField, Min(0.001f)] private float fingertipTouchRadius = 0.003f;
    [SerializeField, Min(0.001f)] private float fingertipReleaseRadius = 0.015f;
    [SerializeField, Min(0f)] private float fingertipForwardExtension = 0.01f;
    [SerializeField, Range(0f, 1f)] private float touchHapticAmplitude = 0.3f;
    [SerializeField, Min(0f)] private float touchHapticDuration = 0.05f;

    [Header("Switching")]
    [Tooltip("Seconds after a press during which further presses are ignored, so one poke " +
             "(or a jittery finger re-entering the button) cannot restart the run twice.")]
    [SerializeField, Min(0f)] private float pressCooldown = 0.75f;
    [Tooltip("Tint of the icons of the VS buttons that are NOT the active VS.")]
    [SerializeField] private Color inactiveVSIconTint = new Color(1f, 1f, 1f, 0.35f);

    [Header("Center playing time")]
    [SerializeField] private string centerPanelName = "center";
    [SerializeField] private Color playingTimeColor = new Color(0.12f, 0.62f, 1f, 1f);
    [SerializeField, Min(0.001f)] private float playingTimeScale = 0.1f;

    private readonly List<Renderer> watchRenderers = new List<Renderer>();
    private readonly List<Collider> watchColliders = new List<Collider>();
    private readonly List<Graphic> watchGraphics = new List<Graphic>();
    private readonly List<GameObject> watchContent = new List<GameObject>();
    private readonly List<bool> watchContentInitiallyActive = new List<bool>();
    private readonly HashSet<WatchButtonAction> fingertipContacts = new HashSet<WatchButtonAction>();
    private readonly HashSet<WatchButtonAction> fingertipContactsThisFrame = new HashSet<WatchButtonAction>();
    private OVRCameraRig cameraRig;
    private Camera hmdCamera;
    private Transform leftController;
    private Vector3 followVelocity;
    private float gestureChangedAt;
    private bool gestureCandidate;
    private bool visible;
    private float nextTouchDiagnosticTime;
    private bool fingertipTouchArmed;
    private bool poseInitialized;
    private string lastPoseSource;
    private Vector3 wristSurfaceNormal = Vector3.up;
    private TMP_Text playingTimeText;
    private int lastDisplayedSecond = -1;
    private VSProfile lastDisplayedProfile;
    private bool profileDisplayInitialized;
    private float nextPressAllowedAt;
    private static MethodInfo tryGetControllerHandMethod;
    private static PropertyInfo rightIndexTipProperty;
    private static bool controllerGloveApiResolved;

    private void Awake()
    {
        ResolveTrackingSources();
        ConfigureButtons();
        ConfigurePlayingTime();
        CacheVisualsAndColliders();
        CacheWatchContent();
        SetVisible(false, true);
    }

    private void OnEnable()
    {
        gestureChangedAt = Time.unscaledTime;
        gestureCandidate = false;
        poseInitialized = false;
    }

    private void Update()
    {
        UpdateActiveVS();
        UpdatePlayingTime();

        if (hmdCamera == null || cameraRig == null)
            ResolveTrackingSources();

        if (!TryGetLeftWristPose(out Vector3 wristPosition, out Quaternion wristRotation))
        {
            ReportPoseSource("none");
            UpdateGesture(false);
            return;
        }

        FollowWrist(wristPosition, wristRotation);
        UpdateGesture(IsWatchViewingGesture(wristPosition));
        ProcessRightGloveFingertipTouch();
    }

    // ---- Tracking -------------------------------------------------------------

    private void ResolveTrackingSources()
    {
        cameraRig = FindFirstObjectByType<OVRCameraRig>();

        hmdCamera = null;
        if (cameraRig != null && cameraRig.centerEyeAnchor != null)
            hmdCamera = cameraRig.centerEyeAnchor.GetComponent<Camera>();
        if (hmdCamera == null)
            hmdCamera = Camera.main;

        leftController = FindSceneTransform("Left Controller");
    }

    private void ReportPoseSource(string source)
    {
        if (source == lastPoseSource)
            return;
        lastPoseSource = source;
        Debug.Log($"[Watch] Wrist pose source: {source}.", this);
    }

    // All positions returned here are WORLD space.
    private bool TryGetLeftWristPose(out Vector3 position, out Quaternion rotation)
    {
        // 1) Controller glove bones (already world space), if the glove script exists.
        if (TryGetControllerGloveBone("L_Wrist", out bool gloveTracked, out Transform gloveWrist) &&
            gloveTracked && gloveWrist != null)
        {
            position = gloveWrist.position;
            rotation = gloveWrist.rotation;

            if (TryGetGloveBone("L_IndexMetacarpal", out Transform index) &&
                TryGetGloveBone("L_LittleMetacarpal", out Transform little) &&
                TryGetGloveBone("L_MiddleMetacarpal", out Transform middle) &&
                TryBuildWatchRotation(position, index.position, little.position, middle.position,
                    out Quaternion built))
            {
                rotation = built;
            }

            wristSurfaceNormal = rotation * Vector3.up;
            ReportPoseSource("controller glove");
            return true;
        }

        // 2) Meta camera rig anchors. They are real scene transforms, so their position and
        //    rotation are already in world space and follow the rig when it moves or turns.
        if (cameraRig != null)
        {
            Transform anchor = OVRInput.IsControllerConnected(OVRInput.Controller.LTouch)
                ? cameraRig.leftControllerAnchor
                : cameraRig.leftHandAnchor;
            if (anchor == null)
                anchor = cameraRig.leftHandAnchor;

            if (anchor != null && anchor.gameObject.activeInHierarchy)
            {
                position = anchor.position;
                rotation = anchor.rotation;
                wristSurfaceNormal = rotation * Vector3.up;
                ReportPoseSource("OVRCameraRig " + anchor.name);
                return true;
            }
        }

        // 3) Last resort: a scene object called "Left Controller".
        if (leftController != null && leftController.gameObject.activeInHierarchy)
        {
            position = leftController.position;
            rotation = leftController.rotation;
            wristSurfaceNormal = rotation * Vector3.up;
            ReportPoseSource("scene object 'Left Controller'");
            return true;
        }

        position = default;
        rotation = Quaternion.identity;
        return false;
    }

    private static bool TryGetGloveBone(string boneName, out Transform bone)
    {
        bool available = TryGetControllerGloveBone(boneName, out bool tracked, out bone);
        return available && tracked && bone != null;
    }

    private static void ResolveGloveApi()
    {
        if (controllerGloveApiResolved)
            return;
        controllerGloveApiResolved = true;

        Type visualizerType = FindTypeByName("ControllerGloveVisualizer");
        tryGetControllerHandMethod = visualizerType?.GetMethod(
            "TryGetControllerHand", BindingFlags.Public | BindingFlags.Static);
        rightIndexTipProperty = visualizerType?.GetProperty(
            "RightIndexTip", BindingFlags.Public | BindingFlags.Static);

        Debug.Log($"[Watch] Glove API: type found={visualizerType != null}, " +
                  $"method found={tryGetControllerHandMethod != null}, " +
                  $"right index tip found={rightIndexTipProperty != null}.");
    }

    // Finds a type by its simple name in any loaded assembly, with or without a namespace.
    private static Type FindTypeByName(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            string assemblyName = assembly.GetName().Name;
            if (assembly.IsDynamic ||
                assemblyName.StartsWith("System", StringComparison.Ordinal) ||
                assemblyName.StartsWith("Unity", StringComparison.Ordinal) ||
                assemblyName.StartsWith("Mono", StringComparison.Ordinal) ||
                assemblyName.StartsWith("Microsoft", StringComparison.Ordinal) ||
                assemblyName == "mscorlib" || assemblyName == "netstandard")
                continue;

            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException exception) { types = exception.Types; }
            catch (Exception) { continue; }

            foreach (Type type in types)
                if (type != null && type.Name == typeName)
                    return type;
        }
        return null;
    }

    private static bool TryGetControllerGloveBone(string boneName, out bool tracked, out Transform bone)
    {
        tracked = false;
        bone = null;
        ResolveGloveApi();

        if (tryGetControllerHandMethod == null)
            return false;

        object[] arguments = { true, boneName, false, 0f, null };
        try
        {
            bool available = (bool)tryGetControllerHandMethod.Invoke(null, arguments);
            tracked = arguments[2] is bool value && value;
            bone = arguments[4] as Transform;
            return available;
        }
        catch (TargetInvocationException exception)
        {
            Debug.LogWarning($"[Watch] Controller glove API failed: {exception.InnerException?.Message ?? exception.Message}");
            return false;
        }
    }

    private bool TryBuildWatchRotation(Vector3 wrist, Vector3 index, Vector3 little, Vector3 middle,
        out Quaternion rotation)
    {
        Vector3 towardFingers = middle - wrist;
        Vector3 indexToLittle = little - index;
        Vector3 handNormal = Vector3.Cross(towardFingers, indexToLittle);
        if (towardFingers.sqrMagnitude < 0.000001f || handNormal.sqrMagnitude < 0.000001f)
        {
            rotation = Quaternion.identity;
            return false;
        }

        towardFingers.Normalize();
        handNormal.Normalize();

        // Local Z follows the fingers and local Y is perpendicular to the back of the hand.
        rotation = Quaternion.LookRotation(towardFingers, handNormal);
        return true;
    }

    private void FollowWrist(Vector3 wristPosition, Quaternion wristRotation)
    {
        Quaternion displayRotation = wristRotation;
        if (hmdCamera != null)
        {
            Vector3 towardHmd = hmdCamera.transform.position - wristPosition;
            if (towardHmd.sqrMagnitude > 0.0001f &&
                Vector3.Dot(displayRotation * Vector3.up, towardHmd) < 0f)
            {
                // Preserve the finger direction (local Z), but turn the face of the
                // watch from the palm side to the side visible to the user.
                displayRotation *= Quaternion.Euler(0f, 0f, 180f);
            }
        }

        Quaternion targetRotation = displayRotation * Quaternion.Euler(rotationOffset);
        Vector3 targetPosition = wristPosition + displayRotation * positionOffset;
        if (!poseInitialized)
        {
            transform.SetPositionAndRotation(targetPosition, targetRotation);
            poseInitialized = true;
            return;
        }

        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref followVelocity,
            positionSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        float blend = 1f - Mathf.Exp(-rotationSharpness * Time.unscaledDeltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, blend);
    }

    // ---- Show / hide gesture --------------------------------------------------

    private bool IsWatchViewingGesture(Vector3 wristPosition)
    {
        if (hmdCamera == null)
            return false;

        Vector3 fromHmd = wristPosition - hmdCamera.transform.position;
        float distance = fromHmd.magnitude;
        if (distance < 0.12f || distance > maximumDistanceFromHmd ||
            wristPosition.y < hmdCamera.transform.position.y - maximumHeightBelowHmd)
            return false;

        float angle = Vector3.Angle(hmdCamera.transform.forward, fromHmd / distance);
        if (angle > maximumViewAngle)
            return false;

        Vector3 wristToHmd = -fromHmd / distance;
        float planeFacing = Mathf.Abs(Vector3.Dot(wristSurfaceNormal.normalized, wristToHmd));
        return planeFacing >= Mathf.Cos(maximumHandPlaneAngle * Mathf.Deg2Rad);
    }

    private void UpdateGesture(bool candidate)
    {
        if (candidate != gestureCandidate)
        {
            gestureCandidate = candidate;
            gestureChangedAt = Time.unscaledTime;
        }

        float requiredHold = candidate ? showHoldTime : hideHoldTime;
        if (visible != candidate && Time.unscaledTime - gestureChangedAt >= requiredHold)
            SetVisible(candidate, false);
    }

    // ---- Buttons --------------------------------------------------------------

    private void ConfigureButtons()
    {
        foreach (WatchButtonAction entry in buttons)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.ButtonName))
                continue;

            Transform buttonTransform = FindChildIgnoreCase(transform, entry.ButtonName);
            if (buttonTransform == null)
            {
                Debug.LogWarning($"[Watch] Could not find '{entry.ButtonName}' under '{name}'.", this);
                continue;
            }

            entry.ButtonObject = buttonTransform.gameObject;
            ApplyIcon(entry, buttonTransform);

            Collider collider = buttonTransform.GetComponentInChildren<Collider>(true);
            if (collider == null)
            {
                var touchZone = new GameObject("Watch Touch Collider");
                touchZone.transform.SetParent(buttonTransform, false);
                var box = touchZone.AddComponent<BoxCollider>();
                box.size = new Vector3(0.04f, 0.012f, 0.04f);
                collider = box;
            }
            entry.TouchCollider = collider;
            Debug.Log($"[Watch] Configured {entry.ButtonName}: collider={collider.GetType().Name}, " +
                      $"boundsSize={collider.bounds.size}.", this);

            Button uiButton = buttonTransform.GetComponent<Button>();
            if (uiButton != null)
                uiButton.onClick.AddListener(() => Run(entry));

            // Non-UI buttons are pressed through PressButton() (Meta poke events in the scene)
            // or ProcessRightGloveFingertipTouch() (only when ControllerGloveVisualizer exists).
        }
    }

    // Called by the poke InteractableUnityEventWrappers on the watch buttons.
    public void PressButton(int index)
    {
        if (!visible || index < 0 || index >= buttons.Length || buttons[index] == null)
            return;
        Run(buttons[index]);
    }

    // The single gateway for every press path, so the cooldown covers all of them.
    private void Run(WatchButtonAction entry)
    {
        if (Time.unscaledTime < nextPressAllowedAt)
        {
            Debug.Log($"[Watch] Ignored {entry.ButtonName}: pressed again within {pressCooldown:0.##} s.", this);
            return;
        }
        nextPressAllowedAt = Time.unscaledTime + pressCooldown;
        entry.Execute(this);
    }
    private void ProcessRightGloveFingertipTouch()
    {
        if (!visible)
        {
            fingertipTouchArmed = false;
            fingertipContacts.Clear();
            fingertipContactsThisFrame.Clear();
            return;
        }

        bool hasRightGlove = TryGetControllerGloveBone(string.Empty, out bool tracked, out _);
        Transform fingertip = rightIndexTipProperty?.GetValue(null) as Transform;
        if (!hasRightGlove || !tracked || fingertip == null)
        {
            if (Time.unscaledTime >= nextTouchDiagnosticTime)
            {
                nextTouchDiagnosticTime = Time.unscaledTime + 1f;
                Debug.Log($"[Watch Touch] visible=true, rightGlove={hasRightGlove}, tracked={tracked}, " +
                          $"indexTip={(fingertip != null)}.", this);
            }
            fingertipContacts.Clear();
            fingertipContactsThisFrame.Clear();
            return;
        }

        Vector3 fingertipPosition = fingertip.position;
        Vector3 fingertipSurfacePosition = fingertipPosition;
        Transform distal = fingertip.parent;
        if (distal != null)
        {
            Vector3 fingerDirection = fingertipPosition - distal.position;
            if (fingerDirection.sqrMagnitude > 0.000001f)
                fingertipSurfacePosition += fingerDirection.normalized * fingertipForwardExtension;
        }
        fingertipContactsThisFrame.Clear();
        float nearestDistance = float.PositiveInfinity;
        string nearestButton = "none";
        int activeButtonCount = 0;
        foreach (WatchButtonAction entry in buttons)
        {
            Collider collider = entry?.TouchCollider;
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                continue;

            activeButtonCount++;

            float radius = fingertipContacts.Contains(entry) ? fingertipReleaseRadius : fingertipTouchRadius;
            Vector3 closestToTip = collider.ClosestPoint(fingertipPosition);
            Vector3 closestToSurface = collider.ClosestPoint(fingertipSurfacePosition);
            float distanceSquared = Mathf.Min((closestToTip - fingertipPosition).sqrMagnitude,
                (closestToSurface - fingertipSurfacePosition).sqrMagnitude);
            if (distanceSquared < nearestDistance * nearestDistance)
            {
                nearestDistance = Mathf.Sqrt(distanceSquared);
                nearestButton = entry.ButtonName;
            }
            if (distanceSquared > radius * radius)
                continue;

            fingertipContactsThisFrame.Add(entry);
            if (!fingertipTouchArmed || fingertipContacts.Contains(entry))
                continue;

            fingertipTouchArmed = false;
            fingertipContacts.Add(entry);
            SendRightControllerTouchHaptic();
            Run(entry);
            return;
        }

        if (!fingertipTouchArmed && nearestDistance > fingertipReleaseRadius)
            fingertipTouchArmed = true;

        if (Time.unscaledTime >= nextTouchDiagnosticTime)
        {
            nextTouchDiagnosticTime = Time.unscaledTime + 1f;
            string distanceText = float.IsPositiveInfinity(nearestDistance)
                ? "n/a"
                : $"{nearestDistance:F4}m";
            Debug.Log($"[Watch Touch] visible=true, tracked=true, buttons={activeButtonCount}, " +
                      $"armed={fingertipTouchArmed}, nearest={nearestButton}, distance={distanceText}, " +
                      $"tip={fingertipPosition}.", this);
        }

        fingertipContacts.Clear();
        foreach (WatchButtonAction entry in fingertipContactsThisFrame)
            fingertipContacts.Add(entry);
    }

    private void SendRightControllerTouchHaptic()
    {
        InputDevice controller = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (controller.isValid)
            controller.SendHapticImpulse(0u, touchHapticAmplitude, touchHapticDuration);
    }

    // ---- Icons ----------------------------------------------------------------

    private void ApplyIcon(WatchButtonAction entry, Transform buttonTransform)
    {
        Transform iconTransform = FindChildIgnoreCase(buttonTransform, "icon");
        Image image = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
        if (image == null && buttonTransform is RectTransform buttonRect)
        {
            var iconObject = new GameObject("icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var iconRect = (RectTransform)iconObject.transform;
            iconRect.SetParent(buttonRect, false);
            iconRect.anchorMin = new Vector2(0.15f, 0.15f);
            iconRect.anchorMax = new Vector2(0.85f, 0.85f);
            iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
            image = iconObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
        }

        if (image != null)
        {
            image.sprite = entry.Icon;
            image.enabled = entry.Icon != null;
            entry.IconImage = image;
            return;
        }

        Bounds buttonBounds = GetButtonRendererBounds(buttonTransform, out bool hasButtonBounds);
        SpriteRenderer spriteRenderer = iconTransform != null ? iconTransform.GetComponent<SpriteRenderer>() : null;
        if (spriteRenderer == null)
        {
            var iconObject = new GameObject("icon");
            iconObject.transform.SetParent(buttonTransform, true);
            spriteRenderer = iconObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 50;
        }

        spriteRenderer.sprite = entry.Icon;
        spriteRenderer.enabled = entry.Icon != null;
        entry.IconSprite = spriteRenderer;
        if (entry.Icon != null && hasButtonBounds)
            PlaceIconOnButtonSurface(spriteRenderer, buttonBounds);
    }

    private static Bounds GetButtonRendererBounds(Transform buttonTransform, out bool found)
    {
        found = false;
        Bounds bounds = default;
        foreach (Renderer renderer in buttonTransform.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || renderer is SpriteRenderer &&
                string.Equals(renderer.name, "icon", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return bounds;
    }

    private void PlaceIconOnButtonSurface(SpriteRenderer icon, Bounds buttonBounds)
    {
        // Watch local Y is the anatomical normal pointing away from the back of the hand.
        Vector3 surfaceNormal = transform.up.normalized;
        Vector3 extents = buttonBounds.extents;
        float projectedRadius = Mathf.Abs(surfaceNormal.x) * extents.x +
                                Mathf.Abs(surfaceNormal.y) * extents.y +
                                Mathf.Abs(surfaceNormal.z) * extents.z;
        icon.transform.position = buttonBounds.center + surfaceNormal * (projectedRadius + iconSurfaceGap);

        Vector3 iconUp = Vector3.ProjectOnPlane(transform.forward, surfaceNormal);
        if (iconUp.sqrMagnitude < 0.0001f)
            iconUp = Vector3.ProjectOnPlane(transform.right, surfaceNormal);
        icon.transform.rotation = Quaternion.LookRotation(surfaceNormal, iconUp.normalized) *
                                  Quaternion.Euler(0f, 0f, -90f) *
                                  Quaternion.Euler(0f, 180f, 0f);

        float spriteSize = Mathf.Max(icon.sprite.bounds.size.x, icon.sprite.bounds.size.y);
        Vector3 parentScale = icon.transform.parent != null ? icon.transform.parent.lossyScale : Vector3.one;
        float largestParentScale = Mathf.Max(Mathf.Abs(parentScale.x),
            Mathf.Max(Mathf.Abs(parentScale.y), Mathf.Abs(parentScale.z)));
        float localScale = iconWorldSize / Mathf.Max(0.0001f, spriteSize * largestParentScale);
        icon.transform.localScale = Vector3.one * localScale;
    }

    // ---- Center playing time --------------------------------------------------

    private void ConfigurePlayingTime()
    {
        Transform center = FindChildIgnoreCase(transform, centerPanelName);
        if (center == null)
        {
            Debug.LogWarning($"[Watch] Could not find center panel '{centerPanelName}'.", this);
            return;
        }

        Bounds centerBounds = GetButtonRendererBounds(center, out bool hasBounds);
        Transform existing = FindChildIgnoreCase(center, "Playing Time");
        TextMeshPro text = existing != null ? existing.GetComponent<TextMeshPro>() : null;
        if (text == null)
        {
            var textObject = new GameObject("Playing Time", typeof(RectTransform), typeof(MeshRenderer),
                typeof(TextMeshPro));
            textObject.transform.SetParent(center, true);
            text = textObject.GetComponent<TextMeshPro>();
        }

        playingTimeText = text;
        text.text = "<size=55%>PLAYING TIME</size>\n<size=100%>00:00</size>";
        text.richText = true;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 1.25f;
        text.enableAutoSizing = false;
        text.color = playingTimeColor;
        text.outlineColor = new Color(0.03f, 0.28f, 0.85f, 0.9f);
        text.outlineWidth = 0.16f;
        text.textWrappingMode = TextWrappingModes.NoWrap; // keep the VS name on one line
        text.rectTransform.sizeDelta = new Vector2(6f, 2.8f);
        text.transform.localScale = Vector3.one * playingTimeScale;

        Material glowMaterial = text.fontMaterial;
        if (glowMaterial != null)
        {
            glowMaterial.EnableKeyword("GLOW_ON");
            if (glowMaterial.HasProperty("_GlowColor"))
                glowMaterial.SetColor("_GlowColor", new Color(0.05f, 0.42f, 1f, 0.8f));
            if (glowMaterial.HasProperty("_GlowPower"))
                glowMaterial.SetFloat("_GlowPower", 0.35f);
            if (glowMaterial.HasProperty("_GlowOuter"))
                glowMaterial.SetFloat("_GlowOuter", 0.18f);
        }

        if (hasBounds)
        {
            Vector3 normal = transform.up.normalized;
            Vector3 extents = centerBounds.extents;
            float radius = Mathf.Abs(normal.x) * extents.x + Mathf.Abs(normal.y) * extents.y +
                           Mathf.Abs(normal.z) * extents.z;
            text.transform.position = centerBounds.center + normal * (radius + iconSurfaceGap * 1.15f);
            Vector3 textUp = Vector3.ProjectOnPlane(transform.forward, normal);
            if (textUp.sqrMagnitude < 0.0001f)
                textUp = Vector3.ProjectOnPlane(transform.right, normal);
            text.transform.rotation = Quaternion.LookRotation(normal, textUp.normalized) *
                                      Quaternion.Euler(0f, 0f, -90f) *
                                      Quaternion.Euler(0f, 180f, 0f);
        }
    }

    // Shows the active VS and its run time. The run time comes from the GameManager, so it
    // restarts on every switch, pauses while a pop-up is open and freezes when the task is done.
    // Without a GameManager it falls back to the time since the app started.
    private void UpdatePlayingTime()
    {
        if (playingTimeText == null)
            return;

        GameManager gm = GameManager.Instance;
        bool hasRun = gm != null && gm.Current != null;
        float elapsed = hasRun ? gm.RunTime : Time.realtimeSinceStartup;

        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(elapsed));
        if (totalSeconds == lastDisplayedSecond)
            return;

        lastDisplayedSecond = totalSeconds;
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        string label = hasRun && !string.IsNullOrEmpty(gm.Current.displayName)
            ? gm.Current.displayName.ToUpperInvariant()
            : "PLAYING TIME";
        playingTimeText.text = $"<size=55%>{label}</size>\n<size=100%>{minutes:00}:{seconds:00}</size>";
    }

    // ---- Active VS --------------------------------------------------------------

    // When the VS changes: dim the icons of the VS buttons that are not active and redraw the face.
    private void UpdateActiveVS()
    {
        VSProfile current = GameManager.Instance != null ? GameManager.Instance.Current : null;
        if (profileDisplayInitialized && current == lastDisplayedProfile)
            return;

        profileDisplayInitialized = true;
        lastDisplayedProfile = current;
        lastDisplayedSecond = -1; // forces UpdatePlayingTime to redraw with the new VS name

        foreach (WatchButtonAction entry in buttons)
        {
            if (entry == null || entry.Profile == null)
                continue;
            Color tint = current == null || entry.Profile == current ? Color.white : inactiveVSIconTint;
            if (entry.IconImage != null) entry.IconImage.color = tint;
            if (entry.IconSprite != null) entry.IconSprite.color = tint;
        }
    }

    // ---- Visibility -----------------------------------------------------------

    private void CacheVisualsAndColliders()
    {
        watchRenderers.Clear();
        watchRenderers.AddRange(GetComponentsInChildren<Renderer>(true));
        watchColliders.Clear();
        watchColliders.AddRange(GetComponentsInChildren<Collider>(true));
        watchGraphics.Clear();
        watchGraphics.AddRange(GetComponentsInChildren<Graphic>(true));
    }

    private void CacheWatchContent()
    {
        watchContent.Clear();
        watchContentInitiallyActive.Clear();
        foreach (Transform child in transform)
        {
            watchContent.Add(child.gameObject);
            watchContentInitiallyActive.Add(child.gameObject.activeSelf);
        }
    }

    private void SetVisible(bool value, bool force)
    {
        if (!force && visible == value)
            return;
        visible = value;
        fingertipTouchArmed = false;
        Debug.Log($"[Watch] Visibility changed: {visible}.", this);

        // Keep this root active so it can continue detecting the gesture, but fully
        // deactivate its visual/interactive children while the watch is hidden.
        for (int index = 0; index < watchContent.Count; index++)
        {
            GameObject content = watchContent[index];
            if (content != null)
                content.SetActive(value && watchContentInitiallyActive[index]);
        }

        foreach (Renderer renderer in watchRenderers)
            if (renderer != null) renderer.enabled = value;
        foreach (Collider collider in watchColliders)
            if (collider != null) collider.enabled = value;
        foreach (Graphic graphic in watchGraphics)
            if (graphic != null) graphic.enabled = value;
    }

    // ---- Helpers --------------------------------------------------------------

    private static Transform FindChildIgnoreCase(Transform root, string childName)
    {
        foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            if (string.Equals(candidate.name, childName, StringComparison.OrdinalIgnoreCase))
                return candidate;
        return null;
    }

    private static Transform FindSceneTransform(string objectName)
    {
        foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (string.Equals(candidate.name, objectName, StringComparison.OrdinalIgnoreCase))
                return candidate;
        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Migrate the original display scale to the requested 10x larger size.
        if (Mathf.Approximately(playingTimeScale, 0.01f))
            playingTimeScale = 0.1f;

        if (buttons == null || buttons.Length == 0)
        {
            buttons = new[]
            {
                new WatchButtonAction("button1"),
                new WatchButtonAction("button2")
            };
        }
    }
#endif
}
