using System;
using UnityEngine;
using Kepler.Parts;

namespace Kepler.Rendering
{
    public static class TankMesh
    {
        // Revolved profile: UV seam is duplicated; the ends are closed for convex collision.
        public static Mesh Create(TankShape shape, float radius, float height, int slices, int domeSteps)
        {
            if (radius <= 0 || height <= 0 || slices < 6 || domeSteps < 2)
                throw new ArgumentException("Invalid tank dimensions or mesh resolution");
            float half = height * .5f;
            float dome = shape == TankShape.Cylinder ? 0 : Mathf.Min(radius, half);
            int rings = shape == TankShape.Cylinder ? 4 : domeSteps * 2 + 2;
            var vertices = new Vector3[rings * (slices + 1)];
            var uv = new Vector2[vertices.Length];
            var indices = new int[(rings - 1) * slices * 6];
            for (int j = 0; j < rings; j++)
            {
                float y, r;
                if (shape == TankShape.Cylinder)
                {
                    y = j < 2 ? -half : half;
                    r = (j == 0 || j == rings - 1) ? 0 : radius;
                }
                else if (j <= domeSteps)
                {
                    float a = -Mathf.PI * .5f + (Mathf.PI * .5f) * j / domeSteps;
                    y = -half + dome + Mathf.Sin(a) * dome;
                    r = Mathf.Max(0, Mathf.Cos(a) * radius);
                }
                else
                {
                    float a = (Mathf.PI * .5f) * (j - domeSteps - 1) / domeSteps;
                    y = half - dome + Mathf.Sin(a) * dome;
                    r = Mathf.Max(0, Mathf.Cos(a) * radius);
                }
                for (int i = 0; i <= slices; i++)
                {
                    float a = 2 * Mathf.PI * i / slices;
                    int k = j * (slices + 1) + i;
                    vertices[k] = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                    uv[k] = new Vector2((float)i / slices, (y + half) / height);
                    if (j == rings - 1 || i == slices) continue;
                    int n = (j * slices + i) * 6;
                    indices[n] = k; indices[n + 1] = k + slices + 1; indices[n + 2] = k + 1;
                    indices[n + 3] = k + 1; indices[n + 4] = k + slices + 1; indices[n + 5] = k + slices + 2;
                }
            }
            var mesh = new Mesh { name = "Tank mesh", vertices = vertices, uv = uv, triangles = indices };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
        public static double Volume(TankShape shape, double radius, double height)
        {
            if (radius <= 0 || height <= 0) throw new ArgumentException("Invalid tank dimensions");
            double cap = shape == TankShape.Cylinder ? 0 : Math.Min(radius, height * .5);
            return Math.PI * radius * radius * (height - 2 * cap) + 4 * Math.PI * radius * radius * cap / 3;
        }
    }
}
