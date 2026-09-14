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
            foreach (var track in _tracks)
            {
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
