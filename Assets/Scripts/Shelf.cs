// Shelf.cs  -  put on the "ShelfZone" trigger of each bookshelf (the room builder creates it).
// Bookshelf_1: accepted = Green, Blue.  Bookshelf_2: accepted = Red.
// A book counts as shelved when it is released inside the zone of a shelf that accepts its colour.
// In VS2 the shelf lights up while the player holds a matching book.

using System.Collections.Generic;
using UnityEngine;

namespace QuietPlease
{
    [RequireComponent(typeof(Collider))]
    public class Shelf : VSBehaviour
    {
        public List<BookColor> accepted = new List<BookColor>();
        [Tooltip("Object turned on to highlight the shelf, e.g. a glowing outline mesh or a light")]
        public GameObject highlight;

        readonly HashSet<Book> inside = new HashSet<Book>();
        bool highlightAllowed;

        void Reset() => GetComponent<Collider>().isTrigger = true;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (GM == null) return;
            GM.BookPickedUp += OnBookPickedUp;
            GM.BookReleased += OnBookReleased;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (GM == null) return;
            GM.BookPickedUp -= OnBookPickedUp;
            GM.BookReleased -= OnBookReleased;
        }

        protected override void ApplyProfile(VSProfile p)
        {
            highlightAllowed = p.highlightMatchingShelf;
            SetHighlight(false);
        }

        void OnBookPickedUp(Book b) => SetHighlight(highlightAllowed && accepted.Contains(b.color));

        void OnBookReleased(Book b)
        {
            SetHighlight(false);
            if (inside.Contains(b) && accepted.Contains(b.color)) b.MarkShelved();
        }

        void OnTriggerEnter(Collider other)
        {
            var b = other.GetComponentInParent<Book>();
            if (b != null) inside.Add(b);
        }

        void OnTriggerExit(Collider other)
        {
            var b = other.GetComponentInParent<Book>();
            if (b != null) inside.Remove(b);
        }

        void SetHighlight(bool on) { if (highlight) highlight.SetActive(on); }
    }
}
