using UnityEngine;

namespace BiomeRivals.Demo
{
    internal sealed class DemoGeneratedMeshOwner : MonoBehaviour
    {
        private Mesh _mesh;

        public void Configure(Mesh mesh)
        {
            _mesh = mesh;
        }

        private void OnDestroy()
        {
            if (_mesh == null) return;
            if (Application.isPlaying) Destroy(_mesh);
            else DestroyImmediate(_mesh);
        }
    }
}
