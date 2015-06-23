using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSPAPIExtensions;

namespace ProceduralParts
{
    class RingMeshBuilder
    {
        public enum EdgeMode
        {
            Smooth,
            Sharp,
            SmoothSeam,
            SharpSeam
        }

        private int vertPerRing = 0;
        private bool loops;

        public RingMeshBuilder(bool loops)
        {
            this.loops = loops;
        }

        protected class Ring
        {
            public readonly Vector3[] Positions;
            public readonly Vector2[] UV1;
            public readonly Vector2[] UV2;
            
            public EdgeMode EdgeMode;

            public int VertCount
            {
                get
                { return Positions.Length; }
            }

            public Ring(Vector3[] positions, Vector2[] uv1, Vector2[] uv2, EdgeMode edge = RingMeshBuilder.EdgeMode.Smooth)
            {
                // check arguments
                if (positions == null)
                    throw new ArgumentNullException("verticies", "must not be null");
             

                Positions = new Vector3[positions.Length];
                UV1 = new Vector2[positions.Length];
                
                
                positions.CopyTo(Positions, 0);
                
                if(uv1 != null)
                    Array.Copy(uv1, UV1, Mathf.Min(positions.Length, uv1.Length));

                if (uv2 != null)
                {
                    UV2 = new Vector2[positions.Length];
                    Array.Copy(uv2, UV2, Mathf.Min(positions.Length, uv2.Length));
                }
                else
                    UV2 = UV1;

                EdgeMode = edge;
            }
        }
        private List<Ring> Rings = new List<Ring>();

        protected class InterRing
        {
            //public readonly int VertCount;
            public readonly int VertOffset1;
            public readonly int VertOffset2;
            //public readonly int TriOffset;

            public InterRing(/*int vertCount,*/ int vertOffset1, int vertOffset2)
            {
                //VertCount = vertCount;
                VertOffset1 = vertOffset1;
                VertOffset2 = vertOffset2;
            }
        }
        //protected List<InterRing> interRings = new List<InterRing>();


        public void AddRing(Vector3[] positions, Vector2[] uv1, Vector2[] uv2, EdgeMode edge)
        {
            if (Rings.Count == 0)
                vertPerRing = positions.Length;
            else
                if (positions.Length != vertPerRing)
                    throw new ArgumentException("invalid vertex count");
 
            Rings.Add(new Ring(positions, uv1, uv2, edge));
        }

