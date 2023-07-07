using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.VisualScripting;
using UnityEngine.Rendering;
using GK;

namespace MeshDestroy
{
    public class MeshDestroyMachine : Singleton<MeshDestroyMachine>
    {
        private bool edgeSet = false;
        private Vector3 edgeVertex = Vector3.zero;
        private Vector2 edgeUV = Vector2.zero;
        private Plane edgePlane = new Plane();
        public Transform PlayerTransform;
        public List<Vector2> add_clipped = new List<Vector2>();

        /// <summary>
        /// 爆炸的切割
        /// </summary>
        public void MeshSplit(List<Vector2> clipped, GameObject obj)
        {
            if (clipped.Count == 0)
                return;
            add_clipped = clipped;
            List<Plane> planes = new List<Plane>();
            for (int i = 0; i < clipped.Count; i++)
            {
                var next_i = i == clipped.Count - 1 ? 0 : i + 1;
                Vector3 cur = new Vector3(clipped[i].x, clipped[i].y, 0);
                Vector3 planePoint1 = cur + obj.transform.TransformDirection(Vector3.forward * 10f);
                Vector3 planePoint2 = cur;
                Vector3 planePoint3 = new Vector3(clipped[next_i].x, clipped[next_i].y, 0);
                Vector3 normal = Vector3.Normalize(Vector3.Cross(planePoint1 - planePoint2, planePoint1 - planePoint3));
                Plane plane = new Plane(normal, planePoint2);
                planes.Add(plane);
            }
            SliceMesh(planes, obj);
        }

        public List<MeshPart> DestroyMesh(MeshDestroyAble destroyAble)
        {
            Mesh originMesh = destroyAble.OrigionMesh;
            if (originMesh == null) 
                return null; 
            originMesh.RecalculateBounds();
            MeshPart mainPart = new MeshPart(originMesh);
            List<MeshPart> parts = new List<MeshPart>();
            List<MeshPart> subParts = new List<MeshPart>();

            parts.Add(mainPart);
            PlayerTransform = GameObject.Find("Sphere").transform;
            Bounds bounds = parts[0].bounds;
            Vector3 planePoint1 = bounds.center + PlayerTransform.TransformDirection(Vector3.forward * 10f);
            Vector3 planePoint2 = bounds.center;
            Vector3 planePoint3 = bounds.center + new Vector3(0, 1.0f, 0);
            Vector3 normal = Vector3.Normalize(Vector3.Cross(planePoint1 - planePoint2, planePoint1- planePoint3));
            Plane plane = new Plane(normal, planePoint2);
            for (int c = 0; c < destroyAble.cutCascades; c++)
            {
                for (int i = 0; i < parts.Count; i++)
                {
                    //Bounds bounds = parts[i].bounds;
                    //Plane plane = new Plane(Random.onUnitSphere, bounds.center);
                    subParts.Add(GenerateMesh(parts[i], plane, true));
                    subParts.Add(GenerateMesh(parts[i], plane, false));
                }
                parts = new List<MeshPart>(subParts);
                subParts.Clear();
            }

            for (int i = 0; i < parts.Count; i++)
            {
                CreatePart(destroyAble, parts[i]);
            }

            Destroy(destroyAble.gameObject);
            

            return parts;
        }

        /// <summary>
        /// 切片
        /// </summary>
        public List<MeshPart> SliceMesh(List<Plane> planes, GameObject obj)
        {
            MeshDestroyAble destroyAble = obj.GetComponent<Collider>().GetComponent<MeshDestroyAble>();
            Mesh originalMesh = destroyAble.OrigionMesh;
            if (originalMesh == null) { return null; }
            originalMesh.RecalculateBounds();
            MeshPart mainPart = new MeshPart(originalMesh);

            List<MeshPart> parts = new List<MeshPart>();
            List<MeshPart> subParts = new List<MeshPart>();
            parts.Add(mainPart);
            Debug.Log("planes: " + planes.Count);
            for (int c = 0; c < planes.Count; c++)
            {
                Plane plane = planes[c];
                for (int i = 0; i < parts.Count; i++)
                {
                    subParts.Add(GenerateMesh(parts[i], plane, true));
                    subParts.Add(GenerateMesh(parts[i], plane, false));
                }
                parts = new List<MeshPart>(subParts);
                subParts.Clear();
            }

            for (int i = 0; i < parts.Count; i++)
            {
                CreatePart(destroyAble, parts[i], obj);
            }

            Destroy(destroyAble.gameObject);

            return parts;
        }

