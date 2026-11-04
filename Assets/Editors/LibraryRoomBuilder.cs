// LibraryRoomBuilder.cs  -  Quiet, please. Studying in two minds
// Put this file in Assets/Editor/ (the folder must be called "Editor").
// Menu: Tools > Quiet Please > Build Library Room
//
// Builds the presented layout:
//   - library room with floor, 4 walls and a CEILING (roof)
//   - window opening in the north wall (+Z), with the street placeholder outside
//   - door opening in the south wall (-Z), leading to a small hall
//   - study table in the middle, Bookshelf 1 (green + blue) on the west wall, Bookshelf 2 (red) on the east wall
//   - empty "Anchor_" objects where the real models go (clock, notebook, pen, ball, books, fan, sound sources, player start)
// Everything has colliders. Replace the grey placeholders by dropping your models as children of the anchors.

using UnityEditor;
using UnityEngine;

public class LibraryRoomBuilder : EditorWindow
{
    float width = 6f, depth = 5f, height = 3f, wall = 0.15f;
    float hallDepth = 2f;
    // window in north wall
    float windowWidth = 2.2f, windowBottom = 0.9f, windowTop = 2.3f;
    // door in south wall
    float doorWidth = 1.0f, doorHeight = 2.1f, doorX = 1.8f;

    const string MatFolder = "Assets/QuietPlease/Materials";

    [MenuItem("Tools/Quiet Please/Build Library Room")]
    static void Open() => GetWindow<LibraryRoomBuilder>("Library Room Builder");

    void OnGUI()
    {
        GUILayout.Label("Room (metres)", EditorStyles.boldLabel);
        width = EditorGUILayout.FloatField("Width (X)", width);
        depth = EditorGUILayout.FloatField("Depth (Z)", depth);
        height = EditorGUILayout.FloatField("Ceiling height", height);
        wall = EditorGUILayout.FloatField("Wall thickness", wall);
        hallDepth = EditorGUILayout.FloatField("Hall depth", hallDepth);
        GUILayout.Space(6);
        GUILayout.Label("Window (north wall)", EditorStyles.boldLabel);
        windowWidth = EditorGUILayout.FloatField("Width", windowWidth);
        windowBottom = EditorGUILayout.FloatField("Sill height", windowBottom);
        windowTop = EditorGUILayout.FloatField("Top height", windowTop);
        GUILayout.Space(6);
        GUILayout.Label("Door (south wall)", EditorStyles.boldLabel);
        doorWidth = EditorGUILayout.FloatField("Width", doorWidth);
        doorHeight = EditorGUILayout.FloatField("Height", doorHeight);
        doorX = EditorGUILayout.FloatField("Position X", doorX);
        GUILayout.Space(12);
        if (GUILayout.Button("Build Library Room", GUILayout.Height(32))) Build();
    }

