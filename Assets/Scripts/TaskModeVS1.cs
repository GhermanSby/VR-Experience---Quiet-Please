// TaskModeVS1.cs  -  the book task from the ADHD perspective.
//   - a grabbed book flickers through colours and then turns white, so you have to remember its colour
//   - TRYING to shelve a book can be interrupted by a pop-up, right before it would land on the shelf:
//     the book is NOT shelved, the run timer pauses, and no shelf accepts a book until the pop-up is
//     closed. Then the player tries again. Each book can be interrupted at most once; the chance per
//     book is VSProfile.shelveInterruptChance.

using System.Collections.Generic;
using UnityEngine;

namespace QuietPlease
{
    public class TaskModeVS1 : TaskMode
    {
        // Title and message of the interrupting pop-ups (examples from the design). Add more freely.
        static readonly (string title, string message)[] Interruptions =
        {
            ("hungry_dinner.exe", "What should I eat for dinner today?"),
            ("Reminder", "Wait... what was the task again?"),
        };

        // Books that already had their one chance of being interrupted in this run
        readonly HashSet<Book> alreadyRolled = new HashSet<Book>();

        bool PopUpsEnabled => Profile == null || Profile.popUps;
        float InterruptChance => Profile != null ? Profile.shelveInterruptChance : 0.5f;

        protected override void OnBegin()
        {
            alreadyRolled.Clear();
            foreach (Book book in Task.Books)
            {
                book.keepColorWhenHeld = false;
                book.flickerOnGrab = true;
            }
        }

        public override bool OnShelveAttempt(Book book, Shelf shelf)
        {
            if (!PopUpsEnabled) return true;
            if (!alreadyRolled.Add(book)) return true;   // this book already had its chance
            if (InterruptChance <= 0f || Random.value > InterruptChance) return true;

            PopupManager popups = PopupManager.Instance;
            if (popups == null) return true;

            var (title, message) = Interruptions[Random.Range(0, Interruptions.Length)];
            PopupCard card = popups.SpawnInFrontOfPlayer(title, message);
            return card == null;   // no pop-up could appear (limit or cooldown): shelve normally
        }
    }
}
