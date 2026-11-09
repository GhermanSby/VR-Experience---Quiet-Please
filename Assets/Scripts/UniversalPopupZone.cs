using UnityEngine;

[RequireComponent(typeof(Collider))]
public class UniversalPopupZone : MonoBehaviour
{
    [Header("Content")]
    [Tooltip("Text key from Localization Table (or raw text if localization unchecked)")]
    [SerializeField] private string messageOrLocalizationKey = "hint_shelf_books";
    [SerializeField] private bool useLocalization = true;

    [Header("Positioning")]
    [Tooltip("Where the popup should hover. If empty, uses this object's position + offset")]
    [SerializeField] private Transform customPopupAnchor;
    [SerializeField] private Vector3 defaultOffset = new Vector3(0, 0.4f, 0);

    [Header("Rules")]
    [Tooltip("Should this hint only appear once, or every time the user approaches?")]
    [SerializeField] private bool triggerOnlyOnce = false;

    [Header("Tags")]
    [SerializeField] private string[] playerTags = new string[] { "Player", "MainCamera" };

    private bool _hasTriggered = false;
    private GameObject _runtimeAnchor;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnlyOnce && _hasTriggered) return;

        if (IsPlayer(other))
        {
            if(customPopupAnchor != null)
            {
                _hasTriggered = true;
                PopupManager.Instance.SpawnAt(customPopupAnchor.position, customPopupAnchor.rotation, "Title", "Text of popup");
            }
            else
            {
                _hasTriggered = true;
                PopupManager.Instance.SpawnInFrontOfPlayer("I'm hungryyyy", "What did I eat for dinner?");
            }
                
        }
    }

    private bool IsPlayer(Collider col)
    {
        for (int i = 0; i < playerTags.Length; i++)
        {
            if (col.CompareTag(playerTags[i]) || (col.transform.parent != null && col.transform.parent.CompareTag(playerTags[i])))
                return true;
        }
        return false;
    }
}