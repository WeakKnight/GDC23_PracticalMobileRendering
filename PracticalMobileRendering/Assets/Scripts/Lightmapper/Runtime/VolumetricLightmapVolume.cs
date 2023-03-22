using UnityEditor;
using UnityEngine;

namespace PMRP
{
    [RequireComponent(typeof(BoxCollider))]
    public class VolumetricLightmapVolume : MonoBehaviour
    {
        public float cellSize = 0.5f;

        [MenuItem("GameObject/PMRP/Create Volumetric Lightmap Volume", false, 1)]
        static void Create()
        {
            GameObject go = new GameObject();
            go.name = "Volumetric Lightmap Volume";
            var boxCollider = go.AddComponent<BoxCollider>();
            go.AddComponent<VolumetricLightmapVolume>();

            var bounds = new Bounds();
            var renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            go.transform.position = bounds.center;
            boxCollider.size = bounds.size;
        }

        void OnValidate()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        public Vector3 GetBoundingBoxMin()
        {
            var boxCollider = GetComponent<BoxCollider>();

            var a = boxCollider.bounds.center - 0.5f * boxCollider.transform.forward * boxCollider.size.z * boxCollider.transform.localScale.z - 0.5f * boxCollider.transform.right * boxCollider.size.x * boxCollider.transform.localScale.x - 0.5f * boxCollider.transform.up * boxCollider.size.y * boxCollider.transform.localScale.y;
            a = boxCollider.transform.worldToLocalMatrix * new Vector4(a.x, a.y, a.z, 1.0f);

            var b = boxCollider.bounds.center + 0.5f * boxCollider.transform.forward * boxCollider.size.z * boxCollider.transform.localScale.z + 0.5f * boxCollider.transform.right * boxCollider.size.x * boxCollider.transform.localScale.x + 0.5f * boxCollider.transform.up * boxCollider.size.y * boxCollider.transform.localScale.y;
            b = boxCollider.transform.worldToLocalMatrix * new Vector4(b.x, b.y, b.z, 1.0f);

            return new Vector3(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Min(a.z, b.z));
        }

        public Vector3 GetBoundingBoxMax()
        {
            var boxCollider = GetComponent<BoxCollider>();

            var a = boxCollider.bounds.center - 0.5f * boxCollider.transform.forward * boxCollider.size.z * boxCollider.transform.localScale.z - 0.5f * boxCollider.transform.right * boxCollider.size.x * boxCollider.transform.localScale.x - 0.5f * boxCollider.transform.up * boxCollider.size.y * boxCollider.transform.localScale.y;
            a = boxCollider.transform.worldToLocalMatrix * new Vector4(a.x, a.y, a.z, 1.0f);

            var b = boxCollider.bounds.center + 0.5f * boxCollider.transform.forward * boxCollider.size.z * boxCollider.transform.localScale.z + 0.5f * boxCollider.transform.right * boxCollider.size.x * boxCollider.transform.localScale.x + 0.5f * boxCollider.transform.up * boxCollider.size.y * boxCollider.transform.localScale.y;
            b = boxCollider.transform.worldToLocalMatrix * new Vector4(b.x, b.y, b.z, 1.0f);

            return new Vector3(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), Mathf.Max(a.z, b.z));
        }

        public Matrix4x4 GetLocalToWorldMatrix()
        {
            return transform.localToWorldMatrix;
        }

        public Matrix4x4 GetWorldToLocalMatrix()
        {
            return transform.worldToLocalMatrix;
        }

        public Vector3 GetInvVolumeSize()
        {
            Vector3Int gridSize = GetGridSize();
            return new Vector3(1.0f / (gridSize.x * cellSize), 1.0f / (gridSize.y * cellSize), 1.0f / (6.0f * gridSize.z * cellSize));
        }

        public Vector3Int GetGridSize()
        {
            var boxCollder = GetComponent<BoxCollider>();

            Vector3 extent = boxCollder.bounds.max - boxCollder.bounds.min;
            Vector3 gridSizeFloat = extent / cellSize;
            return new Vector3Int(Mathf.CeilToInt(gridSizeFloat.x), Mathf.CeilToInt(gridSizeFloat.y), Mathf.CeilToInt(gridSizeFloat.z));
        }
    }
}

