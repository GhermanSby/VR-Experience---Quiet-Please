WATCH WRIST MENU
================

Requirements
------------
- Unity 6 (or a version supporting FindObjectsByType)
- XR Interaction Toolkit 3.x
- XR Hands 1.x
- TextMeshPro
- One active XR Origin with an HMD camera

Import and installation
-----------------------
1. Import WatchWristMenu.unitypackage with Assets > Import Package > Custom Package.
2. Drag WatchWristMenuPackage/Prefabs/Watch.prefab into every destination scene.
3. Keep exactly one Watch instance per scene. Instantiate the prefab through Unity;
   do not paste or hand-edit scene YAML/file IDs.
4. On WatchWristMenu configure button1..button4: icon (optional), Action, and the
   destination Scene or UnityEvent when applicable. Available actions are Load
   Scene, Invoke Event, and Quit Application.
5. Add every Load Scene destination to Build Settings / Build Profiles.
6. Enter Play Mode from a clean restart and test with the target headset.

Button touch
------------
- Controller-glove interaction uses the right index fingertip.
- Each button contains an editable child named "Watch Touch Collider" with a
  BoxCollider. Adjust its Local Position and the collider Center/Size so it matches
  the visible button mesh. Do not use the FBX pivot; it may be outside the geometry.
- A touch is armed only after the fingertip first moves away from the buttons.
- One touch produces one action and a short haptic pulse. The finger must leave the
  release zone before another action can fire.
- Runtime diagnostics use [Watch] and [Watch Touch] in the Unity Console.

Tracking priority
-----------------
1. XR Hands left wrist
2. ControllerGloveVisualizer, when the target project contains it
3. OpenXR left controller pose
4. Scene object named Left Controller

Optional project integration
----------------------------
- ControllerGloveVisualizer is detected without a compile-time dependency. The
  package reads its public RightIndexTip property for controller-glove button touch.
- PlayerFeedback is detected without a compile-time dependency. When it exposes
  SetWatchInteractionActive(bool), scenes named tennis1..tennis5 can replace the
  racket with the right glove while the watch is visible.
- Projects without either class still compile and can use XR Hands/OpenXR tracking.

Quit behavior
-------------
- Quit Application calls Application.Quit in a build.
- In Unity Editor it only stops Play Mode; it never closes the Unity Editor.
- Stopping Play Mode also shuts down XR audio. Assign Quit only to the intended
  button and correctly placed collider.

Validation checklist
--------------------
1. No C# compilation errors.
2. Exactly one active XR Origin and one Watch instance in the scene.
3. Watch appears for the viewing gesture and hides after lowering the wrist.
4. Watch face and icons point toward the user.
5. In tennis scenes the right glove replaces the racket while the watch is visible.
6. Console reports four configured buttons with non-zero collider bounds.
7. Touch each collider with the right fingertip and confirm exactly one [Watch]
   Pressed buttonX message, one haptic pulse, and the assigned action.
8. Confirm audio continues unless the action intentionally stops Play Mode.

The package deliberately does not include destination scenes or scene-object
UnityEvent targets. Assign those references in the destination project.
