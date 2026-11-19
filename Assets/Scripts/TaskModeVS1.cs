// TaskModeVS1.cs  -  the book task from the ADHD perspective.
//   - a grabbed book flickers through colours and then turns white, so you have to remember its colour
//   - shelving a book can interrupt you with a pop-up; the run timer pauses until you close it

namespace QuietPlease
{
    public class TaskModeVS1 : TaskMode
    {
        bool PopUpsEnabled => Profile == null || Profile.popUps;
        int MaxPopUps => Profile != null ? Profile.maxPopUps : 2;

        protected override void OnBegin()
        {
            foreach (Book book in Task.Books)
            {
                book.keepColorWhenHeld = false;
                book.flickerOnGrab = true;
            }
        }

        public override void OnBookPlaced(Book book, Shelf shelf)
        {
            if (!PopUpsEnabled) return;
            if (Task.BooksPlaced >= Task.BooksTotal) return;   // no interruption after the last book

            PopupManager popups = PopupManager.Instance;
            if (popups == null || popups.ActiveCount >= MaxPopUps) return;

            // The GameManager is captured now, so the pause is always released on the same one.
            GameManager gm = GM;
            PopupCard card = popups.SpawnInFrontOfPlayer(
                "Warning", "Task interrupted! Close this to continue.",
                () => { if (gm != null) gm.PopUpClosed(); });
            if (card != null && gm != null) gm.PopUpOpened();
        }
    }
}