        public UncheckedMesh BuildMesh(bool invertFaces = false, bool encloseFirstRing = false, bool encloseLastRing = false)
        {

            List<InterRing> interRings = new List<InterRing>();

            // count how many vertices we will need

            int vertCount = 0;

            for(int i = 0; i < Rings.Count; ++i)
            {
                Ring ring = Rings[i];
                if(i == 0 || i == Rings.Count-1)
                    vertCount += ring.VertCount;
                else
                    vertCount += ring.EdgeMode == EdgeMode.Smooth ? ring.VertCount : ring.VertCount * 2;
            }

            List<Vector3> positions = new List<Vector3>(vertCount);
            List<Vector2> uvs = new List<Vector2>(vertCount);
            
            Ring previousRing = null;
            InterRing previousInterRing = null;
            
            for(int i = 0; i < Rings.Count; ++i)
            {
                Ring currentRing = Rings[i];
                if(i != 0)
                {
                    
                    if(previousRing.EdgeMode == EdgeMode.Sharp || previousInterRing == null)
                    {
                        int vertOffset1 = positions.Count;
                        positions.AddRange(previousRing.Positions);
                        uvs.AddRange(previousRing.UV1);
                        int vertOffset2 = positions.Count;
                        positions.AddRange(currentRing.Positions);
                        uvs.AddRange(currentRing.UV1);
                        InterRing newInterRing = new InterRing(vertOffset1, vertOffset2);
                        interRings.Add(newInterRing);
                        previousInterRing = newInterRing;
                    }
                    else
                    {   
                        switch (previousRing.EdgeMode)
                        {
                            case EdgeMode.Smooth:
                                {
                                    int vertOffset1 = previousInterRing.VertOffset2;
                                    int vertOffset2 = positions.Count;
                                    positions.AddRange(currentRing.Positions);
                                    uvs.AddRange(currentRing.UV1);
                                    InterRing newInterRing = new InterRing(vertOffset1, vertOffset2);
                                    interRings.Add(newInterRing);
                                    previousInterRing = newInterRing;
                                    break;
                                }

                            case EdgeMode.SharpSeam:
                            case EdgeMode.SmoothSeam:
                                {
                                    int vertOffset1 = positions.Count;
                                    positions.AddRange(previousRing.Positions);
                                    uvs.AddRange(previousRing.UV2);
                                    int vertOffset2 = positions.Count;
                                    positions.AddRange(currentRing.Positions);
                                    uvs.AddRange(currentRing.UV1);
                                    InterRing newInterRing = new InterRing(vertOffset1, vertOffset2);
                                    interRings.Add(newInterRing);
                                    previousInterRing = newInterRing;


                                    break;
                                }
                            default:
                                Debug.LogWarning("Warning: unsupported edge mode");
                                break;
                        }
                    }
      
                }
                previousRing = currentRing;
            }

            List<int> triangles = new List<int>();

            for (int i = 0; i < interRings.Count; ++i)
            {
                InterRing current = interRings[i];
                ConnectRings(triangles, current.VertOffset1, current.VertOffset2, vertPerRing, loops, invertFaces);
            }


            if (encloseFirstRing)
            {
                int vertOffset=0;

                switch(Rings[0].EdgeMode)
                {
                    case EdgeMode.Smooth:
                        vertOffset = interRings[0].VertOffset1;
                        break;

                    case EdgeMode.Sharp:
                        vertOffset = positions.Count;
                        positions.AddRange(Rings[0].Positions);
                        uvs.AddRange(Rings[0].UV1);
                        break;

                case EdgeMode.SharpSeam:
                case EdgeMode.SmoothSeam:
                        vertOffset = positions.Count;
                        positions.AddRange(Rings[0].Positions);
                        uvs.AddRange(Rings[0].UV2);
                        break;
                default:
                        Debug.LogError("unhandled edge mode");
                        break;
                    
                }
                
                for(int i = 1; i < vertPerRing-1; i++)
                {
                    if (!invertFaces)
                    {
                        triangles.Add(vertOffset);
                        triangles.Add(vertOffset + i + 1);
                        triangles.Add(vertOffset + i);
                    }
                    else
                    {
                        triangles.Add(vertOffset);
                        triangles.Add(vertOffset + i);
                        triangles.Add(vertOffset + i + 1);
                    }
                }


            }

            Vector3[] normals = new Vector3[positions.Count];
            CalculateNormals(positions.ToArray(), triangles.ToArray(), normals);

            for (int i = 0; i < interRings.Count; ++i)
            {
               
                InterRing A = interRings[i];  
                InterRing B = i < interRings.Count-1 ? interRings[i + 1] : null;

                Ring ring = Rings[i + 1];

                if (!loops)
                {
                    normals[A.VertOffset1] = normals[A.VertOffset1 + vertPerRing - 1] = (normals[A.VertOffset1] + normals[A.VertOffset1 + vertPerRing - 1]).normalized;
                    normals[A.VertOffset2] = normals[A.VertOffset2 + vertPerRing - 1] = (normals[A.VertOffset2] + normals[A.VertOffset2 + vertPerRing - 1]).normalized;
                }
                
                if (B != null && ring.EdgeMode == EdgeMode.SmoothSeam)
                {
                    for (int v = 0; v < vertPerRing; ++v)
                    {
                        normals[A.VertOffset2 + v] = normals[B.VertOffset1 + v] = (normals[A.VertOffset2 + v] + normals[B.VertOffset1 + v]).normalized;
                    }
                }
            }

            
            
            UncheckedMesh m = new UncheckedMesh(positions.Count, triangles.Count/3);

            positions.CopyTo(0, m.verticies, 0, Mathf.Min(m.verticies.Length, positions.Count));
            uvs.CopyTo(0, m.uv, 0, Mathf.Min(m.uv.Length, uvs.Count));
            normals.CopyTo(m.normals, 0);

            triangles.CopyTo(0, m.triangles, 0, Mathf.Min(m.triangles.Length, triangles.Count));

            CalculateTangents(m);

            return m;
        }