    void Build()
    {
        var old = GameObject.Find("LibraryEnvironment");
        if (old != null)
        {
            if (!EditorUtility.DisplayDialog("Library Room Builder", "Replace the existing 'LibraryEnvironment'?", "Replace", "Cancel")) return;
            Undo.DestroyObjectImmediate(old);
        }

        var mFloor = Mat("Floor_Wood", new Color(0.50f, 0.36f, 0.24f));
        var mWall = Mat("Wall_Plaster", new Color(0.90f, 0.87f, 0.80f));
        var mCeil = Mat("Ceiling", new Color(0.95f, 0.94f, 0.92f));
        var mWood = Mat("Furniture_Wood", new Color(0.55f, 0.38f, 0.24f));
        var mHall = Mat("Hall_Wall", new Color(0.78f, 0.80f, 0.82f));
        var mStreet = Mat("Street_Placeholder", new Color(0.35f, 0.37f, 0.40f));
        var mGreen = Mat("Dot_Green", new Color(0.20f, 0.65f, 0.30f));
        var mBlue = Mat("Dot_Blue", new Color(0.20f, 0.40f, 0.85f));
        var mRed = Mat("Dot_Red", new Color(0.85f, 0.20f, 0.20f));

        var root = new GameObject("LibraryEnvironment");
        Undo.RegisterCreatedObjectUndo(root, "Build Library Room");
        var room = Child("Room", root.transform);
        float hw = width / 2f, hd = depth / 2f, t = wall;

        // Floor and ceiling (thin boxes, so both have solid colliders)
        Box("Floor", room, new Vector3(0, -0.05f, 0), new Vector3(width + 2 * t, 0.1f, depth + 2 * t), mFloor);
        Box("Ceiling", room, new Vector3(0, height + 0.05f, 0), new Vector3(width + 2 * t, 0.1f, depth + 2 * t), mCeil);

        // Walls
        var walls = Child("Walls", room);
        WallAlongX("Wall_North", walls, hd + t / 2, 0f, windowWidth, windowBottom, windowTop, mWall);   // window
        WallAlongX("Wall_South", walls, -hd - t / 2, doorX, doorWidth, 0f, doorHeight, mWall);          // door
        Box("Wall_West", walls, new Vector3(-hw - t / 2, height / 2, 0), new Vector3(t, height, depth), mWall);
        Box("Wall_East", walls, new Vector3(hw + t / 2, height / 2, 0), new Vector3(t, height, depth), mWall);

        // Hall behind the door
        var hall = Child("Hall", root.transform);
        float hz = -hd - t - hallDepth / 2f;
        Box("Hall_Floor", hall, new Vector3(0, -0.05f, hz), new Vector3(width + 2 * t, 0.1f, hallDepth), mFloor);
        Box("Hall_Ceiling", hall, new Vector3(0, height + 0.05f, hz), new Vector3(width + 2 * t, 0.1f, hallDepth), mCeil);
        Box("Hall_FarWall", hall, new Vector3(0, height / 2, hz - hallDepth / 2 - t / 2), new Vector3(width + 2 * t, height, t), mHall);
        Box("Hall_WestWall", hall, new Vector3(-hw - t / 2, height / 2, hz), new Vector3(t, height, hallDepth), mHall);
        Box("Hall_EastWall", hall, new Vector3(hw + t / 2, height / 2, hz), new Vector3(t, height, hallDepth), mHall);

        // Street outside the window (placeholder ground strip; put buildings / car paths here)
        var street = Child("Street", root.transform);
        Box("Street_Ground", street, new Vector3(0, -0.6f, hd + 8f), new Vector3(30f, 0.1f, 12f), mStreet);

        // Furniture placeholders
        var furn = Child("Furniture", root.transform);
        var table = Child("StudyTable", furn); table.localPosition = new Vector3(0, 0, 0.3f);
        Box("Top", table, new Vector3(0, 0.725f, 0), new Vector3(1.6f, 0.05f, 0.8f), mWood);
        foreach (var sx in new[] { -0.72f, 0.72f })
            foreach (var sz in new[] { -0.32f, 0.32f })
                Box("Leg", table, new Vector3(sx, 0.35f, sz), new Vector3(0.06f, 0.7f, 0.06f), mWood);

        var shelf1 = Shelf("Bookshelf_1", furn, new Vector3(-hw + 0.25f, 0, 0.2f), 90f, mWood);
        Dot("Dot_Green", shelf1, new Vector3(-0.3f, 1.95f, 0.21f), mGreen);
        Dot("Dot_Blue", shelf1, new Vector3(0.3f, 1.95f, 0.21f), mBlue);
        var shelf2 = Shelf("Bookshelf_2", furn, new Vector3(hw - 0.25f, 0, 0.2f), -90f, mWood);
        Dot("Dot_Red", shelf2, new Vector3(0f, 1.95f, 0.21f), mRed);

        // Anchors: where the real models / systems go
        var anchors = Child("Anchors", root.transform);
        Anchor("Anchor_PlayerStart", anchors, new Vector3(0, 0, -1.2f));
        Anchor("Anchor_Clock", anchors, new Vector3(0.55f, 0.75f, 0.55f));
        Anchor("Anchor_Notebook", anchors, new Vector3(0f, 0.75f, 0.1f));
        Anchor("Anchor_Pen", anchors, new Vector3(0.25f, 0.76f, 0.05f));
        Anchor("Anchor_Ball", anchors, new Vector3(-0.55f, 0.78f, 0.45f));
        Anchor("Anchor_Books", anchors, new Vector3(-0.3f, 0.75f, 0.35f));
        Anchor("Anchor_CeilingFan", anchors, new Vector3(0, height, 0));
        Anchor("Sound_Traffic", anchors, new Vector3(0, 1.5f, hd + 3f));
        Anchor("Sound_Footsteps", anchors, new Vector3(doorX, 1.2f, hz));
        Anchor("Sound_Fan", anchors, new Vector3(0, height - 0.3f, 0));
        Anchor("Sound_Clock", anchors, new Vector3(0.55f, 0.85f, 0.55f));

        foreach (var tr in root.GetComponentsInChildren<Transform>())
            if (!tr.name.StartsWith("Anchor_") && !tr.name.StartsWith("Sound_"))
                GameObjectUtility.SetStaticEditorFlags(tr.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);

        Selection.activeGameObject = root;
        Debug.Log("[Quiet Please] Library room built: room, ceiling, window, door, hall, table, 2 shelves and anchors.");
    }

