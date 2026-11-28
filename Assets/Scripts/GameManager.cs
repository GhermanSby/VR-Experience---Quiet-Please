// GameManager.cs  -  the administrator of the experience.
// It decides WHICH virtual subjectiveness is active and runs the flow (VS1, then VS2, in the SAME scene).
// It also keeps the things every system shares: run time, attention (focused / drifting) and pop-up pauses.
//
// It does NOT know about books or shelves. The task belongs to TaskManager, which listens to
// VSChanged, picks its VS1 or VS2 implementation, and reports back with
// MarkTaskActivity() and NotifyTaskCompleted().
//
// It also runs the HAND MAPPING of the active VS (controller / hand pose -> virtual hands):
// VS1 = overshoot + inertia, VS2 = 1:1. See the HandMapping class at the bottom of this file.

using System;
using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction.Input;
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

        [Header("Hand mapping (controller / hand pose -> virtual hands)")]
        [Tooltip("On / off, stiffness and damping are per VS and live in the VS profiles. " +
                 "These settings are shared by both VSs.")]
        public HandMapping handMapping = new HandMapping();

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
        public event Action<Book> BookPickedUp;
        public event Action<Book> BookReleased;


        float lastTaskActivity;
        int openPopUps;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            StartVS(vs1);
            StartCoroutine(InstallHandMappingNextFrame());
        }

        IEnumerator InstallHandMappingNextFrame()
        {
            yield return null;   // let the Meta Interaction SDK rig finish its own Start() first
            handMapping.Install(Current);
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            handMapping.Uninstall();
            Instance = null;
        }

        void Update()
        {
            handMapping.Refresh(Current);   // re-read every frame: tune the VS1 profile live in Play mode

            if (Phase != Phase.Playing) return;
            if (!PausedByPopUp) RunTime += Time.deltaTime;

            // Attention: drifting after a while without task activity (VS1 only, if enabled)
            bool drifting = Current.racingClock &&
                            Time.time - lastTaskActivity > Current.idleSecondsBeforeDrift;
            SetAttention(drifting ? Attention.Drifting : Attention.Focused);
        }

        // ---- flow ----------------------------------------------------------------

        // Also the entry point for the wristwatch: call StartVS(vs1) or StartVS(vs2) to switch.
        // A switch always RESTARTS the run in the chosen VS (also when it is the VS already active):
        //   GameManager  - run time 0, pop-up pause cleared, attention focused
        //   TaskManager  - books back on the table, shelves empty, VS1 or VS2 task mode
        //   PopupManager - every open pop-up removed
        //   DigitalClock - back to the start time, racing (VS1) or real time (VS2)
        //   HandMapping  - virtual hands snap back onto the real ones, then overshoot (VS1) or stay 1:1 (VS2)
        public void StartVS(VSProfile profile)
        {
            if (profile == null)
            {
                Debug.LogError("[GameManager] No VS Profile assigned. Create one (Create > Quiet Please > VS Profile) and drag it into vs1 / vs2.");
                return;
            }

            Current = profile;
            RunTime = 0f; openPopUps = 0;
            handMapping.ApplyProfile(profile);
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

        // PopupManager reports every pop-up it opens and closes; the run timer stops while any is open.
        public void PopUpOpened() => openPopUps++;
        public void PopUpClosed() => openPopUps = Mathf.Max(0, openPopUps - 1);

        void SetAttention(Attention a)
        {
            if (a == Attention) return;
            Attention = a;
            AttentionChanged?.Invoke(a);
        }
    }

    // =============================================================================================
    // HAND MAPPING  -  controller / hand pose -> virtual hands
    // =============================================================================================
    //
    // VS1 (profile.handOvershoot on):  the virtual hands follow the real ones through a damped spring,
    //     so they lag behind and then overshoot (inertia). Fast, hurried movements overshoot; slow,
    //     careful ones stay close to 1:1 - impulsive movement, not an unsteady (motor) hand.
    // VS2 (profile.handOvershoot off): the virtual hands ARE the tracked hands (1:1, no added delay).
    //
    // How it works
    //  1. One frame after Start, Install() finds the raw hand and controller data sources of the Meta
    //     Interaction SDK rig (FromOVRHandDataSource, FromOVRControllerDataSource) and inserts an
    //     OvershootHand / OvershootController step right after each one. Everything further down the
    //     chain - hand and controller visuals, grab, hand grab, poke, ray - then uses the virtual pose.
    //  2. It moves the OVRCameraRig hand anchors by the same amount, so what sits on those anchors
    //     (the building-block controller models, the wrist watch) stays with the virtual hands.
    //  3. TryGetFingertip() gives the virtual index fingertip, which TouchCloseButton uses. In VS1 the
    //     fingertip overshoots too, so the small pop-up close button is harder to hit.
    // The camera is never touched: the viewpoint stays 1:1 in both VSs.
    //
    // Spring: x'' = w^2 * (real - x) - 2 * zeta * w * x'   with  w = sqrt(handStiffness), zeta = handDamping.
    // It runs in tracking space, so moving the player rig (thumbstick, snap turn, the reset to the start
    // point) never shakes the hands.

    [Serializable]
    public class HandMapping
    {
        [Tooltip("Wrist rotation follows with at least this damping ratio, so wrists lag a little but do not wobble.")]
        [Range(0f, 2f)] public float rotationDampingFloor = 0.7f;

        [Tooltip("Rotation catches up this many times faster than position.")]
        [Min(0.1f)] public float rotationSpeedScale = 1.5f;

        [Tooltip("The virtual hand never gets further than this from the real hand (metres). 0 = no limit.")]
        [Min(0f)] public float maxPositionOffset = 0.25f;

        [Tooltip("The virtual hand never turns further than this from the real hand (degrees). 0 = no limit.")]
        [Min(0f)] public float maxRotationOffset = 30f;

        [Tooltip("If the real hand jumps further than this in one frame (recentring, tracking glitch), " +
                 "the virtual hand snaps to it instead of flying across the room (metres).")]
        [Min(0.05f)] public float snapDistance = 0.5f;

        [Tooltip("Also move the OVRCameraRig hand anchors, so the building-block controller models and the " +
                 "wrist watch stay with the virtual hands. Off = they stay on the real hands.")]
        public bool moveCameraRigHandAnchors = true;

        [Tooltip("How close the virtual index fingertip must get to a touch button to press it (metres).")]
        [Min(0.002f)] public float fingertipTouchRadius = 0.012f;

        [Tooltip("With controllers there is no tracked finger: the 'fingertip' sits this far in front of " +
                 "the virtual controller's pointer pose (metres).")]
        public float controllerTipForward = 0.02f;

        public bool IsInstalled { get; private set; }
        public bool OvershootActive { get; private set; }
        public SpringSettings Spring => spring;

        SpringSettings spring;
        readonly List<OvershootHand> hands = new List<OvershootHand>();
        readonly List<OvershootController> controllers = new List<OvershootController>();
        OVRCameraRig cameraRig;
        AnchorState leftAnchor, rightAnchor;

        struct AnchorState
        {
            public bool valid;
            public Vector3 position;
            public Quaternion rotation;
        }

        // SDK sources that read the tracker directly. Only these get an overshoot step; anything built
        // on top of them (e.g. controller-driven hands) inherits the overshoot automatically.
        static readonly HashSet<string> RawSourceTypes = new HashSet<string>
        {
            "FromOVRHandDataSource", "FromOpenXRHandDataSource",
            "FromOVRControllerDataSource", "FromUnityXRControllerDataSource",
        };

        // ---- called by GameManager ---------------------------------------------------------

        public void Install(VSProfile current)
        {
            if (IsInstalled) return;
            IsInstalled = true;

            var handReaders = new Dictionary<DataSource<HandDataAsset>, List<DataModifier<HandDataAsset>>>();
            var controllerReaders = new Dictionary<DataSource<ControllerDataAsset>, List<DataModifier<ControllerDataAsset>>>();

            foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (behaviour is OvershootHand || behaviour is OvershootController) continue;

                if (behaviour is DataModifier<HandDataAsset> handReader &&
                    handReader.ModifyDataFromSource is DataSource<HandDataAsset> handSource &&
                    RawSourceTypes.Contains(handSource.GetType().Name))
                {
                    AddReader(handReaders, handSource, handReader);
                }
                else if (behaviour is DataModifier<ControllerDataAsset> controllerReader &&
                         controllerReader.ModifyDataFromSource is DataSource<ControllerDataAsset> controllerSource &&
                         RawSourceTypes.Contains(controllerSource.GetType().Name))
                {
                    AddReader(controllerReaders, controllerSource, controllerReader);
                }
            }

            foreach (var pair in handReaders)
            {
                OvershootHand hand = InsertAfter<OvershootHand, HandDataAsset>(pair.Key, pair.Value);
                hand.Mapping = this;
                hands.Add(hand);
            }
            foreach (var pair in controllerReaders)
            {
                OvershootController controller = InsertAfter<OvershootController, ControllerDataAsset>(pair.Key, pair.Value);
                controller.Mapping = this;
                controllers.Add(controller);
            }

            cameraRig = UnityEngine.Object.FindAnyObjectByType<OVRCameraRig>();
            if (cameraRig != null) cameraRig.UpdatedAnchors += OnUpdatedAnchors;

            if (hands.Count + controllers.Count == 0)
                Debug.LogWarning("[HandMapping] No Meta Interaction SDK hand/controller sources found - the virtual hands stay 1:1.");
            else
                Debug.Log($"[HandMapping] Installed on {hands.Count} hand and {controllers.Count} controller source(s).");

            Refresh(current);
        }

        public void Uninstall()
        {
            if (!IsInstalled) return;
            if (cameraRig != null) cameraRig.UpdatedAnchors -= OnUpdatedAnchors;
            OvershootActive = false;   // the inserted steps now pass the tracked data through untouched
            ResetFollowers();
        }

        // VS switch = restart: the virtual hands snap back onto the real ones.
        public void ApplyProfile(VSProfile profile)
        {
            ResetFollowers();
            Refresh(profile);
        }

        public void Refresh(VSProfile profile)
        {
            bool active = IsInstalled && profile != null && profile.handOvershoot;
            if (active != OvershootActive)
            {
                OvershootActive = active;
                ResetFollowers();
            }
            if (profile == null) return;

            float omega = Mathf.Sqrt(Mathf.Max(1f, profile.handStiffness));
            float zeta = Mathf.Max(0f, profile.handDamping);
            spring.omega = omega;
            spring.zeta = zeta;
            spring.rotationOmega = omega * rotationSpeedScale;
            spring.rotationZeta = Mathf.Max(zeta, rotationDampingFloor);
            spring.maxPositionOffset = maxPositionOffset;
            spring.maxRotationOffset = maxRotationOffset;
        }

        // ---- for other scripts --------------------------------------------------------------

        // World position of the VIRTUAL index fingertip of one hand (with controllers: just in front of
        // the virtual controller). False if that hand / controller is not tracked.
        public bool TryGetFingertip(Handedness side, out Vector3 position)
        {
            foreach (OvershootHand hand in hands)
            {
                if (!IsLive(hand, side) || !hand.IsTrackedDataValid) continue;
                if (hand.GetJointPose(HandJointId.HandIndexTip, out Pose tip))
                {
                    position = tip.position;
                    return true;
                }
            }
            foreach (OvershootController controller in controllers)
            {
                if (!IsLive(controller, side) || !controller.IsConnected) continue;
                if (controller.TryGetPointerPose(out Pose pointer))
                {
                    position = pointer.position + pointer.forward * controllerTipForward;
                    return true;
                }
            }
            position = default;
            return false;
        }

        // ---- internals --------------------------------------------------------------------

        static void AddReader<TData>(Dictionary<DataSource<TData>, List<DataModifier<TData>>> map,
                                     DataSource<TData> source, DataModifier<TData> reader)
            where TData : class, ICopyFrom<TData>, new()
        {
            if (!map.TryGetValue(source, out List<DataModifier<TData>> readers))
                map[source] = readers = new List<DataModifier<TData>>();
            readers.Add(reader);
        }

        // source -> [new step] -> every component that used to read the source directly
        static TStep InsertAfter<TStep, TData>(DataSource<TData> source, List<DataModifier<TData>> readers)
            where TStep : DataModifier<TData>
            where TData : class, ICopyFrom<TData>, new()
        {
            var go = new GameObject($"{typeof(TStep).Name} (runtime)");
            go.SetActive(false);                                   // inject before Awake / Start run
            go.transform.SetParent(source.transform, false);       // lives and dies with its source
            TStep step = go.AddComponent<TStep>();
            step.InjectAllDataModifier(DataSource<TData>.UpdateModeFlags.AfterPreviousStep, source, source, true);
            go.SetActive(true);

            foreach (DataModifier<TData> reader in readers)
            {
                if (reader.Started)
                {
                    reader.ResetSources(step, step, reader.UpdateMode);
                }
                else
                {
                    reader.InjectModifyDataFromSource(step);
                    reader.InjectUpdateAfter(step);
                }
            }
            return step;
        }

        void ResetFollowers()
        {
            foreach (OvershootHand hand in hands)
                if (hand != null) hand.Follower.Reset();
            foreach (OvershootController controller in controllers)
                if (controller != null) controller.Follower.Reset();
            leftAnchor.valid = false;
            rightAnchor.valid = false;
        }

        static bool IsLive(Hand hand, Handedness side)
        {
            if (hand == null || !hand.isActiveAndEnabled) return false;
            HandDataAsset data = hand.GetData();
            return data != null && data.IsDataValid && data.Config != null && data.Config.Handedness == side;
        }

        static bool IsLive(Controller controller, Handedness side)
        {
            if (controller == null || !controller.isActiveAndEnabled) return false;
            ControllerDataAsset data = controller.GetData();
            return data != null && data.IsDataValid && data.Config != null && data.Config.Handedness == side;
        }

        // OVRCameraRig has just set its hand anchors from the tracker: move them onto the virtual hands.
        void OnUpdatedAnchors(OVRCameraRig rig)
        {
            if (!OvershootActive || !moveCameraRigHandAnchors)
            {
                leftAnchor.valid = false;
                rightAnchor.valid = false;
                return;
            }
            MoveAnchor(rig.leftHandAnchor, Handedness.Left, ref leftAnchor);
            MoveAnchor(rig.rightHandAnchor, Handedness.Right, ref rightAnchor);
        }

        void MoveAnchor(Transform anchor, Handedness side, ref AnchorState state)
        {
            if (anchor == null) return;
            Vector3 position = anchor.localPosition;
            Quaternion rotation = anchor.localRotation;

            // OVRCameraRig also raises UpdatedAnchors when it only refreshed the eyes. The hand anchor then
            // still holds the pose we gave it last time - don't move it a second time.
            if (state.valid && position == state.position && rotation == state.rotation) return;

            OvershootFollower follower = ActiveFollower(side);
            if (follower == null)
            {
                state.valid = false;
                return;
            }

            var raw = new Pose(position, rotation);
            Pose moved = follower.Apply(raw, raw);
            anchor.localPosition = moved.position;
            anchor.localRotation = moved.rotation;
            state.valid = true;
            state.position = moved.position;
            state.rotation = moved.rotation;
        }

        // A controller in use wins over hand tracking, as in OVRCameraRig.
        OvershootFollower ActiveFollower(Handedness side)
        {
            foreach (OvershootController controller in controllers)
                if (IsLive(controller, side) && controller.Follower.IsActive()) return controller.Follower;
            foreach (OvershootHand hand in hands)
                if (IsLive(hand, side) && hand.Follower.IsActive()) return hand.Follower;
            return null;
        }
    }

    // ---- the Interaction SDK steps inserted by HandMapping (created at runtime only) ---------------

    // Tracked hand -> virtual hand. Only the wrist (root) and the pointer pose are moved; the finger
    // joints are stored relative to the wrist, so the whole hand moves with it.
    public class OvershootHand : Hand
    {
        public HandMapping Mapping { get; set; }
        public OvershootFollower Follower { get; } = new OvershootFollower();

        protected override void Apply(HandDataAsset data)
        {
            base.Apply(data);

            bool tracked = data.IsDataValid && data.IsConnected && data.IsTracked &&
                           data.RootPoseOrigin != PoseOrigin.None;
            if (Mapping == null || !Mapping.OvershootActive || !tracked)
            {
                Follower.Reset();
                return;
            }

            Pose raw = data.Root;
            data.Root = Follower.Follow(raw, Mapping.Spring, Mapping.snapDistance);
            if (data.PointerPoseOrigin != PoseOrigin.None)
                data.PointerPose = Follower.Apply(raw, data.PointerPose);
        }
    }

    // Tracked controller -> virtual controller. Buttons, trigger and grip values are not touched.
    public class OvershootController : Controller
    {
        public HandMapping Mapping { get; set; }
        public OvershootFollower Follower { get; } = new OvershootFollower();

        protected override void Apply(ControllerDataAsset data)
        {
            base.Apply(data);

            bool tracked = data.IsDataValid && data.IsConnected && data.RootPoseOrigin != PoseOrigin.None;
            if (Mapping == null || !Mapping.OvershootActive || !tracked)
            {
                Follower.Reset();
                return;
            }

            Pose raw = data.RootPose;
            data.RootPose = Follower.Follow(raw, Mapping.Spring, Mapping.snapDistance);
            if (data.PointerPoseOrigin != PoseOrigin.None)
                data.PointerPose = Follower.Apply(raw, data.PointerPose);
        }
    }

    // Turns the real hand pose into the virtual (overshooting) one, stepping the spring once per frame.
    // The SDK can ask for new hand data more than once per frame (OVRCameraRig refreshes the hands in
    // Update and again just before rendering); later requests in the same frame reuse that frame's
    // offset on the newest tracked pose.
    public sealed class OvershootFollower
    {
        // Not asked for a pose for this long (hand hidden, tracking lost)? Restart from the real hand.
        const float MaxGapSeconds = 0.25f;

        readonly PoseSpring spring = new PoseSpring();
        int lastStepFrame = -1;
        double lastStepTime;
        Vector3 lastRawPosition;

        // virtual = real moved by OffsetPosition and turned by OffsetRotation (tracking space)
        public Vector3 OffsetPosition { get; private set; }
        public Quaternion OffsetRotation { get; private set; } = Quaternion.identity;
        public int LastActiveFrame { get; private set; } = -1;

        public bool IsActive(int maxFrameAge = 2) =>
            LastActiveFrame >= 0 && Time.frameCount - LastActiveFrame <= maxFrameAge;

        public void Reset()
        {
            spring.Clear();
            lastStepFrame = -1;
            LastActiveFrame = -1;
            OffsetPosition = Vector3.zero;
            OffsetRotation = Quaternion.identity;
        }

        // raw: the pose straight from the tracker. Returns the virtual pose.
        public Pose Follow(Pose raw, in SpringSettings settings, float snapDistance)
        {
            int frame = Time.frameCount;
            if (frame != lastStepFrame)
            {
                double now = Time.realtimeSinceStartupAsDouble;
                float dt = lastStepFrame < 0 ? 0f : (float)(now - lastStepTime);
                bool jumped = spring.IsInitialised &&
                              (raw.position - lastRawPosition).sqrMagnitude > snapDistance * snapDistance;

                if (!spring.IsInitialised || jumped || dt > MaxGapSeconds)
                    spring.Reset(raw);   // recentre, tracking glitch or long pause: snap, don't fly
                else
                    spring.Step(raw, dt, settings);

                lastStepFrame = frame;
                lastStepTime = now;
                lastRawPosition = raw.position;
                OffsetPosition = spring.Position - raw.position;
                OffsetRotation = spring.Rotation * Quaternion.Inverse(raw.rotation);
            }

            LastActiveFrame = frame;
            return Apply(raw, raw);
        }

        // Moves a pose rigidly attached to the real hand (the hand root itself, the pointer pose, an
        // OVRCameraRig hand anchor) so it stays attached to the virtual hand instead.
        public Pose Apply(Pose rawRoot, Pose attached)
        {
            return new Pose(
                rawRoot.position + OffsetPosition + OffsetRotation * (attached.position - rawRoot.position),
                OffsetRotation * attached.rotation);
        }
    }

    public struct SpringSettings
    {
        public float omega;               // position: natural frequency (rad/s) = sqrt(stiffness)
        public float zeta;                // position: damping ratio (< 1 overshoots)
        public float rotationOmega;       // rotation: natural frequency (rad/s)
        public float rotationZeta;        // rotation: damping ratio
        public float maxPositionOffset;   // metres (0 = no limit)
        public float maxRotationOffset;   // degrees (0 = no limit)
    }

    // A damped spring that chases a target pose (position + rotation). Integrated in small fixed
    // sub-steps, so it behaves the same at 72, 90 or 120 Hz and stays stable through frame hitches.
    // Allocation-free.
    public sealed class PoseSpring
    {
        const float MaxSubStep = 1f / 240f;

        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; } = Quaternion.identity;
        public bool IsInitialised { get; private set; }

        Vector3 velocity;          // m/s
        Vector3 angularVelocity;   // rad/s

        public void Reset(Pose target)
        {
            Position = target.position;
            Rotation = target.rotation;
            velocity = Vector3.zero;
            angularVelocity = Vector3.zero;
            IsInitialised = true;
        }

        public void Clear()
        {
            IsInitialised = false;
            velocity = Vector3.zero;
            angularVelocity = Vector3.zero;
        }

        public void Step(Pose target, float dt, in SpringSettings s)
        {
            if (!IsInitialised) { Reset(target); return; }
            if (dt <= 0f) return;

            int steps = Mathf.CeilToInt(dt / MaxSubStep);
            float h = dt / steps;
            Vector3 x = Position;
            Quaternion q = Rotation;

            for (int i = 0; i < steps; i++)
            {
                // Position (semi-implicit Euler: velocity first, then position).
                Vector3 accel = s.omega * s.omega * (target.position - x) - 2f * s.zeta * s.omega * velocity;
                velocity += accel * h;
                x += velocity * h;

                // Rotation: the same spring on the rotation error, written as a rotation vector.
                Vector3 error = RotationVector(target.rotation * Quaternion.Inverse(q));
                Vector3 angAccel = s.rotationOmega * s.rotationOmega * error
                                   - 2f * s.rotationZeta * s.rotationOmega * angularVelocity;
                angularVelocity += angAccel * h;
                q = Integrate(q, angularVelocity, h);

                ClampPosition(ref x, target.position, s.maxPositionOffset);
                ClampRotation(ref q, target.rotation, s.maxRotationOffset);
            }

            Position = x;
            Rotation = q;
        }

        void ClampPosition(ref Vector3 x, Vector3 target, float maxOffset)
        {
            if (maxOffset <= 0f) return;
            Vector3 offset = x - target;
            float distance = offset.magnitude;
            if (distance <= maxOffset) return;

            Vector3 outward = offset / distance;
            x = target + outward * maxOffset;
            float away = Vector3.Dot(velocity, outward);
            if (away > 0f) velocity -= outward * away;
        }

        void ClampRotation(ref Quaternion q, Quaternion target, float maxDegrees)
        {
            if (maxDegrees <= 0f || Quaternion.Angle(target, q) <= maxDegrees) return;

            q = Quaternion.RotateTowards(target, q, maxDegrees);
            Vector3 awayVector = RotationVector(q * Quaternion.Inverse(target));
            float length = awayVector.magnitude;
            if (length < 1e-6f) return;

            Vector3 axis = awayVector / length;
            float away = Vector3.Dot(angularVelocity, axis);
            if (away > 0f) angularVelocity -= axis * away;
        }

        // Rotation vector (axis * angle in radians), taking the short way round.
        static Vector3 RotationVector(Quaternion r)
        {
            if (r.w < 0f) { r.x = -r.x; r.y = -r.y; r.z = -r.z; r.w = -r.w; }
            Vector3 v = new Vector3(r.x, r.y, r.z);
            float sinHalf = v.magnitude;
            if (sinHalf < 1e-6f) return 2f * v;   // small-angle approximation
            float angle = 2f * Mathf.Atan2(sinHalf, r.w);
            return v * (angle / sinHalf);
        }

        static Quaternion Integrate(Quaternion q, Vector3 angularVelocity, float h)
        {
            float speed = angularVelocity.magnitude;
            if (speed < 1e-9f) return q;
            Quaternion delta = Quaternion.AngleAxis(speed * h * Mathf.Rad2Deg, angularVelocity / speed);
            return Quaternion.Normalize(delta * q);
        }
    }
}
