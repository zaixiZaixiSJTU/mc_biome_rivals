using System;
using System.Collections.Generic;
using UnityEngine;

namespace BiomeRivals.Demo
{
    /// <summary>
    /// Lightweight procedural idle animation for voxel entity pieces. Tracks
    /// reference bones built by <see cref="DemoMinecraftEntityModelBuilder"/>
    /// ("Bone_&lt;name&gt;" game objects) and oscillate their local rotation or
    /// position around the bind pose, mimicking vanilla Minecraft's ambient
    /// motion (wing flaps, tail swings, bobbing blaze rods).
    /// </summary>
    public sealed class DemoEntityIdleAnimator : MonoBehaviour
    {
        public sealed class IdleTrackSpec
        {
            public string BoneName;
            /// <summary>Optional source-pixel orbit, relative to its t=0 pose. No pivot or mesh rewriting.</summary>
            public SourceOrbitSpec SourceOrbit;
            /// <summary>Rotation amplitude in degrees; 0 disables rotation.</summary>
            public float AmplitudeDegrees;
            /// <summary>Position amplitude in Unity units; 0 disables offset.</summary>
            public float PositionAmplitude;
            public Vector3 Axis = Vector3.right;
            /// <summary>Oscillation frequency in Hz.</summary>
            public float Frequency = 1f;
            /// <summary>Phase offset in radians.</summary>
            public float Phase;
        }

        /// <summary>Explicit source animation parameters; degrees and pixels, not a Molang interpreter.</summary>
        public sealed class SourceOrbitSpec
        {
            public float Radius, AngularVelocityDegrees, PhaseDegrees;
            public float VerticalBase, VerticalAmplitude = 1f, VerticalVelocityDegrees, VerticalPhaseDegrees;

            public Vector3 EvaluateSourceOffset(float seconds)
            {
                var angle = (PhaseDegrees + AngularVelocityDegrees * seconds) * Mathf.Deg2Rad;
                var vertical = (VerticalPhaseDegrees + VerticalVelocityDegrees * seconds) * Mathf.Deg2Rad;
                return new Vector3(Radius * Mathf.Cos(angle), VerticalBase + VerticalAmplitude * Mathf.Cos(vertical),
                    Radius * Mathf.Sin(angle));
            }
        }

        private sealed class Track
        {
            public readonly Transform Bone;
            public readonly Quaternion InitialRotation;
            public readonly Vector3 InitialPosition;
            public readonly IdleTrackSpec Spec;

            public Track(Transform bone, IdleTrackSpec spec)
            {
                Bone = bone;
                InitialRotation = bone.localRotation;
                InitialPosition = bone.localPosition;
                Spec = spec;
            }
        }

        private readonly List<Track> _tracks = new List<Track>();
        private float _time;

        public int TrackCount => _tracks.Count;

        /// <summary>Attaches an animator under root, resolving "Bone_&lt;name&gt;" transforms. Missing bones are skipped.</summary>
        public static DemoEntityIdleAnimator Attach(GameObject root, IReadOnlyList<IdleTrackSpec> specs)
        {
            if (root == null || specs == null || specs.Count == 0) return null;
            var animator = root.GetComponent<DemoEntityIdleAnimator>();
            if (animator == null) animator = root.AddComponent<DemoEntityIdleAnimator>();
            foreach (var spec in specs)
            {
                var bone = FindChild(root.transform, "Bone_" + spec.BoneName);
                if (bone != null) animator._tracks.Add(new Track(bone, spec));
            }
            return animator._tracks.Count > 0 ? animator : null;
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            for (var index = 0; index < root.childCount; index++)
            {
                var found = FindChild(root.GetChild(index), name);
                if (found != null) return found;
            }
            return null;
        }

        private void Update()
        {
            _time += Time.unscaledDeltaTime;
            ApplyPose();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Deterministic visual audit; unavailable in a production Player.</summary>
        public void SampleForAudit(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            _time = seconds;
            ApplyPose();
        }
#endif

        private void ApplyPose()
        {
            foreach (var track in _tracks)
            {
                if (track.Spec.SourceOrbit != null)
                {
                    var delta = track.Spec.SourceOrbit.EvaluateSourceOffset(_time) - track.Spec.SourceOrbit.EvaluateSourceOffset(0f);
                    track.Bone.localPosition = track.InitialPosition + new Vector3(-delta.x, delta.y, delta.z) / 16f;
                }
                var wave = Mathf.Sin(_time * track.Spec.Frequency * Mathf.PI * 2f + track.Spec.Phase);
                if (track.Spec.AmplitudeDegrees != 0f)
                {
                    track.Bone.localRotation = track.InitialRotation *
                        Quaternion.AngleAxis(wave * track.Spec.AmplitudeDegrees, track.Spec.Axis);
                }
                if (track.Spec.PositionAmplitude != 0f)
                {
                    track.Bone.localPosition = track.InitialPosition +
                        track.Spec.Axis * (wave * track.Spec.PositionAmplitude);
                }
            }
        }
    }
}
