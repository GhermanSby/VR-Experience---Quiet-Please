// VSBehaviour.cs  -  base class for anything that behaves differently in VS1 and VS2.
// Inherit from it and override ApplyProfile(); it is called at start and whenever the VS changes.

using UnityEngine;

namespace QuietPlease
{
    public abstract class VSBehaviour : MonoBehaviour
    {
        protected GameManager GM => GameManager.Instance;
        protected VSProfile Profile => GM != null ? GM.Current : null;

        protected virtual void OnEnable()
        {
            if (GM == null) return;
            GM.VSChanged += ApplyProfile;
            if (GM.Current != null) ApplyProfile(GM.Current);
        }

        protected virtual void OnDisable()
        {
            if (GM != null) GM.VSChanged -= ApplyProfile;
        }

        protected abstract void ApplyProfile(VSProfile p);
    }
}
