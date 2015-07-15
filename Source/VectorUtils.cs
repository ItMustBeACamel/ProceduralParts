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

        public static Vector2 xy2(this Vector3 v)
        {
            return new Vector2(v.x, v.y);
        }

        public static Vector4 toVec4(this Vector3 v, float w)
        {
            return new Vector4(v.x, v.y, v.z, w);
        }

        public static Vector3 toVec3(this Vector2 v, float z)
        {
            return new Vector3(v.x, v.y, z);
        }

        public static Vector2 ParseVector2(string s)
        {
            s = s.Trim();
            s = s.Replace("(", "");
            s = s.Replace(")", "");
            string[] elements = s.Split(' ', ',', '\t', ';');
            elements = elements.Where(e => !String.IsNullOrEmpty(e)).ToArray();

            return new Vector2(float.Parse(elements[0]), float.Parse(elements[1]));    
        }

        public static bool TryParse(string s, ref Vector2 result)
        {
            try
            {
                result = ParseVector2(s);
                return true;
            }
            catch(Exception)
            {
                return false;
            }
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


    public static class ColorUtils
    {
        public static bool TryParseColor(string s, ref Color c)
        {
            try
            {
                c = ParseColor(s);
                return true;
            }
            catch(Exception)
            {
                return false;
            }
        }

        public static Color ParseColor(string s)
        {
            s = s.Trim();
            string[] a = s.Split('(');
            
            string format = a[0];
            string args = a[1];
            args = args.Replace(")", "");

            string[] values = args.Split(' ', ',', '\t', ';');
            values = values.Where(e => !String.IsNullOrEmpty(e)).ToArray();

            Color result = new Color();

            for (int i = 0; i < format.Length; i++)
            {
                if (i < values.Length)
                {
                    switch(format[i])
                    {
                        case 'R':
                        case 'r':
                            result.r = float.Parse(values[i]);
                            break;
                        
                        case 'G':
                        case 'g':
                            result.g = float.Parse(values[i]);
                            break;

                        case 'B':
                        case 'b':
                            result.b = float.Parse(values[i]);
                            break;

                        case 'A':
                        case 'a':
                            result.a = float.Parse(values[i]);
                            break;
                        default:
                            Debug.LogWarning("Could not parse format Char: " + format[i]);
                            break;
                    }

                }
                else
                    break;
            }

            return result;

        }
    }
}
