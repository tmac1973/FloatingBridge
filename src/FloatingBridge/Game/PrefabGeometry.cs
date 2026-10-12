using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FloatingBridge.Game
{
    /// <summary>Mesh-based measuring and copying. Works on inactive prefabs, where collider and renderer bounds are empty.</summary>
    internal static class PrefabGeometry
    {
        /// <summary>Bounds, in <paramref name="space"/>'s local coordinates, of every mesh under <paramref name="sub"/>.</summary>
        internal static Bounds MeshBounds(Transform space, Transform sub)
        {
            bool any = false;
            var bounds = new Bounds();
            foreach (MeshFilter mf in sub.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null)
                    continue;
                Matrix4x4 toSpace = space.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                Bounds mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? mb.min.x : mb.max.x,
                        (i & 2) == 0 ? mb.min.y : mb.max.y,
                        (i & 4) == 0 ? mb.min.z : mb.max.z);
                    Vector3 p = toSpace.MultiplyPoint3x4(corner);
                    if (!any)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(p);
                    }
                }
            }
            return bounds;
        }

        /// <summary>
        /// A new object under <paramref name="parent"/> holding render-only copies of the meshes under
        /// <paramref name="sub"/>, laid out as they are relative to <paramref name="srcRoot"/>.
        /// </summary>
        internal static GameObject CopyMeshes(Transform srcRoot, Transform sub, Transform parent)
        {
            var copy = new GameObject("meshes");
            copy.layer = parent.gameObject.layer;
            copy.transform.SetParent(parent, false);
            // Without the source's LODGroup every LOD level would render at once, so take only the most detailed.
            // (wood_pole_log has a single level holding both of its meshes, so both are kept.)
            var lodGroup = srcRoot.GetComponent<LODGroup>();
            HashSet<Renderer>? lod0 = lodGroup != null && lodGroup.lodCount > 0
                ? new HashSet<Renderer>(lodGroup.GetLODs()[0].renderers.Where(r => r != null))
                : null;
            if (lod0 != null && !sub.GetComponentsInChildren<MeshRenderer>(true).Any(lod0.Contains))
                lod0 = null;
            foreach (MeshFilter mf in sub.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mf.sharedMesh == null || mr == null || (lod0 != null && !lod0.Contains(mr)))
                    continue;
                var part = new GameObject(mf.name);
                part.layer = copy.layer;
                part.transform.SetParent(copy.transform, false);
                part.transform.localPosition = srcRoot.InverseTransformPoint(mf.transform.position);
                part.transform.localRotation = Quaternion.Inverse(srcRoot.rotation) * mf.transform.rotation;
                part.transform.localScale = mf.transform.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var partRenderer = part.AddComponent<MeshRenderer>();
                partRenderer.sharedMaterials = mr.sharedMaterials;
                partRenderer.shadowCastingMode = mr.shadowCastingMode;
                partRenderer.receiveShadows = mr.receiveShadows;
            }
            return copy;
        }

        /// <summary>Turn and stretch <paramref name="t"/> so its longest side lies along X with the given length.</summary>
        internal static void LayAlongX(Transform t, float length)
        {
            Bounds own = MeshBounds(t, t);
            Vector3 size = own.size;
            Vector3 scale = Vector3.one;
            if (size.y >= size.x && size.y >= size.z)
            {
                scale.y = length / Mathf.Max(size.y, 0.01f);
                t.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            else if (size.z >= size.x)
            {
                scale.z = length / Mathf.Max(size.z, 0.01f);
                t.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            else
            {
                scale.x = length / Mathf.Max(size.x, 0.01f);
                t.localRotation = Quaternion.identity;
            }
            t.localScale = scale;
        }
    }
}
