using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSPAPIExtensions;

namespace ProceduralParts
{
    public static class VectorUtils
    {

        public static Vector3 xz(this Vector3 v)
        {
            return new Vector3(v.x, 0, v.z);
        }

        public static Vector2 xz2(this Vector3 v)
        {
            return new Vector2(v.x, v.z);
        }

        public static Vector4 toVec4(this Vector3 v, float w)
        {
            return new Vector4(v.x, v.y, v.z, w);
        }

        public static UncheckedMesh Combine(this UncheckedMesh m1, UncheckedMesh m2)
        {
            UncheckedMesh mesh = new UncheckedMesh(m1.nVrt + m2.nVrt, m1.nTri + m2.nTri);

            int vertOffset = m1.nVrt;
            int triOffset = m1.nTri * 3;

            m1.verticies.CopyTo(mesh.verticies, 0);
            m1.uv.CopyTo(mesh.uv, 0);
            m1.normals.CopyTo(mesh.normals, 0);
            m1.tangents.CopyTo(mesh.tangents, 0);
            m1.triangles.CopyTo(mesh.triangles, 0);

            m2.verticies.CopyTo(mesh.verticies, vertOffset);
            m2.uv.CopyTo(mesh.uv, vertOffset);
            m2.normals.CopyTo(mesh.normals, vertOffset);
            m2.tangents.CopyTo(mesh.tangents, vertOffset);
            
            for(int i = 0; i < m2.triangles.Length;++i)
                mesh.triangles[triOffset+i] = m2.triangles[i] + vertOffset;

            return mesh;
        }


    }
}