    // ---- helpers ---------------------------------------------------------------

    // A wall running along X at position z, with a rectangular opening (window or door).
    void WallAlongX(string name, Transform parent, float z, float openX, float openW, float openBottom, float openTop, Material m)
    {
        var w = Child(name, parent);
        float total = width + 2 * wall;
        float left = -total / 2f, right = total / 2f;
        float oL = openX - openW / 2f, oR = openX + openW / 2f;
        // left and right pieces (full height)
        Box("Left", w, new Vector3((left + oL) / 2f, height / 2f, z), new Vector3(oL - left, height, wall), m);
        Box("Right", w, new Vector3((oR + right) / 2f, height / 2f, z), new Vector3(right - oR, height, wall), m);
        // below the opening (skip for doors)
        if (openBottom > 0.01f)
            Box("Below", w, new Vector3(openX, openBottom / 2f, z), new Vector3(openW, openBottom, wall), m);
        // above the opening
        if (openTop < height - 0.01f)
            Box("Above", w, new Vector3(openX, (openTop + height) / 2f, z), new Vector3(openW, height - openTop, wall), m);
    }

    Transform Shelf(string name, Transform parent, Vector3 pos, float yRot, Material m)
    {
        var s = Child(name, parent);
        s.localPosition = pos; s.localRotation = Quaternion.Euler(0, yRot, 0);
        // back, sides, top and 5 shelf boards (1.0 m wide, 2.0 m tall, 0.35 m deep)
        Box("Back", s, new Vector3(0, 1f, -0.16f), new Vector3(1f, 2f, 0.03f), m);
        Box("SideL", s, new Vector3(-0.49f, 1f, 0), new Vector3(0.03f, 2f, 0.35f), m);
        Box("SideR", s, new Vector3(0.49f, 1f, 0), new Vector3(0.03f, 2f, 0.35f), m);
        for (int i = 0; i < 5; i++)
            Box("Board_" + i, s, new Vector3(0, 0.05f + i * 0.45f, 0), new Vector3(0.95f, 0.03f, 0.33f), m);
        // trigger zone for the GameManager / Shelf script
        var zone = new GameObject("ShelfZone");
        zone.transform.SetParent(s, false);
        zone.transform.localPosition = new Vector3(0, 1f, 0.05f);
        var bc = zone.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(1f, 2f, 0.6f);
        return s;
    }

    static void Dot(string name, Transform parent, Vector3 pos, Material m)
    {
        var d = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        d.name = name; Object.DestroyImmediate(d.GetComponent<Collider>());
        d.transform.SetParent(parent, false);
        d.transform.localPosition = pos;
        d.transform.localRotation = Quaternion.Euler(90, 0, 0);
        d.transform.localScale = new Vector3(0.12f, 0.005f, 0.12f);
        d.GetComponent<Renderer>().sharedMaterial = m;
    }

    static Transform Child(string name, Transform parent)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
    }

    static void Anchor(string name, Transform parent, Vector3 pos)
    {
        var a = Child(name, parent); a.localPosition = pos;
    }

    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = pos; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    static Material Mat(string name, Color c)
    {
        if (!AssetDatabase.IsValidFolder("Assets/QuietPlease")) AssetDatabase.CreateFolder("Assets", "QuietPlease");
        if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/QuietPlease", "Materials");
        string path = $"{MatFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            AssetDatabase.CreateAsset(m, path);
        }
        // Existing materials are kept as they are, so textures you assign are never overwritten.
        return m;
    }
}
