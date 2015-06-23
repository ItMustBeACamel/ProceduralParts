using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSPAPIExtensions;

namespace ProceduralParts
{
    [Serializable]
    public class EndCapProfileList : IConfigNode
    {
        
        public List<EndCapProfile> EndCapProfiles = new List<EndCapProfile>();

        public float blubb;

        public void Load(ConfigNode node)
        {
            Debug.LogWarning("ProfileList Load");
            ConfigNode[] nodes = node.GetNodes("END_CAP");

            if (!float.TryParse(node.GetValue("blubb"), out blubb))
                Debug.LogError("could not parse blubb");

            foreach(ConfigNode n in nodes)
            {
                //EndCapProfiles.Add(ConfigNode.CreateObjectFromConfig<EndCapProfile>(n));
                string name = typeof(EndCapProfile).AssemblyQualifiedName;
                EndCapProfile newProfile = (EndCapProfile)ConfigNode.CreateObjectFromConfig(name, n);
                newProfile.Load(n);
                EndCapProfiles.Add(newProfile);
            }
   
        }

        public void Save(ConfigNode node)
        {
            Debug.LogWarning("ProfileList Save");
            node.AddValue("blubb", blubb);
            foreach(EndCapProfile p in EndCapProfiles)
            {
                ConfigNode n = ConfigNode.CreateConfigFromObject(p);
                p.Save(n);
                node.AddNode(n);
            }          
        }
    }

    [Serializable]
    public class EndCapProfile : IConfigNode
    {
        [Persistent]
        public string name = "*";

        public enum EdgeMode
        {
            Smooth,
            Sharp,
            SmoothSeam,
            SharpSeam
        }

        public enum YMode
        {
            OffsetFromCenter,
            Offset,
            Relative
        }

        public enum RMode
        {
            RELATIVE_TO_CAP_RADIUS,
            RELATIVE_TO_SHAPE_RADIUS
        }

        [Serializable]
        public struct EndCapProfilePoint
        {
            public float r;
            public float yoffset;
            public float uv1;
            public float uv2;
            public EdgeMode EdgeMode;
            public RMode RadiusMode;

            public EndCapProfilePoint(float r, float y, float uv1,
                EdgeMode edgeMode = EndCapProfile.EdgeMode.Smooth,
                float uv2=0.0f,
                RMode rmode = RMode.RELATIVE_TO_CAP_RADIUS)
            {
                this.r = r;
                this.yoffset = y;
                this.uv1 = uv1;
                this.uv2 = uv2;
                this.EdgeMode = edgeMode;
                this.RadiusMode = rmode;
            }

            public EndCapProfilePoint(string s)
            {
                
                string[] elements = s.Split(null).Where(x => x != string.Empty).ToArray();

                r = float.Parse(elements[0]);
                yoffset = float.Parse(elements[1]);
                uv1 = float.Parse(elements[2]);

                if (elements.Length > 3)
                    EdgeMode = (EdgeMode) Enum.Parse(typeof(EdgeMode), elements[3]);
                else
                    EdgeMode = EndCapProfile.EdgeMode.Smooth;


                if (elements.Length > 4)
                    uv2 = float.Parse(elements[4]);
                else
                    uv2 = uv1;

                if (elements.Length > 5)
                    RadiusMode = (RMode)Enum.Parse(typeof(RMode), elements[5]);
                else
                    RadiusMode = EndCapProfile.RMode.RELATIVE_TO_CAP_RADIUS;

                //EdgeMode = EndCapProfile.EdgeMode.Smooth;
                //RadiusMode = RMode.RELATIVE_TO_CAP_RADIUS;
            }

            public override string ToString()
            {
                string s;
                s = r + " " + yoffset + " " + uv1 + " " + EdgeMode + " " + uv2 + " " + RadiusMode;
                return s;
            }
        }

        //[NonSerialized]
        public List<EndCapProfilePoint> ProfilePoints = new List<EndCapProfilePoint>();

        

        public void Load(ConfigNode node)
        {
            string[] keys = node.GetValues("key");

            foreach(string s in keys)
            {                
                EndCapProfilePoint newPoint = new EndCapProfilePoint(s);
                ProfilePoints.Add(newPoint);
                Debug.LogWarning("Loaded key: " + newPoint);
            }
        }

        public void Save(ConfigNode node)
        {
            foreach (EndCapProfilePoint pp in ProfilePoints)
            {
                node.AddValue("key", pp.ToString());
                Debug.LogWarning("Loaded key: " + pp.ToString());
            }
        }
    }

    
}