        /// <summary>
        /// 平面切割
        /// </summary>
        private MeshPart GenerateMesh(MeshPart original, Plane plane, bool left)
        {
            MeshPart partMesh = new MeshPart() { };
            Ray ray1 = new Ray();
            Ray ray2 = new Ray();

            List<int> triangles = original.Triangles;
            edgeSet = false;

            for (var j = 0; j < triangles.Count; j = j + 3)
            {
                // 判断一个三角形是否被切到
                bool sideA = plane.GetSide(original.Vertices[triangles[j]]) == left;
                bool sideB = plane.GetSide(original.Vertices[triangles[j + 1]]) == left;
                bool sideC = plane.GetSide(original.Vertices[triangles[j + 2]]) == left;

                int sideCount = (sideA ? 1 : 0) + (sideB ? 1 : 0) + (sideC ? 1 : 0);
                if (sideCount == 0) 
                {
                    continue;
                }
                if (sideCount == 3) // 对没被切到的三角形直接给子网格
                {
                    partMesh.AddTriangle(
                        original.Vertices[triangles[j]],
                        original.Vertices[triangles[j + 1]],
                        original.Vertices[triangles[j + 2]],
                        original.Normals[triangles[j]],
                        original.Normals[triangles[j + 1]],
                        original.Normals[triangles[j + 2]],
                        original.UVs[triangles[j]],
                        original.UVs[triangles[j + 1]],
                        original.UVs[triangles[j + 2]]);
                    continue;
                }

                // 合并剖面
                // 判断三角形中单独的点是哪一个
                int singleIndex = sideB == sideC ? 0 : sideA == sideC ? 1 : 2;

                // 求切割点1的位置
                ray1.origin = original.Vertices[triangles[j + singleIndex]];
                Vector3 dir1 = original.Vertices[triangles[j + ((singleIndex + 1) % 3)]] - original.Vertices[triangles[j + singleIndex]];
                ray1.direction = dir1;
                plane.Raycast(ray1, out var enter1);
                float lerp1 = enter1 / dir1.magnitude;

                // 求切割点2的位置
                ray2.origin = original.Vertices[triangles[j + singleIndex]];
                Vector3 dir2 = original.Vertices[triangles[j + ((singleIndex + 2) % 3)]] - original.Vertices[triangles[j + singleIndex]];
                ray2.direction = dir2;
                plane.Raycast(ray2, out var enter2);
                float lerp2 = enter2 / dir2.magnitude;

                AddEdge(partMesh, left ? plane.normal * -1f : plane.normal,
                        ray1.origin + ray1.direction.normalized * enter1,
                        ray2.origin + ray2.direction.normalized * enter2,
                        Vector2.Lerp(original.UVs[triangles[j + singleIndex]], original.UVs[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
                        Vector2.Lerp(original.UVs[triangles[j + singleIndex]], original.UVs[triangles[j + ((singleIndex + 2) % 3)]], lerp2));


                if (sideCount == 1)
                {
                    partMesh.AddTriangle(
                        original.Vertices[triangles[j + singleIndex]],
                        ray1.origin + ray1.direction.normalized * enter1,
                        ray2.origin + ray2.direction.normalized * enter2,
                        original.Normals[triangles[j + singleIndex]],
                        Vector3.Lerp(original.Normals[triangles[j + singleIndex]], original.Normals[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
                        Vector3.Lerp(original.Normals[triangles[j + singleIndex]], original.Normals[triangles[j + ((singleIndex + 2) % 3)]], lerp2),
                        original.UVs[triangles[j + singleIndex]],
                        Vector2.Lerp(original.UVs[triangles[j + singleIndex]], original.UVs[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
                        Vector2.Lerp(original.UVs[triangles[j + singleIndex]], original.UVs[triangles[j + ((singleIndex + 2) % 3)]], lerp2));
                }
                else if (sideCount == 2)
                {
                    partMesh.AddTriangle(
                        // Vertices
                        ray1.origin + ray1.direction.normalized * enter1,
                        original.Vertices[triangles[j + ((singleIndex + 1) % 3)]],
                        original.Vertices[triangles[j + ((singleIndex + 2) % 3)]],
                        // normal
                        Vector3.Lerp(original.Normals[triangles[j + singleIndex]], original.Normals[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
                        original.Normals[triangles[j + ((singleIndex + 1) % 3)]],
                        original.Normals[triangles[j + ((singleIndex + 2) % 3)]],
                        // uv
                        Vector2.Lerp(original.UVs[triangles[j + singleIndex]], original.UVs[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
                        original.UVs[triangles[j + ((singleIndex + 1) % 3)]],
                        original.UVs[triangles[j + ((singleIndex + 2) % 3)]]);

                    partMesh.AddTriangle(
                        // Vertices
                        ray1.origin + ray1.direction.normalized * enter1,
                        original.Vertices[triangles[j + ((singleIndex + 2) % 3)]],
                        ray2.origin + ray2.direction.normalized * enter2,
                        // normal
                        Vector3.Lerp(original.Normals[triangles[j + singleIndex]], original.Normals[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
                        original.Normals[triangles[j + ((singleIndex + 2) % 3)]],
                        Vector3.Lerp(original.Normals[triangles[j + singleIndex]], original.Normals[triangles[j + ((singleIndex + 2) % 3)]], lerp2),
                        // uv
                        Vector2.Lerp(original.UVs[triangles[j + singleIndex]], original.UVs[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
                        original.UVs[triangles[j + ((singleIndex + 2) % 3)]],
                        Vector2.Lerp(original.UVs[triangles[j + singleIndex]], original.UVs[triangles[j + ((singleIndex + 2) % 3)]], lerp2));
                }
            }

            return partMesh;
        }

        private void AddEdge(MeshPart meshPart, Vector3 normal, Vector3 vertex1, Vector3 vertex2, Vector2 uv1, Vector2 uv2)
        {
            if (!edgeSet)
            {
                edgeSet = true;
                edgeVertex = vertex1;
                edgeUV = uv1;
            }
            else
            {
                edgePlane.Set3Points(edgeVertex, vertex1, vertex2);

                meshPart.AddTriangle(
                    edgeVertex, 
                    edgePlane.GetSide(edgeVertex + normal) ? vertex1 : vertex2, 
                    edgePlane.GetSide(edgeVertex + normal) ? vertex2 : vertex1,
                    normal, normal, normal,
                    edgeUV, uv1, uv2);
            }
        }

        private void CreatePart(MeshDestroyAble destroyAble, MeshPart meshPart, GameObject obj = null)
        { 
            GameObject go = new GameObject(destroyAble.name);
            var bs = go.AddComponent<BreakableSurface>();
            bs.Polygon.Clear();
            bs.Polygon.AddRange(add_clipped);
            go.transform.position = destroyAble.transform.position;
            go.transform.rotation = destroyAble.transform.rotation;
            go.transform.localScale = destroyAble.transform.localScale;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.materials = destroyAble.meshRenderer.materials;
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.mesh = meshPart.CreatePartMesh();
            MeshCollider collider = go.AddComponent<MeshCollider>();
            collider.convex = true;

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.AddForceAtPosition(meshPart.bounds.center * destroyAble.explodeForce, go.transform.position);
            var childArea = bs.Area;

            var bs_rb = bs.GetComponent<Rigidbody>();
            var obj_mass = obj.GetComponent<Rigidbody>().mass;
            var area = obj.GetComponent<BreakableSurface>().Area;
            bs_rb.mass = obj_mass * (childArea / area);
            if (destroyAble.inherit)
            {
                go.AddComponent<MeshDestroyAble>().Inherit(destroyAble);
            }
        }

    }
}