using System.Collections.Generic;
using UnityEngine;

public class PopupManager : MonoBehaviour
{
    public static PopupManager Instance { get; private set; }

    [Header("Prefab Reference")]
    [Tooltip("Drag your Popup_Card prefab here from the Project window.")]
    [SerializeField] private GameObject popupPrefab;

    [Header("VR Player Reference")]
    [Tooltip("Camera / CenterEyeAnchor for spawning in front of player gaze.")]
    [SerializeField] private Transform playerHead;

    [Header("Ergonomics & Collision Detection")]
    [SerializeField] private float defaultDistance = 0.65f; // Standard distance in open air
    [SerializeField] private float minDistance = 0.35f;     // Absolute closest to face
    [SerializeField] private float surfacePadding = 0.08f;  // Space between popup and the obstacle (8cm)
    [SerializeField] private float castRadius = 0.2f;      // Thickness of the sphere check

    [Tooltip("Layers that block the popup (Walls, Furniture, Shelves). Do not include Triggers.")]
    [SerializeField] private LayerMask obstacleLayers = ~0; // Default: Everything

    private readonly List<PopupCard> activePopups = new List<PopupCard>();

    public bool HasActivePopups => activePopups.Count > 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (playerHead == null && Camera.main != null)
        {
            playerHead = Camera.main.transform;
        }
    }

    /// <summary>
    /// Spawns a popup at an exact world position and rotation.
    /// </summary>
    public PopupCard SpawnAt(Vector3 position, Quaternion rotation, string title, string message, System.Action onClosed = null)
    {
        if (popupPrefab == null)
        {
            Debug.LogError("[PopupManager] Popup Prefab is not assigned!");
            return null;
        }

        GameObject obj = Instantiate(popupPrefab, position, rotation);
        PopupCard card = obj.GetComponent<PopupCard>();
        card.Initialize(title, message, onClosed);

        activePopups.Add(card);
        return card;
    }

    /// <summary>
    /// Spawns the popup in front of the player, automatically avoiding clipping into furniture/walls.
    /// </summary>
    public PopupCard SpawnInFrontOfPlayer(string title, string message, System.Action onClosed = null)
    {
        if (playerHead == null && Camera.main != null)
        {
            playerHead = Camera.main.transform;
        }

        Vector3 headPos = playerHead.position;
        Vector3 gazeDir = playerHead.forward;

        // Keep it slightly leveled so looking steeply down at the floor doesn't bury it
        Vector3 forwardFlat = new Vector3(gazeDir.x, Mathf.Clamp(gazeDir.y, -0.3f, 0.2f), gazeDir.z).normalized;

        float targetDistance = defaultDistance;

        // 1. Check for physical obstacles in front of the player (ignores trigger colliders!)
        Ray ray = new Ray(headPos, forwardFlat);
        if (Physics.SphereCast(ray, castRadius, out RaycastHit hit, defaultDistance + castRadius, obstacleLayers, QueryTriggerInteraction.Ignore))
        {
            // We hit something (e.g. a shelf or wall)!
            // Pull the popup closer so it sits safely in front of the object
            float obstacleDistance = hit.distance - surfacePadding;
            targetDistance = Mathf.Clamp(obstacleDistance, minDistance, defaultDistance);

            Debug.Log($"[PopupManager] Obstacle detected ({hit.collider.name}). Adjusted distance to: {targetDistance:F2}m");
        }

        // 2. Calculate the safe world position
        Vector3 spawnPos = headPos + (forwardFlat * targetDistance);

        // 3. Face the player with a slight upward tilt for natural reading
        Quaternion spawnRot = Quaternion.LookRotation(forwardFlat) * Quaternion.Euler(15f, 0, 0);

        return SpawnAt(spawnPos, spawnRot, title, message, onClosed);
    }

    public void UnregisterPopup(PopupCard card)
    {
        if (activePopups.Contains(card))
        {
            activePopups.Remove(card);
        }
    }
}