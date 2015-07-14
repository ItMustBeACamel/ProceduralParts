using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSPAPIExtensions;

namespace ProceduralParts
{
    [Serializable]
    public class EndCapList : IConfigNode
    {     
        public List<EndCaps> EndCaps = new List<EndCaps>();

        public void Load(ConfigNode node)
        {
            Debug.LogWarning("EndCapList Load");
            ConfigNode[] nodes = node.GetNodes("CAPS");

            foreach(ConfigNode n in nodes)
            {
                //EndCapProfiles.Add(ConfigNode.CreateObjectFromConfig<EndCapProfile>(n));
                string name = typeof(EndCaps).AssemblyQualifiedName;
                EndCaps newCaps = (EndCaps)ConfigNode.CreateObjectFromConfig(name, n);
                newCaps.Load(n);
                EndCaps.Add(newCaps);
            }
   
        }

        public void Save(ConfigNode node)
        {
            Debug.LogWarning("EndCapList Save");
            
            foreach(EndCaps ec in EndCaps)
            {
                ConfigNode n = ConfigNode.CreateConfigFromObject(ec);
                ec.Save(n);
                node.AddNode("CAPS",n);
            }          
        }
    }

    [Serializable]
    public class EndCapProfile : IConfigNode
    {
        //[Persistent]
        //public string name = "*";

        [Persistent]
        public string texture;

        [Persistent]
        public string bump;

        [NonSerialized]
        public Vector2 textureScale = new Vector2(0.93f, 0.93f);

        [Persistent]
        public float shininess = 0.4f;
        
        [NonSerialized]
        public Color specular = new Color(0.2f, 0.2f, 0.2f);

        [Persistent]
        public bool closeFirstRing = true;

        [Persistent]
        public bool closeLastRing = false;

        [Persistent]
        public bool doNotCreate = false;

        //[Persistent]
        //public bool createBottom = true;

        [Persistent]
        public bool invertFaces = false;

        [Persistent]
        public bool invertFirstClosure = false;

        [Persistent]
        public bool invertLastClosure = false;

        public enum EdgeMode
        {
            SMOOTH,
            SHARP,
            SMOOTH_SEAM,
            SHARP_SEAM
        }

        public enum YMode
        {
            OFFSET_FROM_CENTER,
            OFFSET,
            RELATIVE
        }

        public enum RMode
        {
            RELATIVE_TO_CAP_RADIUS,
            RELATIVE_TO_SHAPE_RADIUS,
            OFFSET_TO_CAP_RADIUS,
            OFFSET_TO_SHAPE_RADIUS
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
            public YMode HeightMode;

            public EndCapProfilePoint(float r, float y, float uv1,
                EdgeMode edgeMode = EndCapProfile.EdgeMode.SMOOTH,
                float uv2=0.0f,
                RMode rmode = RMode.RELATIVE_TO_CAP_RADIUS,
                YMode ymode = YMode.RELATIVE)
            {
                this.r = r;
                this.yoffset = y;
                this.uv1 = uv1;
                this.uv2 = uv2;
                this.EdgeMode = edgeMode;
                this.RadiusMode = rmode;
                this.HeightMode = ymode;
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
                    EdgeMode = EndCapProfile.EdgeMode.SMOOTH;


                if (elements.Length > 4)
                    uv2 = float.Parse(elements[4]);
                else
                    uv2 = uv1;

                if (elements.Length > 5)
                    RadiusMode = (RMode)Enum.Parse(typeof(RMode), elements[5]);
                else
                    RadiusMode = EndCapProfile.RMode.RELATIVE_TO_CAP_RADIUS;

                if (elements.Length > 6)
                    HeightMode = (YMode)Enum.Parse(typeof(YMode), elements[6]);
                else
                    HeightMode = YMode.RELATIVE;

                //EdgeMode = EndCapProfile.EdgeMode.Smooth;
                //RadiusMode = RMode.RELATIVE_TO_CAP_RADIUS;
            }

            public override string ToString()
            {
                string s;
                s = r + " " + yoffset + " " + uv1 + " " + EdgeMode + " " + uv2 + " " + RadiusMode + " " + HeightMode;
                return s;
            }
        }

        
        public List<EndCapProfilePoint> ProfilePoints = new List<EndCapProfilePoint>();

        public void Load(ConfigNode node)
        {
            string[] keys = node.GetValues("key");

            foreach(string s in keys)
            {                
                EndCapProfilePoint newPoint = new EndCapProfilePoint(s);
                ProfilePoints.Add(newPoint);
                //Debug.LogWarning("Loaded key: " + newPoint);
            }

            if(node.HasNode("specular"))
              specular = ConfigNode.ParseColor(node.GetNode("sides").GetValue("specular"));

            if(node.HasValue("textureScale"))
            {
                Vector3 scale;
                if (ParseUtils.TryParseVector3(node.GetValue("textureScale"), out scale))
                    textureScale = scale.xy2();
            }
            //Debug.LogWarning("texture scale: " + textureScale);
        }

        public void Save(ConfigNode node)
        {
            foreach (EndCapProfilePoint pp in ProfilePoints)
            {
                node.AddValue("key", pp.ToString());
                //Debug.LogWarning("Saved key: " + pp.ToString());
                node.AddValue("textureScale", textureScale.toVec3(0.0f));

            }
        }
    }

    [Serializable]
    public class EndCaps : IConfigNode
    {

        [Persistent]
        public string name = "***";

        [Persistent]
        public EndCapProfile topCap;
        
        public EndCapProfile bottomCap;

        public void Load(ConfigNode node)
        {
            ConfigNode bothNode;
            string className = typeof(EndCapProfile).AssemblyQualifiedName;
            if(node.TryGetNode("BOTH", out bothNode))
            {         
                EndCapProfile newProfile = (EndCapProfile)ConfigNode.CreateObjectFromConfig(className, bothNode);
                newProfile.Load(bothNode);
                topCap = newProfile;
                bottomCap = newProfile;
            }
            else
            {
                ConfigNode topNode;
                ConfigNode bottomNode;

                if (node.TryGetNode("TOP", out topNode))
                {
                    EndCapProfile newProfile = (EndCapProfile)ConfigNode.CreateObjectFromConfig(className, topNode);
                    newProfile.Load(topNode);
                    topCap = newProfile;
                }
                else
                    topCap = null;

                if (node.TryGetNode("BOTTOM", out bottomNode))
                {
                    EndCapProfile newProfile = (EndCapProfile)ConfigNode.CreateObjectFromConfig(className, bottomNode);
                    newProfile.Load(bottomNode);
                    bottomCap = newProfile;
                }
                else
                    bottomCap = null;
            }     
        }

        public void Save(ConfigNode node)
        {
            if(topCap == bottomCap && topCap != null)
            {
                ConfigNode bothNode = ConfigNode.CreateConfigFromObject(topCap ?? bottomCap);
                topCap.Save(bothNode);

                node.AddNode("BOTH", bothNode);
            }
            else
            {
                if(topCap != null)
                {
                    ConfigNode topNode = ConfigNode.CreateConfigFromObject(topCap);
                    topCap.Save(topNode);

                    node.AddNode("TOP", topNode);
                }

                if (bottomCap != null)
                {
                    ConfigNode bottomNode = ConfigNode.CreateConfigFromObject(bottomCap);
                    bottomCap.Save(bottomNode);

                    node.AddNode("BOTTOM", bottomNode);
                }

            }
        }
    }

    
}
