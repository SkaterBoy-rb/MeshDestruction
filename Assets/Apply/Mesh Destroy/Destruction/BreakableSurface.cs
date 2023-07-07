using MeshDestroy;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HullDelaunayVoronoi.Voronoi;
using HullDelaunayVoronoi.Delaunay;
using HullDelaunayVoronoi.Hull;
using HullDelaunayVoronoi.Primitives;

namespace GK {
	public class BreakableSurface : MonoBehaviour {

		public MeshFilter Filter     { get; private set; }
		public MeshRenderer Renderer { get; private set; }
		public MeshCollider Collider { get; private set; }
		public Rigidbody Rigidbody   { get; private set; }
		public GameObject cur;

		public List<Vector2> Polygon = new List<Vector2>();
		public float Thickness = 1.0f;
		public float MinBreakArea = 0.01f;
		public float MinImpactToBreak = 5.0f;

		float _Area = -1.0f;

		int age;

        public int NumberOfVertices = 50;
        public float size_x;
        public float size_y;
        public float size_z;
        public int seed = 0;

        private VoronoiMesh3 voronoi;

        private List<Mesh> meshes;

		public Mesh curMesh = null;
		public Vector3 curPosition = new Vector3();

        public float Area {
			get {
				if (_Area < 0.0f) {
					_Area = Geom.Area(Polygon);
				}

				return _Area;
			}
		}

		void Start() {
			age = 0;
            Reload();
        }

		public void Reload() {
			var pos = transform.position;

			if (Filter == null) Filter = GetComponent<MeshFilter>();
			if (Renderer == null) Renderer = GetComponent<MeshRenderer>();
			if (Collider == null) Collider = GetComponent<MeshCollider>();
			if (Rigidbody == null) Rigidbody = GetComponent<Rigidbody>();

			if (Polygon.Count == 0) {
				var scale = 0.5f * transform.localScale;

				Polygon.Add(new Vector2(-scale.x, -scale.y));
				Polygon.Add(new Vector2(scale.x, -scale.y));
				Polygon.Add(new Vector2(scale.x, scale.y));
				Polygon.Add(new Vector2(-scale.x, scale.y));

				Thickness = 2.0f * scale.z;

				transform.localScale = Vector3.one;
			}
			if (curMesh == null)
				curMesh = cur.GetComponent<MeshFilter>().mesh;
			//var mesh = MeshFromPolygon(Polygon, Thickness);

			Filter.sharedMesh = curMesh;
			Collider.sharedMesh = curMesh;
		}

		void FixedUpdate() {
			var pos = transform.position;

			age++;
            if (pos.magnitude > 1000.0f) {
				DestroyImmediate(gameObject);
			}
		}

		void OnCollisionEnter(Collision coll) {
            if (age > 5 && coll.relativeVelocity.magnitude > MinImpactToBreak) {
                var pnt = coll.contacts[0].point;
				//Break((Vector2)transform.InverseTransformPoint(pnt));
				Break3D(pnt);
			}
		}

		static float NormalizedRandom(float mean, float stddev) {
			var u1 = UnityEngine.Random.value;
			var u2 = UnityEngine.Random.value;

			var randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) *
				Mathf.Sin(2.0f * Mathf.PI * u2);

			return mean + stddev * randStdNormal;
		}

		public void Break(Vector2 position) {
			var area = Area;
            if (area > MinBreakArea) {
				var calc = new VoronoiCalculator();
				var clip = new VoronoiClipper();

				var sites = new Vector2[10];

				for (int i = 0; i < sites.Length; i++) {
					var dist = Mathf.Abs(NormalizedRandom(0.5f, 1.0f/2.0f));
					var angle = 2.0f * Mathf.PI * Random.value;

					sites[i] = position + new Vector2(
							dist * Mathf.Cos(angle),
							dist * Mathf.Sin(angle));
				}

				var diagram = calc.CalculateDiagram(sites);

				var clipped = new List<Vector2>();
                Debug.Log("sites: " + sites.Length);
                for (int i = 0; i < sites.Length; i++) {
					clip.ClipSite(diagram, Polygon, i, ref clipped); // 扩展了边界点
                    MeshDestroyMachine.Instance.MeshSplit(clipped, this.gameObject);
					//               if (clipped.Count > 0) {
					//	var newGo = Instantiate(gameObject, transform.parent);

					//	newGo.transform.localPosition = transform.localPosition;
					//	newGo.transform.localRotation = transform.localRotation;

					//	var bs = newGo.GetComponent<BreakableSurface>();

					//	bs.Thickness = Thickness;
					//	bs.Polygon.Clear();
					//	bs.Polygon.AddRange(clipped);

					//	var childArea = bs.Area;

					//	var rb = bs.GetComponent<Rigidbody>();

					//	rb.mass = Rigidbody.mass * (childArea / area);
					//}
					break;
				}
				if (clipped.Count > 0)
				{
                    gameObject.SetActive(false);
                    Destroy(gameObject);
                }
			}
		}

