using System;
using System.Collections;
using UnityEngine;

namespace BiomeRivals.Presentation
{
    public sealed class ImmediateTweenService : ITweenService
    {
        public IEnumerator Move(Transform target, Vector3 destination, float durationSeconds)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            target.position = destination;
            yield break;
        }

        public void Kill(Transform target) { }
    }
}
