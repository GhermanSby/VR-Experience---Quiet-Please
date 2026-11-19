// TaskMode.cs  -  how the book task behaves in ONE virtual subjectiveness.
// TaskManager owns the task (books, shelves, progress) and holds exactly one TaskMode at a time:
//   TaskModeVS1  -  ADHD perspective
//   TaskModeVS2  -  non-ADHD perspective
// To change what a VS does during the task, edit that VS's file. Numbers and on/off switches
// still come from the VSProfile, so they can be tuned in the Inspector.

namespace QuietPlease
{
    public abstract class TaskMode
    {
        protected TaskManager Task { get; private set; }
        protected VSProfile Profile { get; private set; }   // null when the scene has no GameManager
        protected GameManager GM => GameManager.Instance;

        // Called by TaskManager right after the books and shelves were reset.
        public void Begin(TaskManager task, VSProfile profile)
        {
            Task = task;
            Profile = profile;
            OnBegin();
        }

        // Called when the VS changes; undo anything this mode left in the scene.
        public void End() => OnEnd();

        protected virtual void OnBegin() { }
        protected virtual void OnEnd() { }

        public virtual void OnBookGrabbed(Book book) { }
        public virtual void OnBookReleased(Book book) { }
        public virtual void OnBookPlaced(Book book, Shelf shelf) { }
        public virtual void OnBookRejected(Book book, Shelf shelf) { }
    }
}