		public void Break3D(Vector3 position)
		{
            curPosition = cur.transform.position;
            Vertex3[] vertices = new Vertex3[NumberOfVertices];
			size_x = cur.transform.localScale.x / 2;
            size_y = cur.transform.localScale.y / 2;
            size_z = cur.transform.localScale.z / 2;
            //Random.InitState(seed);
            for (int i = 0; i < NumberOfVertices; i++)
            {
                float x = size_x * Random.Range(-1.0f, 1.0f);
                float y = size_y * Random.Range(-1.0f, 1.0f);
                float z = size_z * Random.Range(-1.0f, 1.0f);

                vertices[i] = new Vertex3(x, y, z);
            }

            voronoi = new VoronoiMesh3();
            voronoi.Generate(vertices);
            RegionsToMeshes();
            for (int i = 0; i < meshes.Count; i++)
			{
                var newGo = Instantiate(gameObject, transform.parent);
				newGo.transform.localPosition = transform.localPosition;
				newGo.transform.localRotation = transform.localRotation;

				var bs = newGo.GetComponent<BreakableSurface>();
				bs.curMesh = meshes[i];
				bs.age = 0;

                //bs.MinImpactToBreak = 100;
            }
			if (meshes.Count > 0)
			{
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }

        private bool InBound(Vertex3 v)
        {
            if (v.X < -size_x || v.X > size_x) return false;
            if (v.Y < -size_y || v.Y > size_y) return false;
            if (v.Z < -size_z || v.Z > size_z) return false;

            return true;
        }

        private void RegionsToMeshes()
        {

            meshes = new List<Mesh>();

            foreach (VoronoiRegion<Vertex3> region in voronoi.Regions)
            {
                bool draw = true;

                List<Vertex3> verts = new List<Vertex3>();
                foreach (DelaunayCell<Vertex3> cell in region.Cells)
                {
                    if (!InBound(cell.CircumCenter))
                    {
                        draw = false;
                        break;
                    }
                    else
                    {
                        verts.Add(cell.CircumCenter);
                    }
                }

                if (!draw) continue;
                //If you find the convex hull of the voronoi region it
                //can be used to make a triangle mesh.

                ConvexHull3 hull = new ConvexHull3();
                hull.Generate(verts, false);

                List<Vector3> positions = new List<Vector3>();
                List<Vector3> normals = new List<Vector3>();
                List<int> indices = new List<int>();

                for (int i = 0; i < hull.Simplexs.Count; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        Vector3 v = new Vector3();
                        v.x = hull.Simplexs[i].Vertices[j].X;
                        v.y = hull.Simplexs[i].Vertices[j].Y;
                        v.z = hull.Simplexs[i].Vertices[j].Z;

                        positions.Add(v);
                    }

                    Vector3 n = new Vector3();
                    n.x = hull.Simplexs[i].Normal[0];
                    n.y = hull.Simplexs[i].Normal[1];
                    n.z = hull.Simplexs[i].Normal[2];

                    if (hull.Simplexs[i].IsNormalFlipped)
                    {
                        indices.Add(i * 3 + 2);
                        indices.Add(i * 3 + 1);
                        indices.Add(i * 3 + 0);
                    }
                    else
                    {
                        indices.Add(i * 3 + 0);
                        indices.Add(i * 3 + 1);
                        indices.Add(i * 3 + 2);
                    }

                    normals.Add(n);
                    normals.Add(n);
                    normals.Add(n);
                }

                Mesh mesh = new Mesh();
                mesh.SetVertices(positions);
                mesh.SetNormals(normals);
                mesh.SetTriangles(indices, 0);

                mesh.RecalculateBounds();
                //mesh.RecalculateNormals();

                meshes.Add(mesh);

            }

        }

        static Mesh MeshFromPolygon(List<Vector2> polygon, float thickness) {
			var count = polygon.Count;
			var verts = new Vector3[6 * count];
			var norms = new Vector3[6 * count];
			var tris = new int[3 * (4 * count - 4)];
			// UV 不知道怎么加

			var vi = 0;
			var ni = 0;
			var ti = 0;

			var ext = 0.5f * thickness;

			// Top
			for (int i = 0; i < count; i++) {
				verts[vi++] = new Vector3(polygon[i].x, polygon[i].y, ext);
				norms[ni++] = Vector3.forward;
			}

			// Bottom
			for (int i = 0; i < count; i++) {
				verts[vi++] = new Vector3(polygon[i].x, polygon[i].y, -ext);
				norms[ni++] = Vector3.back;
			}

			// Sides
			for (int i = 0; i < count; i++) {
				var iNext = i == count - 1 ? 0 : i + 1;

				verts[vi++] = new Vector3(polygon[i].x, polygon[i].y, ext);
				verts[vi++] = new Vector3(polygon[i].x, polygon[i].y, -ext);
				verts[vi++] = new Vector3(polygon[iNext].x, polygon[iNext].y, -ext);
				verts[vi++] = new Vector3(polygon[iNext].x, polygon[iNext].y, ext);

				var norm = Vector3.Cross(polygon[iNext] - polygon[i], Vector3.forward).normalized;

				norms[ni++] = norm;
				norms[ni++] = norm;
				norms[ni++] = norm;
				norms[ni++] = norm;
			}


			for (int vert = 2; vert < count; vert++) {
				tris[ti++] = 0;
				tris[ti++] = vert - 1;
				tris[ti++] = vert;
			}

			for (int vert = 2; vert < count; vert++) {
				tris[ti++] = count;
				tris[ti++] = count + vert;
				tris[ti++] = count + vert - 1;
			}

			for (int vert = 0; vert < count; vert++) {
				var si = 2*count + 4*vert;

				tris[ti++] = si;
				tris[ti++] = si + 1;
				tris[ti++] = si + 2;

				tris[ti++] = si;
				tris[ti++] = si + 2;
				tris[ti++] = si + 3;
			}

			Debug.Assert(ti == tris.Length);
			Debug.Assert(vi == verts.Length);

			var mesh = new Mesh();


			mesh.vertices = verts;
			mesh.triangles = tris;
			mesh.normals = norms;

			return mesh;
		}
	}
}
