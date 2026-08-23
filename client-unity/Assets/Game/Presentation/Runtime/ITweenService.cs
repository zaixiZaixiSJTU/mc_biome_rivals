using System.Collections;
using UnityEngine;

namespace BiomeRivals.Presentation
{
    public interface ITweenService
    {
        IEnumerator Move(Transform target, Vector3 destination, float durationSeconds);
        void Kill(Transform target);
    }
}