        void ConnectRings(List<int> triangles, int ring1Offset, int ring2Offset, int vertCount, bool loop, bool inverse = false)
        {
            //int tri = triOffset;
            for (int i = 0; i < vertCount; ++i)
            {
                //Debug.Log(i);
                if (i < vertCount - 1)
                {
                    if (!inverse)
                    {
                        triangles.Add(ring1Offset + i);
                        triangles.Add(ring1Offset + i + 1);
                        triangles.Add(ring2Offset + i + 1);

                        triangles.Add(ring1Offset + i);
                        triangles.Add(ring2Offset + i + 1);
                        triangles.Add(ring2Offset + i);
                    }
                    else
                    {
                        triangles.Add(ring1Offset + i);
                        triangles.Add(ring2Offset + i + 1);
                        triangles.Add(ring1Offset + i + 1);

                        triangles.Add(ring1Offset + i);
                        triangles.Add(ring2Offset + i);
                        triangles.Add(ring2Offset + i + 1);
                    }
                }
                else if(loop)
                {
                    if (!inverse)
                    {
                        triangles.Add(ring1Offset + i);
                        triangles.Add(ring1Offset);
                        triangles.Add(ring2Offset);

                        triangles.Add(ring1Offset + i);
                        triangles.Add(ring2Offset);
                        triangles.Add(ring2Offset + i);
                    }
                    else
                    {
                        triangles.Add(ring1Offset + i);
                        triangles.Add(ring2Offset);
                        triangles.Add(ring1Offset);

                        triangles.Add(ring1Offset + i);
                        triangles.Add(ring2Offset + i);
                        triangles.Add(ring2Offset);
                    }

                }
                
            }
            

        }

        void CalculateNormals(Vector3[] positions, int[] triangles, Vector3[] normals)
        {
            //Vector3[] normals = new Vector3[m.normals.Length];

            //Debug.Log(m.triangles.Length);
            for(int i = 0; i < triangles.Length; i += 3)
            {
                int offA = triangles[i];
                int offB = triangles[i+1];
                int offC = triangles[i+2];

                Vector3 A = positions[offA];
                Vector3 B = positions[offB];
                Vector3 C = positions[offC];

                Vector3 normal = Vector3.Cross(B - A, C - A);

                normals[offA] += normal;                
                normals[offB] += normal;
                normals[offC] += normal;
            }

            for (int i = 0; i < normals.Length; ++i )
                    normals[i] = normals[i].normalized;

            //Debug.LogWarning("NORMALS");
            //for (int i = 0; i < positions.Length; ++i)
            //{
            //    Debug.Log(positions[i] + " - " + normals[i]);
            //}

            //normals.CopyTo(m.normals, 0);
        }

        void CalculateTangents(UncheckedMesh mesh)
        {
            int triangleCount = mesh.triangles.Length;
            int vertexCount = mesh.verticies.Length;

            Vector3[] tan1 = new Vector3[vertexCount];
            Vector3[] tan2 = new Vector3[vertexCount];
     
            Vector4[] tangents = new Vector4[vertexCount];
     
            for(long a = 0; a < triangleCount; a+=3)
            {
                long i1 = mesh.triangles[a+0];
                long i2 = mesh.triangles[a+1];
                long i3 = mesh.triangles[a+2];
     
                Vector3 v1 = mesh.verticies[i1];
                Vector3 v2 = mesh.verticies[i2];
                Vector3 v3 = mesh.verticies[i3];
     
                Vector2 w1 = mesh.uv[i1];
                Vector2 w2 = mesh.uv[i2];
                Vector2 w3 = mesh.uv[i3];
     
                float x1 = v2.x - v1.x;
                float x2 = v3.x - v1.x;
                float y1 = v2.y - v1.y;
                float y2 = v3.y - v1.y;
                float z1 = v2.z - v1.z;
                float z2 = v3.z - v1.z;
     
                float s1 = w2.x - w1.x;
                float s2 = w3.x - w1.x;
                float t1 = w2.y - w1.y;
                float t2 = w3.y - w1.y;
     
                float r = 1.0f / (s1 * t2 - s2 * t1);
     
                Vector3 sdir = new Vector3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
                Vector3 tdir = new Vector3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);
     
                tan1[i1] += sdir;
                tan1[i2] += sdir;
                tan1[i3] += sdir;
     
                tan2[i1] += tdir;
                tan2[i2] += tdir;
                tan2[i3] += tdir;
            }
     
     
            for (long a = 0; a < vertexCount; ++a)
            {
                Vector3 n = mesh.normals[a];
                Vector3 t = tan1[a];
     
                Vector3 tmp = (t - n * Vector3.Dot(n, t)).normalized;
                tangents[a] = new Vector4(tmp.x, tmp.y, tmp.z);
     
                tangents[a].w = (Vector3.Dot(Vector3.Cross(n, t), tan2[a]) < 0.0f) ? -1.0f : 1.0f;
            }
     
            tangents.CopyTo(mesh.tangents, 0);

        }

    }
}
