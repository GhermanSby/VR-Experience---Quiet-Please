// Book.cs  -  put on every colour-coded book (together with Meta's grab components).
// Wire Meta's grab events to OnGrabbed() / OnReleased():
//   add "Interactable Unity Event Wrapper" to the book, drag the book's Grab Interactable into it,
//   then add OnGrabbed to "When Select" and OnReleased to "When Unselect".

using UnityEngine;

namespace QuietPlease
{
    public enum BookColor { Green, Blue, Red }

    [RequireComponent(typeof(Rigidbody))]
    public class Book : MonoBehaviour
    {
        public BookColor color;
        public bool IsHeld { get; private set; }
        public bool IsShelved { get; private set; }

        Vector3 startPos; Quaternion startRot; Rigidbody rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            startPos = transform.position; startRot = transform.rotation;
        }

        void Start() => GameManager.Instance?.Register(this);

        public void OnGrabbed()
        {
            if (IsShelved) return;
            IsHeld = true;
            GameManager.Instance?.NotifyBookPickedUp(this);
        }

        public void OnReleased()
        {
            IsHeld = false;
            GameManager.Instance?.NotifyBookReleased(this);
        }

        // Called by Shelf when the book is dropped into the matching shelf zone
        public void MarkShelved()
        {
            if (IsShelved) return;
            IsShelved = true;
            GameManager.Instance?.NotifyBookShelved(this);
        }

        public void ResetToStart()
        {
            IsHeld = false; IsShelved = false;
            rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(startPos, startRot);
        }
    }
}
