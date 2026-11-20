using UnityEngine;
namespace Kepler
{
    public static class GlobeMesh
    {
        // Baked once by the editor. Adjacent tiles on cube edges share positions/normals.
        public static Mesh Create(int n)
        {
            var vertices = new Vector3[6 * (n + 1) * (n + 1)];
            var normals = new Vector3[vertices.Length];
            var indices = new int[36 * n * n];
            int v = 0, t = 0;
            foreach (var axis in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward,
                                         Vector3.back })
            {
                var u = new Vector3(axis.y, axis.z, axis.x);
                var w = Vector3.Cross(axis, u);
                for (int j = 0; j <= n; j++)
                    for (int i = 0; i <= n; i++, v++)
                    {
                        normals[v] = (axis + u * (2f * i / n - 1) + w * (2f * j / n - 1)).normalized;
                        vertices[v] = normals[v] * .5f;
                        if (i < n && j < n)
                        {
                            indices[t++] = v;
                            indices[t++] = v + 1;
                            indices[t++] = v + n + 1;
                            indices[t++] = v + 1;
                            indices[t++] = v + n + 2;
                            indices[t++] = v + n + 1;
                        }
                    }
            }
            var mesh = new Mesh { name = "Unit cube-sphere", vertices = vertices, normals = normals,
                                  triangles = indices };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
