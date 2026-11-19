// TaskModeVS2.cs  -  the book task from the non-ADHD perspective.
//   - a grabbed book keeps its colour, so there is nothing to remember
//   - while you hold a book, the shelf it belongs on lights up
//   - nothing interrupts you

namespace QuietPlease
{
    public class TaskModeVS2 : TaskMode
    {
        bool HighlightEnabled => Profile == null || Profile.highlightMatchingShelf;

        protected override void OnBegin()
        {
            foreach (Book book in Task.Books)
            {
                book.keepColorWhenHeld = true;
                book.flickerOnGrab = false;
            }
        }

        protected override void OnEnd() => ClearHighlights();

        public override void OnBookGrabbed(Book book)
        {
            if (!HighlightEnabled) return;
            foreach (Shelf shelf in Task.Shelves)
                shelf.SetHighlight(shelf.acceptedColors.Contains(book.bookColor));
        }

        public override void OnBookReleased(Book book) => ClearHighlights();
        public override void OnBookPlaced(Book book, Shelf shelf) => ClearHighlights();

        void ClearHighlights()
        {
            foreach (Shelf shelf in Task.Shelves) shelf.SetHighlight(false);
        }
    }
}
