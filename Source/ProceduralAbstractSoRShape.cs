using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSPAPIExtensions;
using KSPAPIExtensions.PartMessage;

namespace ProceduralParts
{
    public abstract class ProceduralAbstractSoRShape : ProceduralAbstractShape
    {

        #region Callbacks

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            Debug.LogWarning("OnLoad Shape");
            endCaps = new EndCapList();

            ConfigNode endCapsNode = node.GetNode("END_CAPS");

            if(endCapsNode != null)
            {
                endCaps.Load(endCapsNode);
            }
            else
            {
                Debug.LogWarning("No End caps found");
            }

            // Serialize the end caps
            endCapsSerialized = ObjectSerializer.Serialize(endCaps);
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            if (endCaps != null)
            {
                ConfigNode capsNode = ConfigNode.CreateConfigFromObject(endCaps);
                endCaps.Save(capsNode);

                node.AddNode("END_CAPS", capsNode);
            }

        }

        public override void OnStart(StartState state)
        {
            Debug.LogWarning("OnStart Shape " + displayName);

            base.OnStart(state);

            foreach(AttachNode attachNode in part.attachNodes)
            {
                AttachmentNode newNode;

                newNode.Node = attachNode;
                newNode.Position = new ShapeCoordinates();

                if (attachNode.id == topNodeName)
                {
                    // standard top node
                    
                    newNode.Position.r = 0;
                    newNode.Position.u = 0;
                    newNode.Position.y = 1;
                    newNode.Position.HeightMode = ShapeCoordinates.YMode.RELATIVE_TO_SHAPE;
                    newNode.Position.RadiusMode = ShapeCoordinates.RMode.RELATIVE_TO_TOP_RADIUS;                  
                }
                else if (attachNode.id == bottomNodeName)
                {
                    // standard bottom node

                    newNode.Position.r = 0;
                    newNode.Position.u = 0;
                    newNode.Position.y = -1;
                    newNode.Position.HeightMode = ShapeCoordinates.YMode.RELATIVE_TO_SHAPE;
                    newNode.Position.RadiusMode = ShapeCoordinates.RMode.RELATIVE_TO_TOP_RADIUS;
                }
                else
                {
                    newNode.Position.r = attachNode.position.x;
                    newNode.Position.u = attachNode.position.z;
                    newNode.Position.y = attachNode.position.y;
                    newNode.Position.HeightMode = ShapeCoordinates.YMode.RELATIVE_TO_SHAPE;
                    newNode.Position.RadiusMode = ShapeCoordinates.RMode.RELATIVE_TO_TOP_RADIUS;
                }
                attachmentNodes.Add(newNode);
            }
            
            
            
            if (endCapsSerialized != null)
            {
                ObjectSerializer.Deserialize(endCapsSerialized, out endCaps);

                BaseField field = Fields["endCap"];
                UI_ChooseOption range = (UI_ChooseOption)field.uiControlEditor;

                range.options = endCaps.EndCaps.Select(x => x.name).ToArray();
            }
            else
                Debug.LogWarning("endCapSerialized == null");

            UpdateEndCaps(true);

        }

        public override void OnUpdateEditor()
        {
            UpdateEndCaps();
            base.OnUpdateEditor();

            
        }

        #endregion

        #region Config fields

        internal const int MinCircleVertexes = 12;
        internal const float MaxCircleError = 0.01f;
        internal const float MaxDiameterChange = 5.0f;

        [KSPField]
        public string topNodeName = "top";

        [KSPField]
        public string bottomNodeName = "bottom";

        #endregion


        #region attachments

        protected class SoRShapeAttachment : ShapeAttachment
        {
            public override ShapeCoordinates updateCoordinates()
            {
                throw new NotImplementedException();
            }
            
            
        }

        public override Vector3 FromCylindricCoordinates(ShapeCoordinates coords)
        {
            Vector3 position = new Vector3();

            AttachNode node = null;
            switch (coords.HeightMode)
            {
                case ShapeCoordinates.YMode.RELATIVE_TO_SHAPE:
                    float halfLength = (lastProfile.Last.Value.y - lastProfile.First.Value.y) / 2.0f;
                    position.y = halfLength * coords.y;
                    break;

                case ShapeCoordinates.YMode.OFFSET_FROM_SHAPE_CENTER:
                    position.y = coords.y;
                    break;

                case ShapeCoordinates.YMode.OFFSET_FROM_SHAPE_BOTTOM:
                    position.y = coords.y + lastProfile.First.Value.y;
                    break;

                case ShapeCoordinates.YMode.OFFSET_FROM_SHAPE_TOP:
                    position.y = coords.y + lastProfile.Last.Value.y;
                    break;

                case ShapeCoordinates.YMode.RELATIVE_TO_TOP_NODE:
                    node = part.findAttachNode(topNodeName);
                    position.y = node.position.y * coords.y;
                    break;
                case ShapeCoordinates.YMode.RELATIVE_TO_BOTTOM_NODE:
                    node = part.findAttachNode(bottomNodeName);
                    position.y = node.position.y * coords.y;
                    break;
                case ShapeCoordinates.YMode.OFFSET_FROM_TOP_NODE:
                    node = part.findAttachNode(topNodeName);
                    position.y = node.position.y + coords.y;
                    break;
                case ShapeCoordinates.YMode.OFFSET_FROM_BOTTOM_NODE:
                    node = part.findAttachNode(bottomNodeName);
                    position.y = node.position.y + coords.y;
                    break;
                default:
                    Debug.LogError("Can not handle PartCoordinate attribute: " + coords.HeightMode);
                    position.y = 0.0f;
                    break;
            }

            float radius = 0;

            switch(coords.RadiusMode)
            {
                case ShapeCoordinates.RMode.OFFSET_FROM_SHAPE_CENTER:
                    radius = coords.r;
                    break;

                case ShapeCoordinates.RMode.RELATIVE_TO_TOP_RADIUS:
                    radius = lastProfile.Last.Value.dia / 2.0f * coords.r;
                    break;

                case ShapeCoordinates.RMode.RELATIVE_TO_BOTTOM_RADIUS:
                    radius = lastProfile.First.Value.dia / 2.0f * coords.r;
                    break;

                case ShapeCoordinates.RMode.OFFSET_FROM_TOP_RADIUS:
                    radius = lastProfile.Last.Value.dia / 2.0f + coords.r;
                    break;

                case ShapeCoordinates.RMode.OFFSET_FROM_BOTTOM_RADIUS:
                    radius = lastProfile.First.Value.dia / 2.0f + coords.r;
                    break;

                case ShapeCoordinates.RMode.OFFSET_FROM_SHAPE_RADIUS:
                case ShapeCoordinates.RMode.RELATIVE_TO_SHAPE_RADIUS:

                    if (position.y <= lastProfile.First.Value.y)
                        if (coords.RadiusMode == ShapeCoordinates.RMode.OFFSET_FROM_SHAPE_RADIUS)
                            goto case ShapeCoordinates.RMode.OFFSET_FROM_BOTTOM_RADIUS;
                        else
                            goto case ShapeCoordinates.RMode.RELATIVE_TO_BOTTOM_RADIUS;

                    if (position.y >= lastProfile.Last.Value.y)
                        if (coords.RadiusMode == ShapeCoordinates.RMode.OFFSET_FROM_SHAPE_RADIUS)
                            goto case ShapeCoordinates.RMode.OFFSET_FROM_TOP_RADIUS;
                        else
                            goto case ShapeCoordinates.RMode.RELATIVE_TO_TOP_RADIUS;

                    ProfilePoint pt = lastProfile.First.Value;
                    for (LinkedListNode<ProfilePoint> ptNode = lastProfile.First.Next; ptNode != null; ptNode = ptNode.Next)
                    {
                        if (!ptNode.Value.inCollider)
                            continue;
                        ProfilePoint pv = pt;
                        pt = ptNode.Value;

                        if (position.y >= Mathf.Min(pv.y, pt.y) && position.y < Mathf.Max(pv.y, pt.y))
                        {
                            float t = Mathf.InverseLerp(Mathf.Min(pv.y, pt.y), Mathf.Max(pv.y, pt.y), position.y);
                            float profileRadius = Mathf.Lerp(pv.dia, pt.dia, t) / 2.0f;

                            
                            radius = coords.RadiusMode == ShapeCoordinates.RMode.OFFSET_FROM_SHAPE_RADIUS ? 
                                profileRadius + coords.r :
                                radius = profileRadius * coords.r;
                        }
                    }

                    break;
            }

            float theta = Mathf.Lerp(0, Mathf.PI * 2f, coords.u);

            position.x = Mathf.Cos(theta) * radius;
            position.z = -Mathf.Sin(theta) * radius;

            return position;
            
        }

        public void GetCylindricCoordinates(Transform transform, ShapeCoordinates result)
        {
            //Vector3 position = this.transform.TransformPoint(transform.position);

            GetCylindricCoordinates(gameObject.transform.InverseTransformPoint(transform.position), result);
        }

        public override void GetCylindricCoordinates(Vector3 position, ShapeCoordinates result)
        {

            Vector2 direction = new Vector2(position.x, position.z);
            AttachNode node = null;

            switch(result.HeightMode)
            {
                case ShapeCoordinates.YMode.RELATIVE_TO_SHAPE:
                    float halfLength = (lastProfile.Last.Value.y - lastProfile.First.Value.y) / 2.0f;
                    result.y = position.y / halfLength;
                    break;
                    
                case ShapeCoordinates.YMode.OFFSET_FROM_SHAPE_CENTER:
                    result.y = position.y;
                    break;

                case ShapeCoordinates.YMode.OFFSET_FROM_SHAPE_BOTTOM:
                    result.y = position.y - lastProfile.First.Value.y;
                    break;

                case ShapeCoordinates.YMode.OFFSET_FROM_SHAPE_TOP:
                    result.y = position.y - lastProfile.Last.Value.y;
                    break;

                case ShapeCoordinates.YMode.RELATIVE_TO_TOP_NODE:
                    node = part.findAttachNode(topNodeName);
                    result.y = position.y / node.position.y;
                    break;

                case ShapeCoordinates.YMode.RELATIVE_TO_BOTTOM_NODE:
                    node = part.findAttachNode(bottomNodeName);
                    result.y = position.y / node.position.y;
                    break;
                case ShapeCoordinates.YMode.OFFSET_FROM_TOP_NODE:
                    node = part.findAttachNode(topNodeName);
                    result.y = position.y - node.position.y;
                    break;
                case ShapeCoordinates.YMode.OFFSET_FROM_BOTTOM_NODE:
                    node = part.findAttachNode(bottomNodeName);
                    result.y = position.y - node.position.y;
                    break;
                default:
                    Debug.LogError("Can not handle PartCoordinate attribute: " + result.HeightMode);
                    result.y = 0.0f;
                    break;
            }

            
            result.r = 0;
            
            float theta = Mathf.Atan2(-direction.y, direction.x);
           
            result.u = (Mathf.InverseLerp(-Mathf.PI, Mathf.PI, theta) + 0.5f) % 1.0f;

            if(result.RadiusMode == ShapeCoordinates.RMode.OFFSET_FROM_SHAPE_CENTER)
            {
                result.r = direction.magnitude;
                return;
            }

            if (position.y <= lastProfile.First.Value.y)
                result.r = result.RadiusMode == ShapeCoordinates.RMode.OFFSET_FROM_SHAPE_RADIUS ? 
                    direction.magnitude - lastProfile.First.Value.dia / 2.0f :
                    direction.magnitude / (lastProfile.First.Value.dia / 2.0f); // RELATIVE_TO_SHAPE_RADIUS

            else if (position.y >= lastProfile.Last.Value.y)
                result.r = result.RadiusMode == ShapeCoordinates.RMode.OFFSET_FROM_SHAPE_RADIUS ?
                    direction.magnitude - lastProfile.Last.Value.dia / 2.0f :
                    direction.magnitude / (lastProfile.Last.Value.dia / 2.0f); // RELATIVE_TO_SHAPE_RADIUS
            else
            {
                ProfilePoint pt = lastProfile.First.Value;
                for (LinkedListNode<ProfilePoint> ptNode = lastProfile.First.Next; ptNode != null; ptNode = ptNode.Next)
                {
                    if (!ptNode.Value.inCollider)
                        continue;
                    ProfilePoint pv = pt;
                    pt = ptNode.Value;

                    if(position.y >= Mathf.Min(pv.y, pt.y) && position.y < Mathf.Max(pv.y, pt.y))
                    {
                        float t = Mathf.InverseLerp(Mathf.Min(pv.y, pt.y), Mathf.Max(pv.y, pt.y), position.y);
                        float r = Mathf.Lerp(pv.dia, pt.dia, t) / 2.0f;

                        result.r = result.RadiusMode == ShapeCoordinates.RMode.OFFSET_FROM_SHAPE_RADIUS ?
                            direction.magnitude - r : direction.magnitude / r;
                    }

                }

            }

            // sometimes, if the shapes radius is 0, r rersults in NaN
            if (float.IsNaN(result.r))
            {
                result.r = 0;
            }
        }


        #region shape attachments

        //protected class ShapeAttachment
        //{
        //    public TransformFollower follower;
        //    public ShapeCoordinates coordinates;
        //}
        protected LinkedList<SoRShapeAttachment> shapeAttachments = new LinkedList<SoRShapeAttachment>();

        public override ShapeAttachment AddAttachment(TransformFollower follower, ShapeCoordinates coordinates, bool updateCoordinates = false)
        {
            if (follower == null)
                throw new ArgumentNullException("follower");
            if (coordinates == null)
                throw new ArgumentNullException("coordinates");

            if(updateCoordinates)
            {
                GetCylindricCoordinates(follower.transform, coordinates);
            }

            SoRShapeAttachment newAttachment = new SoRShapeAttachment();

            newAttachment.coordinates = coordinates;
            newAttachment.follower = follower;

            shapeAttachments.AddLast(newAttachment);

            return newAttachment;
        }

        public override void RemoveAttachment(ShapeAttachment attachment, bool updateCoordinates = false)
        {
            SoRShapeAttachment sorAttachment = attachment as SoRShapeAttachment;

            if (sorAttachment != null)
            {
                shapeAttachments.Remove(sorAttachment);

                if(updateCoordinates)
                {
                    GetCylindricCoordinates(sorAttachment.follower.transform, sorAttachment.coordinates);
                }

            }
        }

        #endregion

        private enum Location
        {
            Top, Bottom, Side
        }

        private class Attachment
        {
            public TransformFollower follower;
            public Location location;
            public Vector2 uv;

            public LinkedListNode<Attachment> node;

            public override string ToString()
            {
                return "Attachment(location:" + location + ", uv=" + uv.ToString("F4") + ")";
            }
        }

        private readonly LinkedList<Attachment> topAttachments = new LinkedList<Attachment>();
        private readonly LinkedList<Attachment> bottomAttachments = new LinkedList<Attachment>();
        private readonly LinkedList<Attachment> sideAttachments = new LinkedList<Attachment>();

        public override object AddAttachment(TransformFollower attach, bool normalized)
        {
            return normalized ? AddAttachmentNormalized(attach) : AddAttachmentNotNormalized(attach);
        }

        private object AddAttachmentNotNormalized(TransformFollower attach)
        {
            Attachment ret = new Attachment
            {
                follower = attach
            };

            if (lastProfile == null)
                throw new InvalidOperationException("Can't child non-normalized attachments prior to the first update");

            // All the code from here down assumes the part is a convex shape, which is fair as it needs to be convex for 
            // partCollider purposes anyhow. If we allow concave shapes it will need some refinement.
            Vector3 position = attach.transform.localPosition;

            // Convert the offset into spherical coords
            float r = position.magnitude;
            float theta, phi;
            if (r > 0f)
            {
                theta = Mathf.Atan2(-position.z, position.x);
                phi = Mathf.Asin(position.y / r);
            }
            else
            {
                // move the origin to the top to avoid divide by zeros.
                theta = 0;
                phi = Mathf.PI / 2f;
            }


            // top or bottom?
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (phi != 0)
            {
                ProfilePoint topBot = (phi < 0) ? lastProfile.First.Value : lastProfile.Last.Value;

                float tbR = Mathf.Sqrt(topBot.y * topBot.y + topBot.dia * topBot.dia * 0.25f);
                float tbPhi = Mathf.Asin(topBot.y / tbR);

                if (Mathf.Abs(phi) >= Mathf.Abs(tbPhi))
                {
                    ret.uv = topBot.dia < 0.001f ? 
                        new Vector2(0.5f, 0.5f) : 
                        new Vector2(position.x / topBot.dia * 2f + 0.5f, position.z / topBot.dia * 2f + 0.5f);

                    if (phi > 0)
                    {
                        ret.location = Location.Top;
                        ret.node = topAttachments.AddLast(ret);
                        ret.follower.SetLocalRotationReference(Quaternion.LookRotation(Vector3.up, Vector3.right));
                    }
                    else
                    {
                        ret.location = Location.Bottom;
                        ret.node = bottomAttachments.AddLast(ret);
                        ret.follower.SetLocalRotationReference(Quaternion.LookRotation(Vector3.down, Vector3.left));
                    }
                    //Debug.LogWarning("Adding non-normalized attachment to position=" + position + " location=" + ret.location + " uv=" + ret.uv + " attach=" + attach.name);
                    return ret;
                }
            }

            // THis is the slope of a line projecting out towards our attachment
            float s = position.y / Mathf.Sqrt(position.x * position.x + position.z * position.z);

            ret.location = Location.Side;
            ret.uv[0] = (Mathf.InverseLerp(-Mathf.PI, Mathf.PI, theta) + 0.5f) % 1.0f;

            ProfilePoint pt = lastProfile.First.Value;
            for (LinkedListNode<ProfilePoint> ptNode = lastProfile.First.Next; ptNode != null; ptNode = ptNode.Next)
            {
                if (!ptNode.Value.inCollider)
                    continue;
                ProfilePoint pv = pt;
                pt = ptNode.Value;

                float ptR = Mathf.Sqrt(pt.y * pt.y + pt.dia * pt.dia * 0.25f);
                float ptPhi = Mathf.Asin(pt.y / ptR);

                //Debug.LogWarning("ptPhi=" + ptPhi + " phi=" + phi);

                if (phi > ptPhi)
                    continue;

                // so we know the attachment is somewhere between the previous and this circle
                // Geometry: draw a line between the point (dia/2, y) in the prev circle and  (dia/2, y) in the current circle (parametric in t)
                // find the point on the line where y = s * dia / 2  and solve for t

                // r(t) = r0 + (r1-r0)t
                // y(t) = y0 + (y1-y0)t
                // y(t) = s * r(t)
                //
                // y0 + (y1-y0)t = s r0 + s (r1-r0) t
                // ((y1-y0)- s(r1-r0))t = s r0 - y0
                // t = (s r0 - y0) / ((y1-y0) - s(r1-r0))

                float r0 = pv.dia * 0.5f;
                float r1 = pt.dia * 0.5f;

                float t = (s * r0 - pv.y) / ((pt.y - pv.y) - s * (r1 - r0));

                //Debug.LogWarning(string.Format("New Attachment: pv=({0:F2}, {1:F2}) pt=({2:F2}, {3:F2}) s={4:F2} t={5:F2}", r0, pv.y, r1, pt.y, s, t));

                ret.uv[1] = Mathf.Lerp(pv.v, pt.v, t);
                if (ret.uv[1] > 1.0f)
                    Debug.LogError("result off end of segment v=" + ret.uv[1] + " pv.v=" + pv.v + " pt.v=" + pt.v + " t=" + t);

                // 
                Vector3 normal;
                Quaternion rot = SideAttachOrientation(pv, pt, theta, out normal);
                ret.follower.SetLocalRotationReference(rot);

                AddSideAttachment(ret);
                //Debug.LogWarning("Adding non-normalized attachment to position=" + position + " location=" + ret.location + " uv=" + ret.uv + " attach=" + attach.name);
                return ret;
            }

            // This should be impossible to reach
            throw new InvalidProgramException("Unreachable code reached");
        }

        private object AddAttachmentNormalized(TransformFollower attach)
        {
            Attachment ret = new Attachment
            {
                follower = attach
            };

            Vector3 position = attach.transform.localPosition;

            // This is easy, just get the UV and location correctly and force an update.
            // as the position might be after some rotation and translation, it might not be exactly +/- 0.5
            if (Mathf.Abs(Mathf.Abs(position.y) - 0.5f) < 1e-5f)
            {
                if (position.y > 0)
                {
                    ret.location = Location.Top;
                    ret.uv = new Vector2(position.x + 0.5f, position.z + 0.5f);
                    ret.node = topAttachments.AddLast(ret);
                    ret.follower.SetLocalRotationReference(Quaternion.LookRotation(Vector3.up, Vector3.right));
                }
                else if (position.y < 0)
                {
                    ret.location = Location.Bottom;
                    ret.uv = new Vector2(position.x + 0.5f, position.z + 0.5f);
                    ret.node = bottomAttachments.AddLast(ret);
                    ret.follower.SetLocalRotationReference(Quaternion.LookRotation(Vector3.down, Vector3.left));
                }
            }
            else
            {
                ret.location = Location.Side;
                float theta = Mathf.Atan2(-position.z, position.x);
                ret.uv[0] = (Mathf.InverseLerp(-Mathf.PI, Mathf.PI, theta) + 0.5f) % 1.0f;
                ret.uv[1] = 0.5f - position.y;

                Vector3 normal = new Vector3(position.x * 2f, 0, position.z * 2f);
                ret.follower.SetLocalRotationReference(Quaternion.FromToRotation(Vector3.up, normal));

                // side attachments are kept sorted
                AddSideAttachment(ret);
            }
            ForceNextUpdate();

            //Debug.LogWarning("Adding normalized attachment to position=" + position + " location=" + ret.location + " uv=" + ret.uv + " attach=" + attach.name);
            return ret;
        }

        private void MoveAttachments(LinkedList<ProfilePoint> pts)
        {
            //lastProfile = pts; // moved to WriteMeshes()

            // top points
            ProfilePoint top = pts.Last.Value;
            foreach (Attachment a in topAttachments)
            {
                Vector3 pos = new Vector3(
                    (a.uv[0] - 0.5f) * top.dia * 0.5f,
                    top.y,
                    (a.uv[1] - 0.5f) * top.dia * 0.5f);
                //Debug.LogWarning("Moving attachment:" + a + " to:" + pos.ToString("F7") + " uv: " + a.uv.ToString("F5"));
                a.follower.transform.localPosition = pos;
                a.follower.ForceUpdate();
            }

            // bottom points
            ProfilePoint bot = pts.First.Value;
            foreach (Attachment a in bottomAttachments)
            {
                Vector3 pos = new Vector3(
                    (a.uv[0] - 0.5f) * bot.dia * 0.5f,
                    bot.y,
                    (a.uv[1] - 0.5f) * bot.dia * 0.5f);
                //Debug.LogWarning("Moving attachment:" + a + " to:" + pos.ToString("F7") + " uv: " + a.uv.ToString("F5"));
                a.follower.transform.localPosition = pos;
                a.follower.ForceUpdate();
            }

            // sides
            ProfilePoint pv = null;
            ProfilePoint pt = pts.First.Value;
            LinkedListNode<ProfilePoint> ptNode = pts.First;
            foreach (Attachment a in sideAttachments)
            {
                while (pt.v < a.uv[1])
                {
                    ptNode = ptNode.Next;
                    if (ptNode == null)
                    {
                        ptNode = pts.Last;
                        Debug.LogError("Child v greater than last point. Child v=" + a.uv[1] + " last point v=" + ptNode.Value.v);
                        break;
                    }
                    if (!ptNode.Value.inCollider)
                        continue;
                    pv = pt;
                    pt = ptNode.Value;
                }
                if (pv == null)
                {
                    Debug.LogError("Child v smaller than first point. Child v=" + a.uv[1] + " first point v=" + ptNode.Value.v);
                    continue;                    
                }

                float t = Mathf.InverseLerp(pv.v, pt.v, a.uv[1]);
                //Debug.LogWarning("pv.v=" + pv.v + " pt.v=" + pt.v + " att.v=" + a.uv[1] + " t=" + t);

                // using cylindrical coords
                float r = Mathf.Lerp(pv.dia * 0.5f, pt.dia * 0.5f, t);
                float y = Mathf.Lerp(pv.y, pt.y, t);

                float theta = Mathf.Lerp(0, Mathf.PI * 2f, a.uv[0]);

                float x = Mathf.Cos(theta) * r;
                float z = -Mathf.Sin(theta) * r;

                Vector3 pos = new Vector3(x, y, z);
                //print("Moving attachment:" + a + " to:" + pos.ToString("F3"));
                a.follower.transform.localPosition = pos;

                Vector3 normal;
                Quaternion rot = SideAttachOrientation(pv, pt, theta, out normal);

                //Debug.LogWarning("Moving to orientation: normal: " + normal.ToString("F3") + " theta:" + (theta * 180f / Mathf.PI) + rot.ToStringAngleAxis());

                a.follower.transform.localRotation = rot;
                a.follower.ForceUpdate();
            }
        }

        private static Quaternion SideAttachOrientation(ProfilePoint pv, ProfilePoint pt, float theta, out Vector3 normal)
        {
            normal = Quaternion.AngleAxis(theta * 180 / Mathf.PI, Vector3.up) * new Vector2(pt.y - pv.y, -(pt.dia - pv.dia) / 2f);
            return Quaternion.FromToRotation(Vector3.up, normal);
        }

        private void AddSideAttachment(Attachment ret)
        {
            for (LinkedListNode<Attachment> node = sideAttachments.First; node != null; node = node.Next)
                if (node.Value.uv[1] > ret.uv[1])
                {
                    ret.node = sideAttachments.AddBefore(node, ret);
                    return;
                }
            ret.node = sideAttachments.AddLast(ret);
        }

        public override TransformFollower RemoveAttachment(object data, bool normalize)
        {
            Attachment attach = (Attachment)data;
            switch (attach.location)
            {
                case Location.Top:
                    topAttachments.Remove(attach.node);
                    if (normalize)
                        attach.follower.transform.localPosition = new Vector3(attach.uv[0] - 0.5f, 0.5f, attach.uv[1] - 0.5f);
                    break;
                case Location.Bottom:
                    bottomAttachments.Remove(attach.node);
                    if (normalize)
                        attach.follower.transform.localPosition = new Vector3(attach.uv[0] - 0.5f, -0.5f, attach.uv[1] - 0.5f);
                    break;
                case Location.Side:
                    sideAttachments.Remove(attach.node);

                    if (normalize)
                    {
                        float theta = Mathf.Lerp(0, Mathf.PI * 2f, attach.uv[0]);
                        float x = Mathf.Cos(theta);
                        float z = -Mathf.Sin(theta);

                        Vector3 normal = new Vector3(x, 0, z);
                        attach.follower.transform.localPosition = new Vector3(normal.x * 0.5f, 0.5f - attach.uv[1], normal.z * 0.5f);
                        attach.follower.transform.localRotation = Quaternion.FromToRotation(Vector3.up, normal);
                    }
                    break;
            }

            if (normalize)
                attach.follower.ForceUpdate();
            return attach.follower;
        }

        #endregion

        #region Mesh Writing

        protected class ProfilePoint
        {
            public readonly float dia;
            public readonly float y;
            public float v;

            public readonly bool inRender;
            public readonly bool inCollider;

            // the normal as a 2 component unit vector (dia, y)
            // eg: for cylinders this would be (1,0), for endcaps (0,+/-1)
            public readonly Vector2 norm;

            public readonly CirclePoints circ;
            public readonly CirclePoints colliderCirc;

            public ProfilePoint(float dia, float y, float v, Vector2 norm, bool inRender = true, bool inCollider = true, CirclePoints circ = null, CirclePoints colliderCirc = null)
            {
                this.dia = dia;
                this.y = y;
                this.v = v;
                this.norm = norm;
                this.inRender = inRender;
                this.inCollider = inCollider;
                this.circ = inRender ? (circ ?? CirclePoints.ForDiameter(dia, MaxCircleError, MinCircleVertexes)) : null;
                this.colliderCirc = inCollider ? (colliderCirc ?? this.circ ?? CirclePoints.ForDiameter(dia, MaxCircleError, MinCircleVertexes)) : null;
            }

            public bool CustomCollider
            {
                get
                {
                    return circ != colliderCirc;
                }
            }
        }

        private LinkedList<ProfilePoint> lastProfile;


        public Vector3[] GetEndcapVerticies(bool top)
        {
            if (lastProfile == null)
                return new Vector3[0];

            ProfilePoint profilePoint = top ? lastProfile.Last.Value : lastProfile.First.Value;
            

            Vector3[] verticies = new Vector3[profilePoint.circ.totVertexes];

            bool odd = false;

            
            odd = lastProfile.Count % 2 == 0;

            profilePoint.circ.WriteEndcapVerticies(profilePoint.dia, profilePoint.y, 0, verticies, odd);

            return verticies;
        }
        


        protected void WriteMeshes(params ProfilePoint[] pts)
        {
            WriteMeshes(new LinkedList<ProfilePoint>(pts));
        }

        /// <summary>
        /// Generate the compShape from profile points from pt to bottom.
        /// Note that this list will have extra interpolated points added if the change in radius is high to avoid
        /// texture stretching.
        /// </summary>
        /// <param name="pts"></param>
        protected void WriteMeshes(LinkedList<ProfilePoint> pts)
        {
            if (pts == null || pts.Count < 2)
                return;

            // update nodes
            UpdateNodeSize(pts.First(), bottomNodeName);
            UpdateNodeSize(pts.Last(), topNodeName);

            lastProfile = pts;

            MoveAttachNodes(selectedEndCaps);

            // Move attachments first, before subdividing
            MoveAttachments(pts);

            

            // Horizontal profile point subdivision
            SubdivHorizontal(pts);

            // Tank stats
            float tankVLength = 0;

            int nVrt = 0;
            int nTri = 0;
            int nColVrt = 0;
            int nColTri = 0;
            bool customCollider = false;

            ProfilePoint first = pts.First.Value;
            ProfilePoint last = pts.Last.Value;

            if (!first.inCollider || !last.inCollider)
                throw new InvalidOperationException("First and last profile points must be used in the collider");

            foreach (ProfilePoint pt in pts)
            {
                customCollider = customCollider || pt.CustomCollider;

                if (pt.inRender)
                {
                    nVrt += pt.circ.totVertexes + 1;
                    // one for above, one for below
                    nTri += 2 * pt.circ.totVertexes;
                }

                if (pt.inCollider)
                {
                    nColVrt += pt.colliderCirc.totVertexes + 1;
                    nColTri += 2 * pt.colliderCirc.totVertexes;
                }
            }
            // Have double counted for the first and last circles.
            nTri -= first.circ.totVertexes + last.circ.totVertexes;
            nColTri -= first.colliderCirc.totVertexes + last.colliderCirc.totVertexes;

            UncheckedMesh m = new UncheckedMesh(nVrt, nTri);

            float sumDiameters = 0;
            //Debug.LogWarning("Display mesh vert=" + nVrt + " tris=" + nTri);

            bool odd = false;
            {
                ProfilePoint prev = null;
                int off = 0, prevOff = 0;
                int tOff = 0;
                foreach (ProfilePoint pt in pts)
                {
                    if (!pt.inRender)
                        continue;

                    pt.circ.WriteVertexes(diameter: pt.dia, y: pt.y, v: pt.v, norm: pt.norm, off: off, m: m, odd: odd);
                    if (prev != null)
                    {
                        CirclePoints.WriteTriangles(prev.circ, prevOff, pt.circ, off, m.triangles, tOff * 3, !odd);
                        tOff += prev.circ.totVertexes + pt.circ.totVertexes;

                        // Deprecated: Volume has been moved up to callers. This way we can use the idealized rather than aproximate volume
                        // Work out the area of the truncated cone

                        // integral_y1^y2 pi R(y)^2 dy   where R(y) = ((r2-r1)(y-y1))/(r2-r1) + r1   Integrate circles along a line
                        // integral_y1^y2 pi ( ((r2-r1)(y-y1))/(r2-r1) + r1) ^2 dy                Substituted in formula.
                        // == -1/3 pi (y1-y2) (r1^2+r1*r2+r2^2)                                   Do the calculus
                        // == -1/3 pi (y1-y2) (d1^2/4+d1*d2/4+d2^2/4)                             r = d/2
                        // == -1/12 pi (y1-y2) (d1^2+d1*d2+d2^2)                                  Take out the factor
                        //volume += (Mathf.PI * (pt.y - prev.y) * (prev.dia * prev.dia + prev.dia * pt.dia + pt.dia * pt.dia)) / 12f;

                        float dy = (pt.y - prev.y);
                        float dr = (prev.dia - pt.dia) * 0.5f;

                        //print("dy=" + dy + " dr=" + dr + " len=" + Mathf.Sqrt(dy * dy + dr * dr).ToString("F3"));
                        tankVLength += Mathf.Sqrt(dy * dy + dr * dr);

                        // average diameter weighted by dy
                        sumDiameters += (pt.dia + prev.dia) * dy;
                    }

                    prev = pt;
                    prevOff = off;
                    off += pt.circ.totVertexes + 1;
                    odd = !odd;
                }
            }

            // Use the weighted average diameter across segments to set the ULength
            float tankULength = Mathf.PI * sumDiameters / (last.y - first.y);

            //print("ULength=" + tankULength + " VLength=" + tankVLength);

            // set the texture scale.
            RaiseChangeTextureScale("sides", PPart.SidesMaterial, new Vector2(tankULength, tankVLength));

            

            if(HighLogic.LoadedScene == GameScenes.LOADING)
                m.WriteTo(PPart.SidesIconMesh);
            else
                m.WriteTo(SidesMesh);


            // The endcaps.
            /*
            nVrt = first.circ.totVertexes + last.circ.totVertexes;
            nTri = first.circ.totVertexes - 2 + last.circ.totVertexes - 2;
            m = new UncheckedMesh(nVrt, nTri);

            first.circ.WriteEndcap(first.dia, first.y, false, 0, 0, m, false);
            last.circ.WriteEndcap(last.dia, last.y, true, first.circ.totVertexes, (first.circ.totVertexes - 2) * 3, m, !odd);
            */

            //EndCapProfile testProfile = new EndCapProfile();

            //testProfile.ProfilePoints.Add(new EndCapProfile.EndCapProfilePoint(0.5f, 0.0f, 0.5f, 0.5f));
            //testProfile.ProfilePoints.Add(new EndCapProfile.EndCapProfilePoint(0.7f, 0.1f, 0.7f, 0.7f));
            //testProfile.ProfilePoints.Add(new EndCapProfile.EndCapProfilePoint(1.0f, 0.0f, 1.0f, 1.0f));

            // Stockalike cap
            //testProfile.ProfilePoints.Add(new EndCapProfile.EndCapProfilePoint(0.65f, 0.95f, 0.6f, EndCapProfile.EdgeMode.Sharp ));
            //testProfile.ProfilePoints.Add(new EndCapProfile.EndCapProfilePoint(0.65f, 1.2f, 0.65f, EndCapProfile.EdgeMode.Sharp ));
            //testProfile.ProfilePoints.Add(new EndCapProfile.EndCapProfilePoint(1.03f, 1.2f, 0.95f, EndCapProfile.EdgeMode.Sharp ));
            //testProfile.ProfilePoints.Add(new EndCapProfile.EndCapProfilePoint(1.03f, 0.97f, 1.0f, EndCapProfile.EdgeMode.Sharp ));
            //testProfile.ProfilePoints.Add(new EndCapProfile.EndCapProfilePoint(1.0f, 0.97f, 1.0f  , EndCapProfile.EdgeMode.Sharp, rmode: EndCapProfile.RMode.RELATIVE_TO_SHAPE_RADIUS));

            // tank dome
            //testProfile.ProfilePoints.Add(new EndCapProfile.EndCapProfilePoint(0.172f, 0.875f, 0.2f));
            //testProfile.ProfilePoints.Add(new EndCapProfile.EndCapProfilePoint(0.344f, 0.831f, 0.3f));
            //testProfile.ProfilePoints.Add(new EndCapProfile.EndCapProfilePoint(0.950f, 0.636f, 0.6f, EndCapProfile.EdgeMode.Sharp));
            //testProfile.ProfilePoints.Add(new EndCapProfile.EndCapProfilePoint(0.950f, 1.000f, 0.7f, EndCapProfile.EdgeMode.Sharp));
            //testProfile.ProfilePoints.Add(new EndCapProfile.EndCapProfilePoint(1.000f, 1.000f, 1.000f, EndCapProfile.EdgeMode.Sharp));
            
            //if (endCaps != null)
            //{
            //    if (endCaps.EndCapProfiles != null)
            //    {
            //        Debug.Log(endCaps.blubb);
            //        //part.partInfo.iconPrefab.
            //        foreach (EndCapProfile p in endCaps.EndCapProfiles)
            //        {
            //            Debug.LogWarning("end cap " + p.name);
            //            foreach (EndCapProfile.EndCapProfilePoint pp in p.ProfilePoints)
            //            {
            //                Debug.Log("profile point: " + pp);
            //            }
            //        }
            //    }
            //    else
            //        Debug.LogError("end caps profiles == null");
            //}
            //else
            //    Debug.Log("endCaps == null");

            UncheckedMesh top = null;
            UncheckedMesh bottom = null;

            //if (selectedEndCaps != null /* && selectedEndCap.ProfilePoints.Count >= 2*/)
            //{
                if(selectedEndCaps == null || selectedEndCaps.topCap == null)
                {
                    Debug.Log("Create default top cap");
                    int nVertices = last.circ.totVertexes;
                    int nTriangles = last.circ.totVertexes - 2;
                    top = new UncheckedMesh(nVertices, nTriangles);
                    last.circ.WriteEndcap(last.dia, last.y, true, 0, 0, top, !odd);
                }
                else
                {
                    if (selectedEndCaps != null && selectedEndCaps.topCap.ProfilePoints.Count >= 2)
                    {
                        Debug.Log("Create custom top cap");
                        top = CreateEndCapFromProfile(true, pts, selectedEndCaps.topCap, selectedEndCaps.topCap.invertFaces);
                        //MoveAttachNodes(selectedEndCaps.topCap, true);
                    }
                    else
                    {
                        Debug.Log("Do not create top cap");
                        // TODO, no end cap at the top
                    }
                }

                if (selectedEndCaps == null || selectedEndCaps.bottomCap == null)
                {
                    Debug.Log("Create default bottom cap");
                    int nVertices = first.circ.totVertexes;
                    int nTriangles = first.circ.totVertexes - 2;
                    bottom = new UncheckedMesh(nVertices, nTriangles);
                    first.circ.WriteEndcap(first.dia, first.y, false, 0, 0, bottom, false);
                }
                else
                {
                    if (selectedEndCaps != null && selectedEndCaps.bottomCap.ProfilePoints.Count >= 2)
                    {
                        Debug.Log("Create custom bottom cap");
                        bottom = CreateEndCapFromProfile(false, pts, selectedEndCaps.bottomCap, selectedEndCaps.bottomCap.invertFaces);
                        //MoveAttachNodes(selectedEndCaps.bottomCap, false);
                    }
                    else
                    {
                        Debug.Log("Do not create bottom cap");
                        // TODO no end cap at the bottom
                    }
                }
          
                //if(selectedEndCap.createTop)
                //    top = CreateEndCapFromProfile(true, pts, selectedEndCap, selectedEndCap.invertFaces);
               
               
                //if(selectedEndCap.createBottom)
                //    bottom = CreateEndCapFromProfile(false, pts, selectedEndCap,selectedEndCap.invertFaces);

                //if(top != null && bottom != null)
                //    m = top.Combine(bottom);
                //else
                //{
                //    if (top != null)
                //        m = top;
                //    if (bottom != null)
                //        m = bottom;
                //}

            //}
            //else
            //{
            //    // Write default endcaps.         
            //    nVrt = first.circ.totVertexes + last.circ.totVertexes;
            //    nTri = first.circ.totVertexes - 2 + last.circ.totVertexes - 2;
            //    m = new UncheckedMesh(nVrt, nTri);

            //    first.circ.WriteEndcap(first.dia, first.y, false, 0, 0, m, false);
            //    last.circ.WriteEndcap(last.dia, last.y, true, first.circ.totVertexes, (first.circ.totVertexes - 2) * 3, m, !odd);
            //}



                if (HighLogic.LoadedScene == GameScenes.LOADING)
                {
                    //m.WriteTo(PPart.EndsIconMesh);
                    if (top != null)
                        top.WriteTo(PPart.EndsIconMeshTop);
                    if (bottom != null)
                        bottom.WriteTo(PPart.EndsIconMeshBottom);
                }
                else
                {
                    if (top != null)
                        top.WriteTo(PPart.EndsMeshTop);
                    if (bottom != null)
                        bottom.WriteTo(PPart.EndsMeshBottom);
                    //m.WriteTo(EndsMesh);
                }

            // build the collider mesh at a lower resolution than the visual mesh.
            if (true)//customCollider) // always build a custom collider because the sides mesh does not contain end caps. Which is bad.
            {
                //Debug.LogWarning("Collider mesh vert=" + nColVrt + " tris=" + nColTri);
                
                // collider endcaps
                ProfilePoint firstColPt, lastColPt;

                firstColPt = pts.First(x => x.inCollider);
                lastColPt = pts.Last(x => x.inCollider);

                int nColEndVrt = firstColPt.colliderCirc.totVertexes + lastColPt.colliderCirc.totVertexes;
                int nColEndTri = firstColPt.colliderCirc.totVertexes - 2 + lastColPt.colliderCirc.totVertexes - 2;

                m = new UncheckedMesh(nColVrt+nColEndVrt, nColTri+nColEndTri);
                odd = false;
                {
                    ProfilePoint prev = null;
                    int off = 0, prevOff = 0;
                    int tOff = 0;
                    
                    foreach (ProfilePoint pt in pts)
                    {
                        if (!pt.inCollider)
                            continue;
                        
                        if(prev == null)
                        {
                            pt.colliderCirc.WriteEndcap(pt.dia, pt.y, false, 0, 0, m, odd);
                            off = firstColPt.colliderCirc.totVertexes;
                            tOff = (firstColPt.colliderCirc.totVertexes - 2);
                        }
                        //Debug.LogWarning("Collider circ (" + pt.dia + ", " + pt.y + ") verts=" + pt.colliderCirc.totVertexes);
                        pt.colliderCirc.WriteVertexes(diameter: pt.dia, y: pt.y, v: pt.v, norm: pt.norm, off: off, m: m, odd: odd);
                        if (prev != null)
                        {
                            CirclePoints.WriteTriangles(prev.colliderCirc, prevOff, pt.colliderCirc, off, m.triangles, tOff * 3, !odd);
                            tOff += prev.colliderCirc.totVertexes + pt.colliderCirc.totVertexes;
                        }

                        prev = pt;
                        prevOff = off;
                        off += pt.colliderCirc.totVertexes + 1;
                        odd = !odd;
                    }

                    prev.colliderCirc.WriteEndcap(prev.dia, prev.y, true, off, tOff*3, m, odd);
                }

                if (colliderMesh == null)
                    colliderMesh = new Mesh();

                m.WriteTo(colliderMesh);
                //m.WriteTo(SidesMesh);
                if (colliderMesh.triangles.Length / 3 > 255)
                    Debug.LogWarning("Collider mesh contains " + colliderMesh.triangles.Length / 3 + " triangles. Maximum allowed triangles: 255");

                PPart.ColliderMesh = colliderMesh;
            }
            else
            {
                PPart.ColliderMesh = SidesMesh;
            }

            // updatem all props
            foreach(PartModule pm in GetComponents<PartModule>())
            {
                IProp prop = pm as IProp;
                if(null != prop)
                    prop.UpdateProp();
            }

            RaiseModelAndColliderChanged();
        }

        private Mesh colliderMesh;

        /// <summary>
        /// Subdivide profile points according to the max diameter change. 
        /// </summary>
        private void SubdivHorizontal(LinkedList<ProfilePoint> pts)
        {
            ProfilePoint prev = pts.First.Value;
            for (LinkedListNode<ProfilePoint> node = pts.First.Next; node != null; node = node.Next)
            {
                ProfilePoint curr = node.Value;
                if (!curr.inRender)
                    continue;

                float dDiameter = curr.dia - prev.dia;
                float dPercentage = Math.Abs(curr.dia - prev.dia) / (Math.Max(curr.dia, prev.dia) / 100.0f);
                int subdiv = Math.Min((int)(Math.Truncate(dPercentage / MaxDiameterChange)), 30);
                //int subdiv = Math.Min((int)Math.Truncate(Mathf.Abs(dDiameter) / MaxDiameterChange), 30);
                if (subdiv > 1)
                {
                    // slerp alg for normals  http://http://en.wikipedia.org/wiki/Slerp
                    bool doSlerp = prev.norm != curr.norm;
                    float omega = 0, sinOmega = 0;
                    if (doSlerp)
                    {
                        omega = Mathf.Acos(Vector2.Dot(prev.norm, curr.norm));
                        sinOmega = Mathf.Sin(omega);
                    }

                    for (int i = 1; i < subdiv; ++i)
                    {
                        float t = i / (float)subdiv;
                        float tDiameter = prev.dia + dDiameter * t;
                        float tY = Mathf.Lerp(prev.y, curr.y, t);
                        float tV = Mathf.Lerp(prev.v, curr.v, t);

                        Vector2 norm;
                        if (doSlerp)
                            norm = (Mathf.Sin(omega * (1f - t)) / sinOmega * prev.norm + Mathf.Sin(omega * t) / sinOmega * curr.norm);
                        else
                            norm = prev.norm;

                        pts.AddBefore(node, new ProfilePoint(dia: tDiameter, y: tY, v: tV, norm: norm, inCollider: false));
                    }
                }

                prev = curr;
            }
        }

        private void UpdateNodeSize(ProfilePoint pt, string nodeName)
        {
            AttachNode node = part.attachNodes.Find(n => n.id == nodeName);
            if (node == null)
                return;
            node.size = Math.Min((int)(pt.dia / PPart.diameterLargeStep), 3);

            // Breaking force and torque scales with the area of the surface (node size).
            node.breakingTorque = node.breakingForce = Mathf.Max(50 * node.size * node.size, 50);

            // Send messages for the changing of the ends
            RaiseChangeAttachNodeSize(node, pt.dia, Mathf.PI * pt.dia * pt.dia * 0.25f);

            // TODO: separate out the meshes for each end so we can use the scale for texturing.
            RaiseChangeTextureScale(nodeName, PPart.EndsMaterialTop, new Vector2(pt.dia, pt.dia));
        }

        

        protected void MoveAttachNodes(EndCaps caps)
        {

            foreach (AttachmentNode attachmentNode in attachmentNodes)
            {
                ShapeCoordinates nodePosition = attachmentNode.Position;
                
                if(selectedEndCaps != null && selectedEndCaps.AttachNodes != null)
                {
                    
                    EndCaps.AttachNodePosition overridePosition =
                                selectedEndCaps.AttachNodes.FirstOrDefault(x => x.id == attachmentNode.Node.id);


                    if (overridePosition != null)
                    {
                        if (overridePosition.Coordinates == null)
                        {
                            // create new override coordinates

                            ShapeCoordinates newOverride = new ShapeCoordinates();
                            newOverride.r = overridePosition.r;
                            newOverride.u = overridePosition.u;
                            newOverride.y = overridePosition.y;
                            newOverride.RadiusMode = ShapeCoordinates.RMode.RELATIVE_TO_SHAPE_RADIUS;

                            switch(overridePosition.HeightMode)
                            {
                                case EndCaps.AttachNodePosition.YMode.RELATIVE:
                                    newOverride.HeightMode = ShapeCoordinates.YMode.RELATIVE_TO_SHAPE;
                                    break;

                                case EndCaps.AttachNodePosition.YMode.OFFSET:
                                    newOverride.HeightMode = ShapeCoordinates.YMode.OFFSET_FROM_SHAPE_CENTER;
                                    break;

                                case EndCaps.AttachNodePosition.YMode.OFFSET_FROM_TOP:
                                    newOverride.HeightMode = ShapeCoordinates.YMode.OFFSET_FROM_SHAPE_TOP;
                                    break;
                                case EndCaps.AttachNodePosition.YMode.OFFSET_FROM_BOTTOM:
                                    newOverride.HeightMode = ShapeCoordinates.YMode.OFFSET_FROM_SHAPE_BOTTOM;
                                    break;
                            }

                            overridePosition.Coordinates = newOverride;
                            nodePosition = newOverride;
                        }
                        else
                            nodePosition = overridePosition.Coordinates;
                    }

                }

                attachmentNode.Node.position = attachmentNode.Node.originalPosition = FromCylindricCoordinates(nodePosition);
                AttachNodeChanged(attachmentNode.Node);

            }

            //foreach(AttachNode attachNode in part.attachNodes)
            //{
            //    EndCaps.AttachNodePosition nodePosition = null;
            //    if(caps != null && (nodePosition = caps.AttachNodes.FirstOrDefault( x => x.id == attachNode.id)) != null)
            //    {
            //        // custom cap node
            //        ShapeCoordinates coords = new ShapeCoordinates();
            //        coords.r = nodePosition.r;
            //        coords.u = nodePosition.u;
            //        coords.y = nodePosition.y;
            //        coords.HeightMode = ShapeCoordinates.YMode.RELATIVE_TO_SHAPE;
            //        coords.RadiusMode = ShapeCoordinates.RMode.RELATIVE_TO_TOP_RADIUS;

            //        attachNode.originalPosition = attachNode.position = FromCylindricCoordinates(coords);
            //        AttachNodeChanged(attachNode);
            //    }
            //    else
            //    {
            //        if(attachNode.id == topNodeName)
            //        {
            //            // standard top node
            //            ShapeCoordinates coords = new ShapeCoordinates();
            //            coords.r = 0;
            //            coords.u = 0;
            //            coords.y = 1;
            //            coords.HeightMode = ShapeCoordinates.YMode.RELATIVE_TO_SHAPE;
            //            coords.RadiusMode = ShapeCoordinates.RMode.RELATIVE_TO_TOP_RADIUS;

            //            attachNode.originalPosition = attachNode.position = FromCylindricCoordinates(coords);
            //            AttachNodeChanged(attachNode);
            //        }

            //        if (attachNode.id == bottomNodeName)
            //        {
            //            // standard bottom node
            //            ShapeCoordinates coords = new ShapeCoordinates();
            //            coords.r = 0;
            //            coords.u = 0;
            //            coords.y = -1;
            //            coords.HeightMode = ShapeCoordinates.YMode.RELATIVE_TO_SHAPE;
            //            coords.RadiusMode = ShapeCoordinates.RMode.RELATIVE_TO_BOTTOM_RADIUS;

            //            attachNode.originalPosition = attachNode.position = FromCylindricCoordinates(coords);
            //            AttachNodeChanged(attachNode);
            //        }

                    
            //    }
            //}


            
            //if(profile == null)
            //{
            //    Debug.LogError("profile == null");
            //    return;
            //}
            
            //if (part != null && part.attachNodes != null && profile.AttachNodes != null)
            //{
            //    foreach (EndCapProfile.AttachNodePosition nodePosition in profile.AttachNodes)
            //    {
                    
            //        if (String.IsNullOrEmpty(nodePosition.id))
            //        {
            //            Debug.LogError("invalid id");
            //            break;
            //        }
                   
            //        Debug.Log(nodePosition.ToString());
                    
            //        //AttachNode node = part.attachNodes.FirstOrDefault(n => n.id == nodePosition.id);
            //        AttachNode node = part.findAttachNode(top ? topNodeName : bottomNodeName);
                    
            //        if (node != null)
            //        {
            //            //Debug.Log("found node: " + node.id);
            //            ShapeCoordinates coords = new ShapeCoordinates();

            //            coords.HeightMode = ShapeCoordinates.YMode.RELATIVE_TO_SHAPE;
            //            coords.RadiusMode = ShapeCoordinates.RMode.RELATIVE_TO_SHAPE_RADIUS;
            //            coords.r = nodePosition.r;
            //            coords.y = top ? nodePosition.y : -nodePosition.y;
            //            coords.u = nodePosition.u;

            //            Vector3 newPosition = FromCylindricCoordinates(coords);

                        
            //            Debug.Log("New position: " + newPosition);

            //            Debug.Log("position: " + node.position);
            //            Debug.Log("orig position: " + node.originalPosition);

            //            Debug.Log("orientation: " + node.orientation);
            //            Debug.Log("original orientation: " + node.originalOrientation);

            //            node.position = newPosition;

            //            node.originalPosition = node.position;

            //            try
            //            {
            //                if (GameSceneFilter.AnyEditorOrFlight.IsLoaded())
            //                {
            //                    if (node == null)
            //                        Debug.Log("node == null");
            //                    else
            //                    {
            //                        AttachNodeChanged(node);
                                    
            //                    }
            //                }
            //            }
            //            catch(Exception e)
            //            {
            //                Debug.LogError("Error while sending Node pos change msg");
            //                Debug.LogException(e);
                            
            //            }
                        
            //        }
            //        else
            //        {
            //            Debug.LogWarning("Could not find node: " + nodePosition.id);
            //        }
            //    }
                
            //}
            //else
            //{
            //    Debug.LogWarning("part or caps Attach node list is null");
            //}
            
        }

        protected UncheckedMesh CreateEndCapFromProfile(bool top, LinkedList<ProfilePoint> pts, EndCapProfile profile, bool invertFaces = false)
        {
            ProfilePoint profilePoint = top ? pts.Last.Value : pts.First.Value;

            Vector3[] vertices = new Vector3[profilePoint.circ.totVertexes];

            //bool odd = false;

            bool odd = top ? pts.Count % 2 == 0 : false;

            if (!invertFaces && profile.ProfilePoints.Count % 2 == 0)
                odd = !odd;

            //profilePoint.circ.WriteEndcapVerticies(profilePoint.dia, profilePoint.y, 0, vertices, odd);

            int vertCount = vertices.Length;

            RingMeshBuilder meshBuilder = new RingMeshBuilder(true);

            Vector3[] positions = new Vector3[vertCount];
            Vector2[] uv1 = new Vector2[vertCount];
            Vector2[] uv2 = new Vector2[vertCount];

            foreach(EndCapProfile.EndCapProfilePoint pp in profile.ProfilePoints)
            {
                profilePoint.circ.WriteEndcapVerticies(profilePoint.dia, profilePoint.y, 0, vertices, odd);

                for (int i = 0; i < vertCount; i++)
                {
                    ShapeCoordinates coords = new ShapeCoordinates();
                    //coords.HeightMode = ShapeCoordinates.YMode.RELATIVE_TO_SHAPE;

                    switch(pp.HeightMode)
                    {
                        case EndCapProfile.YMode.RELATIVE:
                            coords.HeightMode = ShapeCoordinates.YMode.RELATIVE_TO_SHAPE;
                            break;
                        case EndCapProfile.YMode.OFFSET:
                            coords.HeightMode = top ? ShapeCoordinates.YMode.OFFSET_FROM_SHAPE_TOP : ShapeCoordinates.YMode.OFFSET_FROM_SHAPE_BOTTOM;
                            break;
                        case EndCapProfile.YMode.OFFSET_FROM_CENTER:
                            coords.HeightMode = ShapeCoordinates.YMode.OFFSET_FROM_SHAPE_CENTER;
                            break;
                    }

                    switch(pp.RadiusMode)
                    {
                        case EndCapProfile.RMode.RELATIVE_TO_CAP_RADIUS:
                            coords.RadiusMode = top ? ShapeCoordinates.RMode.RELATIVE_TO_TOP_RADIUS : ShapeCoordinates.RMode.RELATIVE_TO_BOTTOM_RADIUS;
                            break;

                        case EndCapProfile.RMode.RELATIVE_TO_SHAPE_RADIUS:
                            coords.RadiusMode = ShapeCoordinates.RMode.RELATIVE_TO_SHAPE_RADIUS;
                            break;

                        case EndCapProfile.RMode.OFFSET_TO_CAP_RADIUS:
                            coords.RadiusMode = top ? ShapeCoordinates.RMode.OFFSET_FROM_TOP_RADIUS : ShapeCoordinates.RMode.OFFSET_FROM_BOTTOM_RADIUS;
                            break;
                    }
                    

                    float theta = Mathf.Atan2(-vertices[i].z, vertices[i].x);

                    coords.u = (Mathf.InverseLerp(-Mathf.PI, Mathf.PI, theta) + 0.5f) % 1.0f;

                    //Debug.LogWarning("Last Profile: " + lastProfile.Last.Value.y);
                    //Debug.LogWarning("pts: " + pts.Last.Value.y);

                    //GetCylindricCoordinates(vertices[i], coords);
                    //Debug.LogWarning("r: " + coords.r);
                    coords.r = pp.r;
                    coords.y = top ? pp.yoffset : -pp.yoffset;
                    positions[i] = FromCylindricCoordinates(coords);

                    //positions[i] = vertices[i].xz() * pp.r;
                    //positions[i].y = top ? vertices[i].y + pp.offset : vertices[i].y - pp.offset;

                    uv1[i] = vertices[i].xz2().normalized / 2.0f * pp.uv1 + new Vector2(0.5f, 0.5f);
                    uv2[i] = vertices[i].xz2().normalized / 2.0f * pp.uv2 + new Vector2(0.5f, 0.5f);
                    
                }

                RingMeshBuilder.EdgeMode edgeMode;
                switch(pp.EdgeMode)
                {
                    case EndCapProfile.EdgeMode.SHARP:
                        edgeMode = RingMeshBuilder.EdgeMode.Sharp;
                        break;
                    case EndCapProfile.EdgeMode.SHARP_SEAM:
                        edgeMode = RingMeshBuilder.EdgeMode.SharpSeam;
                        break;
                    case EndCapProfile.EdgeMode.SMOOTH:
                        edgeMode = RingMeshBuilder.EdgeMode.Smooth;
                        break;
                    case EndCapProfile.EdgeMode.SMOOTH_SEAM:
                        edgeMode = RingMeshBuilder.EdgeMode.SmoothSeam;
                        break;
                    default:
                        edgeMode = RingMeshBuilder.EdgeMode.Smooth;
                        break;
                }

                meshBuilder.AddRing(positions, uv1, uv2, edgeMode);

                odd = !odd;
            }

            UncheckedMesh m = meshBuilder.BuildMesh(!invertFaces ? !top : top, 
                                                    profile.closeFirstRing, 
                                                    profile.closeLastRing, 
                                                    profile.invertFirstClosure, 
                                                    profile.invertLastClosure);

            return m;
        }

        #endregion

        #region Circle Points

        public class CirclePoints
        {
            public static CirclePoints ForDiameter(float diameter, float maxError, int minVertexes, int maxVertexes = int.MaxValue)
            {
                int idx = circlePoints.FindIndex(v => (v.totVertexes >= minVertexes) && (v.maxError * diameter * 2f) <= maxError);
                switch (idx)
                {
                    case 0:
                        return circlePoints[0];
                    case -1:
                        CirclePoints prev;
                        if (circlePoints.Count == 0)
                            circlePoints.Add(prev = new CirclePoints(0));
                        else
                            prev = circlePoints.Last();

                        while (prev.totVertexes <= minVertexes)
                            circlePoints.Add(prev = new CirclePoints(prev.subdivCount + 1));

                        while (true)
                        {
                            CirclePoints nxt = new CirclePoints(prev.subdivCount + 1);
                            circlePoints.Add(nxt);
                            if (nxt.totVertexes >= maxVertexes || nxt.maxError * diameter * 2 < maxError)
                                return prev;
                            prev = nxt;
                        }
                    default:
                        return circlePoints[Math.Min(idx - 1, maxVertexes / 4 - 1)];
                }
            }

            public static CirclePoints ForPoints(int vertexes)
            {
                int idx = vertexes / 4 - 1;
                if (idx >= circlePoints.Count)
                {
                    CirclePoints prev = circlePoints.Last();
                    do
                    {
                        circlePoints.Add(prev = new CirclePoints(prev.subdivCount + 1));
                    }
                    while (prev.totVertexes <= vertexes);
                }
                return circlePoints[idx];
            }

            private static readonly List<CirclePoints> circlePoints = new List<CirclePoints>();


            private readonly int subdivCount;
            public readonly int totVertexes;
            private readonly float maxError;

            private static readonly float MaxError0 = Mathf.Sqrt(2) * (Mathf.Sin(Mathf.PI / 4.0f) - 0.5f) * 0.5f;

            private float[][] uCoords;
            private float[][] xCoords;
            private float[][] zCoords;

            private bool complete;

            private CirclePoints(int subdivCount)
            {
                this.subdivCount = subdivCount;
                totVertexes = (1 + subdivCount) * 4;

                if (subdivCount == 0)
                {
                    uCoords = new[] { new[] { 0.0f }, new[] { 0.125f } };
                    xCoords = new[] { new[] { 0.0f }, new[] { -1f / Mathf.Sqrt(2) } };
                    zCoords = new[] { new[] { 1.0f }, new[] { 1f / Mathf.Sqrt(2) } };

                    maxError = MaxError0;
                    complete = true;
                }
                else
                {
                    // calculate the max error.
                    uCoords = new[] { new[] { 0.0f, 1f / totVertexes }, new[] { 0.5f / totVertexes, 1.5f / totVertexes } };
                    float theta = uCoords[0][1] * Mathf.PI * 2.0f;
                    xCoords = new[] { new[] { 0.0f, -Mathf.Sin(theta) }, new[] { -Mathf.Sin(theta * 0.5f), -Mathf.Sin(theta * 1.5f) } };
                    zCoords = new[] { new[] { 1.0f, Mathf.Cos(theta) }, new[] { Mathf.Cos(theta * 0.5f), Mathf.Cos(theta * 1.5f) } };

                    float dX = xCoords[1][0] - xCoords[0][1] / 2.0f;
                    float dY = zCoords[1][0] - (1f + zCoords[0][1]) / 2.0f;

                    maxError = Mathf.Sqrt(dX * dX + dY * dY);

                    complete = subdivCount == 1;
                }
            }

            private void Complete()
            {
                if (complete)
                    return;

                int totalCoords = subdivCount + 1;

                float[][] oldUCoords = uCoords;
                float[][] oldXCoords = xCoords;
                float[][] oldYCoords = zCoords;
                uCoords = new[] { new float[totalCoords], new float[totalCoords] };
                xCoords = new[] { new float[totalCoords], new float[totalCoords] };
                zCoords = new[] { new float[totalCoords], new float[totalCoords] };
                Array.Copy(oldUCoords[0], uCoords[0], 2);
                Array.Copy(oldXCoords[0], xCoords[0], 2);
                Array.Copy(oldYCoords[0], zCoords[0], 2);
                Array.Copy(oldUCoords[1], uCoords[1], 2);
                Array.Copy(oldXCoords[1], xCoords[1], 2);
                Array.Copy(oldYCoords[1], zCoords[1], 2);

                float denom = 4 * (subdivCount + 1);
                for (int i = 2; i <= subdivCount; ++i)
                {
                    uCoords[0][i] = i / denom;
                    uCoords[1][i] = (i + 0.5f) / denom;

                    float theta = uCoords[0][i] * Mathf.PI * 2.0f;
                    float theta1 = uCoords[1][i] * Mathf.PI * 2.0f;
                    xCoords[0][i] = -Mathf.Sin(theta);
                    zCoords[0][i] = Mathf.Cos(theta);
                    xCoords[1][i] = -Mathf.Sin(theta1);
                    zCoords[1][i] = Mathf.Cos(theta1);
                }

                complete = true;
            }

            /// <summary>
            /// writes this.totVerticies + 1 xy, verticies, and tangents and this.totVerticies triangles to the passed arrays for a single endcap.
            /// Callers will need to fill the normals. This will be { 0, 1, 0 } for pt endcap, and { 0, -1, 0 } for bottom.
            /// </summary>
            /// <param name="dia">diameter of circle</param>
            /// <param name="y">y dimension for points</param>
            /// <param name="up">If this endcap faces up</param>
            /// <param name="vOff">offset into xy, verticies, and normal arrays to begin at</param>
            /// <param name="to">offset into triangles array</param>
            /// <param name="m">Mesh to write into</param>
            /// <param name="odd">If this is an odd row</param>
            public void WriteEndcap(float dia, float y, bool up, int vOff, int to, UncheckedMesh m, bool odd)
            {
                Complete();

                int o = odd ? 1 : 0;

                for (int i = 0; i <= subdivCount; ++i)
                {
                    int o0 = vOff + i;
                    m.uv[o0] = new Vector2((-xCoords[o][i] + 1f) * 0.5f, (-zCoords[o][i] + 1f) * 0.5f);
                    m.verticies[o0] = new Vector3(xCoords[o][i] * dia * 0.5f, y, zCoords[o][i] * dia * 0.5f);

                    int o1 = vOff + i + subdivCount + 1;
                    m.uv[o1] = new Vector2((zCoords[o][i] + 1f) * 0.5f, (-xCoords[o][i] + 1f) * 0.5f);
                    m.verticies[o1] = new Vector3(-zCoords[o][i] * dia * 0.5f, y, xCoords[o][i] * dia * 0.5f);

                    int o2 = vOff + i + 2 * (subdivCount + 1);
                    m.uv[o2] = new Vector2((xCoords[o][i] + 1f) * 0.5f, (zCoords[o][i] + 1f) * 0.5f);
                    m.verticies[o2] = new Vector3(-xCoords[o][i] * dia * 0.5f, y, -zCoords[o][i] * dia * 0.5f);

                    int o3 = vOff + i + 3 * (subdivCount + 1);
                    m.uv[o3] = new Vector2((-zCoords[o][i] + 1f) * 0.5f, (xCoords[o][i] + 1f) * 0.5f);
                    m.verticies[o3] = new Vector3(zCoords[o][i] * dia * 0.5f, y, -xCoords[o][i] * dia * 0.5f);

                    m.tangents[o0] = m.tangents[o1] = m.tangents[o2] = m.tangents[o3] = new Vector4(-1, 0, 0, up ? 1 : -1);
                    m.normals[o0] = m.normals[o1] = m.normals[o2] = m.normals[o3] = new Vector3(0, up ? 1 : -1, 0);
                }

                for (int i = 1; i < totVertexes - 1; ++i)
                {
                    m.triangles[to++] = vOff;
                    m.triangles[to++] = vOff + i + (up ? 1 : 0);
                    m.triangles[to++] = vOff + i + (up ? 0 : 1);
                }
            }

            public void WriteEndcapVerticies(float dia, float y, int vOff, Vector3[] verticies, bool odd)
            {
                Complete();

                int o = odd ? 1 : 0;

                for (int i = 0; i <= subdivCount; ++i)
                {
                    int o0 = vOff + i;
                    
                    verticies[o0] = new Vector3(xCoords[o][i] * dia * 0.5f, y, zCoords[o][i] * dia * 0.5f);

                    int o1 = vOff + i + subdivCount + 1;
                    
                    verticies[o1] = new Vector3(-zCoords[o][i] * dia * 0.5f, y, xCoords[o][i] * dia * 0.5f);

                    int o2 = vOff + i + 2 * (subdivCount + 1);
                    
                    verticies[o2] = new Vector3(-xCoords[o][i] * dia * 0.5f, y, -zCoords[o][i] * dia * 0.5f);

                    int o3 = vOff + i + 3 * (subdivCount + 1);
                    
                    verticies[o3] = new Vector3(zCoords[o][i] * dia * 0.5f, y, -xCoords[o][i] * dia * 0.5f);

                }

                
            }

            

            /// <summary>
            /// Write vertexes for the circle.
            /// </summary>
            /// <param name="diameter">diameter of the circle</param>
            /// <param name="y">y coordinate</param>
            /// <param name="norm">unit normal vector along the generator curve for increasing y. The y param becomes the y of the normal, the x multiplies the normals to the circle</param>
            /// <param name="v">v coordinate for UV</param>
            /// <param name="off">offset into following arrays</param>
            /// <param name="odd">If this is an odd row</param>
            /// <param name="m">Mesh to write vertexes into</param>
            public void WriteVertexes(float diameter, float y, float v, Vector2 norm, int off, bool odd, UncheckedMesh m)
            {
                Complete();

                int o = odd ? 1 : 0;

                for (int i = 0; i <= subdivCount; ++i)
                {
                    int o0 = off + i;
                    m.uv[o0] = new Vector2(uCoords[o][i], v);
                    m.verticies[o0] = new Vector3(xCoords[o][i] * 0.5f * diameter, y, zCoords[o][i] * 0.5f * diameter);
                    m.normals[o0] = new Vector3(xCoords[o][i] * norm.x, norm.y, zCoords[o][i] * norm.x);
                    m.tangents[o0] = new Vector4(-zCoords[o][i], 0, xCoords[o][i], -1.0f);
                    //MonoBehaviour.print("Vertex #" + i + " off=" + o0 + " u=" + xy[o0][0] + " coords=" + verticies[o0]);
                    
                    int o1 = off + i + subdivCount + 1;
                    m.uv[o1] = new Vector2(uCoords[o][i] + 0.25f, v);
                    m.verticies[o1] = new Vector3(-zCoords[o][i] * 0.5f * diameter, y, xCoords[o][i] * 0.5f * diameter);
                    m.normals[o1] = new Vector3(-zCoords[o][i] * norm.x, norm.y, xCoords[o][i] * norm.x);
                    m.tangents[o1] = new Vector4(-xCoords[o][i], 0, -zCoords[o][i], -1.0f);

                    int o2 = off + i + 2 * (subdivCount + 1);
                    m.uv[o2] = new Vector2(uCoords[o][i] + 0.50f, v);
                    m.verticies[o2] = new Vector3(-xCoords[o][i] * 0.5f * diameter, y, -zCoords[o][i] * 0.5f * diameter);
                    m.normals[o2] = new Vector3(-xCoords[o][i] * norm.x, norm.y, -zCoords[o][i] * norm.x);
                    m.tangents[o2] = new Vector4(zCoords[o][i], 0, -xCoords[o][i], -1.0f);

                    int o3 = off + i + 3 * (subdivCount + 1);
                    m.uv[o3] = new Vector2(uCoords[o][i] + 0.75f, v);
                    m.verticies[o3] = new Vector3(zCoords[o][i] * 0.5f * diameter, y, -xCoords[o][i] * 0.5f * diameter);
                    m.normals[o3] = new Vector3(zCoords[o][i] * norm.x, norm.y, -xCoords[o][i] * norm.x);
                    m.tangents[o3] = new Vector4(xCoords[o][i], 0, zCoords[o][i], -1.0f);
                }

                // write the wrapping vertex. This is identical to the first one except for u coord += 1
                int lp = off + totVertexes;
                m.uv[lp] = new Vector2(uCoords[o][0] + 1.0f, v);
                m.verticies[lp] = m.verticies[off];
                m.normals[lp] = m.normals[off];
                m.tangents[lp] = m.tangents[off];
            }

            private const float UDelta = 1e-5f;

            public IEnumerable<Vector3> PointsXZU(float uFrom, float uTo)
            {
                Complete();

                int denom = (4 * (subdivCount + 1));

                if (uFrom <= uTo)
                {
                    int iFrom = Mathf.CeilToInt((uFrom + UDelta) * denom);
                    int iTo = Mathf.FloorToInt((uTo - UDelta) * denom);

                    if (iFrom < 0)
                    {
                        int pushUp = (-iFrom / denom + 1) * denom;
                        iFrom += pushUp;
                        iTo += pushUp;
                    }

                    for (int i = iFrom; i <= iTo; ++i)
                        yield return PointXZU(i);
                }
                else
                {
                    int iFrom = Mathf.FloorToInt((uFrom - UDelta) * denom);
                    int iTo = Mathf.CeilToInt((uTo + UDelta) * denom);

                    if (iTo < 0)
                    {
                        int pushUp = (-iTo / denom + 1) * denom;
                        iFrom += pushUp;
                        iTo += pushUp;
                    }

                    for (int i = iFrom; i >= iTo; --i)
                        yield return PointXZU(i);
                }
            }

            private Vector3 PointXZU(int i)
            {
                int o = i % (subdivCount + 1);
                int q = i / (subdivCount + 1) % 4;
                //Debug.LogWarning("PointXZU(" + i + ") o=" + o + " q=" + q + " subdiv=" + subdivCount);
                switch (q)
                {
                    case 0:
                        return new Vector3(xCoords[0][o], zCoords[0][o], uCoords[0][o]);
                    case 1:
                        return new Vector3(-zCoords[0][o], xCoords[0][o], uCoords[0][o] + 0.25f);
                    case 2:
                        return new Vector3(-xCoords[0][o], -zCoords[0][o], uCoords[0][o] + 0.5f);
                    case 3:
                        return new Vector3(zCoords[0][o], -xCoords[0][o], uCoords[0][o] + 0.75f);
                }
                throw new InvalidProgramException("Unreachable code");
            }

            /// <summary>
            /// Creates a.vertexes + b.vertexes triangles to cover the surface between circle a and b.
            /// </summary>
            /// <param name="a">the first circle points</param>
            /// <param name="ao">offset into vertex array for a points</param>
            /// <param name="b">the second circle points</param>
            /// <param name="bo">offset into vertex array for b points</param>
            /// <param name="triangles">triangles array for output</param>
            /// <param name="to">offset into triangles array. This must be a multiple of 3</param>
            /// <param name="odd">Is this an odd row</param>
            public static void WriteTriangles(CirclePoints a, int ao, CirclePoints b, int bo, int[] triangles, int to, bool odd)
            {
                int aq = a.subdivCount + 1, bq = b.subdivCount + 1;
                int ai = 0, bi = 0;
                int ad = (odd ? 1 : 0), bd = (odd ? 0 : 1);

                while (ai < aq || bi < bq)
                {
                    float au = (ai < aq) ? a.uCoords[ad][ai] : (a.uCoords[ad][0] + 0.25f);
                    float bu = (bi < bq) ? b.uCoords[bd][bi] : (b.uCoords[bd][0] + 0.25f);

                    if (au < bu)
                    {
                        //MonoBehaviour.print("A-tri #" + ai + " tOff=" + to);
                        triangles[to++] = ao + ai;
                        triangles[to++] = bo + bi;
                        triangles[to++] = ao + ai + 1;
                        //MonoBehaviour.print(" (" + triangles[to - 3] + ", " + triangles[to - 2] + ", " + triangles[to - 1] + ") ");

                        triangles[to++] = ao + ai + aq;
                        triangles[to++] = bo + bi + bq;
                        triangles[to++] = ao + ai + 1 + aq;
                        //MonoBehaviour.print(" (" + triangles[to - 3] + ", " + triangles[to - 2] + ", " + triangles[to - 1] + ") ");

                        triangles[to++] = ao + ai + 2 * aq;
                        triangles[to++] = bo + bi + 2 * bq;
                        triangles[to++] = ao + ai + 1 + 2 * aq;
                        //MonoBehaviour.print(" (" + triangles[to - 3] + ", " + triangles[to - 2] + ", " + triangles[to - 1] + ") ");

                        triangles[to++] = ao + ai + 3 * aq;
                        triangles[to++] = bo + bi + 3 * bq;
                        triangles[to++] = ao + ai + 1 + 3 * aq;
                        //MonoBehaviour.print(" (" + triangles[to - 3] + ", " + triangles[to - 2] + ", " + triangles[to - 1] + ") ");

                        ++ai;
                    }
                    else
                    {
                        //MonoBehaviour.print("B-tri #" + bi + " tOff=" + to);
                        triangles[to++] = bo + bi;
                        triangles[to++] = bo + bi + 1;
                        triangles[to++] = ao + ai;
                        //MonoBehaviour.print(" (" + triangles[to - 3] + ", " + triangles[to - 2] + ", " + triangles[to - 1] + ") ");

                        triangles[to++] = bo + bi + bq;
                        triangles[to++] = bo + bi + 1 + bq;
                        triangles[to++] = ao + ai + aq;
                        //MonoBehaviour.print(" (" + triangles[to - 3] + ", " + triangles[to - 2] + ", " + triangles[to - 1] + ") ");

                        triangles[to++] = bo + bi + 2 * bq;
                        triangles[to++] = bo + bi + 1 + 2 * bq;
                        triangles[to++] = ao + ai + 2 * aq;
                        //MonoBehaviour.print(" (" + triangles[to - 3] + ", " + triangles[to - 2] + ", " + triangles[to - 1] + ") ");

                        triangles[to++] = bo + bi + 3 * bq;
                        triangles[to++] = bo + bi + 1 + 3 * bq;
                        triangles[to++] = ao + ai + 3 * aq;
                        //MonoBehaviour.print(" (" + triangles[to - 3] + ", " + triangles[to - 2] + ", " + triangles[to - 1] + ") ");

                        ++bi;
                    }
                }
            }

        }
        #endregion


        #region End Caps

        public EndCapList endCaps = new EndCapList();

        [SerializeField]
        public byte[] endCapsSerialized;

        [KSPField(guiName = "End Cap", guiActive = false, guiActiveEditor = true, isPersistant = true), UI_ChooseOption(scene = UI_Scene.Editor)]
        public string endCap;
        public string previousEndCap = "*__*";

        private EndCaps selectedEndCaps;


        public void UpdateEndCaps(bool force = false)
        {
            if(force || endCap != previousEndCap)
            {
                if (endCaps.EndCaps.Count < 1)
                    selectedEndCaps = null;
                else
                {

                    if (endCaps.EndCaps.Count == 1)
                    {
                        selectedEndCaps = endCaps.EndCaps[0];
                        endCap = selectedEndCaps.name;
                    }
                    else
                    {
                        selectedEndCaps = endCaps.EndCaps.FirstOrDefault(x => x.name == endCap);

                        if (selectedEndCaps == null)
                        {
                            selectedEndCaps = endCaps.EndCaps[0];
                            endCap = selectedEndCaps.name;
                        }
                    }
                }
                                   
                UpdateEndCapsTexture();
                //this.UpdateShape(true);
                previousEndCap = endCap;
                ForceNextUpdate();
            }

        }

        protected void ApplyEndcapToMaterial(Material material, EndCapProfile endCap)
        {
            if(material == null || endCap == null)
            {
                Debug.LogError("Can not apply end cap to material");
                return;
            }

            Texture[] textures = Resources.FindObjectsOfTypeAll(typeof(Texture)) as Texture[];

            Texture tex = string.IsNullOrEmpty(endCap.texture) ? null : textures.FirstOrDefault(x => x.name == endCap.texture);
            Texture bump = string.IsNullOrEmpty(endCap.bump) ? null : textures.FirstOrDefault(x => x.name == endCap.bump);

            // Set shaders
            if (!part.Modules.Contains("ModulePaintable"))
            {
                Shader newShader = Shader.Find(bump != null ? "KSP/Bumped Specular" : "KSP/Specular");

                //Debug.Log("new shader: " + newShader.name);
                if (newShader != null)
                    material.shader = newShader;
                else
                {
                    Debug.LogError("Could not find shader for top");
                    return;
                }

            }

            if (null != tex)
                material.SetTexture("_MainTex", tex);


            if (null != bump)
                material.SetTexture("_BumpMap", bump);
            

            material.SetColor("_SpecColor", endCap.specular);
            material.SetFloat("_Shininess", endCap.shininess);
            material.SetTextureScale("_MainTex", endCap.textureScale);
            material.SetTextureScale("_BumpMap", endCap.textureScale);
            
            //Debug.Log("scale: " + selectedEndCaps.topCap.textureScale);

            Vector2 topOffset = new Vector2((1f / endCap.textureScale.x - 1f) / 2f,
                                                (1f / endCap.textureScale.y - 1f) / 2f);


            material.SetTextureOffset("_MainTex", topOffset);

            material.SetTextureOffset("_BumpMap", topOffset);

        }

        protected void ApplyDefaultEndCapToMaterial(Material material)
        {
            ProceduralPart.TextureSet textureSet = PPart.TextureSets.FirstOrDefault(set => set.name == PPart.textureSet);

            if (textureSet != null)
            {
                Texture tex = textureSet.ends;
                Texture bump = null;

                // Set shaders
                if (!part.Modules.Contains("ModulePaintable"))
                {
                    Shader newShader = Shader.Find("KSP/Diffuse");
                    if (newShader != null)
                        material.shader = newShader;
                    else
                        Debug.LogError("Could not find shader");
                }

                if(tex != null)
                    material.SetTexture("_MainTex", tex);

                if (bump != null)
                    material.SetTexture("_BumpMap", bump);

                if (material != null)
                {
                    const float scale = 0.93f;
                    const float offset = (1f / scale - 1f) / 2f;
                    material.mainTextureScale = new Vector2(scale, scale);
                    material.mainTextureOffset = new Vector2(offset, offset);
                }
            }

        }

        public override void UpdateEndCapsTexture()
        {
            Debug.Log("Update end caps tex");
            Material EndsMaterialTop;
            Material EndsMaterialBottom;
            
            if (HighLogic.LoadedScene == GameScenes.LOADING)
            {
                // if we are in loading screen, all changes have to be made to the icon materials. Otherwise all icons will have the same texture 
                EndsMaterialTop = PPart.EndsIconMaterialTop;
                EndsMaterialBottom = PPart.EndsIconMaterialBottom;
               
            }
            else
            {
                EndsMaterialTop = PPart.EndsMaterialTop;
                EndsMaterialBottom = PPart.EndsMaterialBottom;
            }

            //Texture[] textures = Resources.FindObjectsOfTypeAll(typeof(Texture)) as Texture[];
            
            //Texture texTop = null;
            //Texture texBottom = null;
            
            //Texture bumpTop = null;
            //Texture bumpBottom = null;

            if(selectedEndCaps == null || selectedEndCaps.topCap == null)
            {
                // Set default texture from TextureSet

                ApplyDefaultEndCapToMaterial(EndsMaterialTop);
            }
            else
            {
                // Create a custom cap
                ApplyEndcapToMaterial(EndsMaterialTop, selectedEndCaps.topCap);            
            }

            if (selectedEndCaps == null || selectedEndCaps.bottomCap == null)
            {
                // Set default texture from TextureSet

                ApplyDefaultEndCapToMaterial(EndsMaterialBottom);
            }
            else
            {
                // Create a custom cap
                ApplyEndcapToMaterial(EndsMaterialBottom, selectedEndCaps.bottomCap);
            }


            ///////////////////////////////////////////////////////////////////////////////////////////////////

            //if (selectedEndCaps != null)
            //{
            //    texTop = textures.FirstOrDefault(x => x.name == selectedEndCaps.topCap.texture);
            //    texBottom = textures.FirstOrDefault(x => x.name == selectedEndCaps.bottomCap.texture);

            //    bumpTop = textures.FirstOrDefault(x => x.name == selectedEndCaps.topCap.bump);
            //    bumpBottom = textures.FirstOrDefault(x => x.name == selectedEndCaps.bottomCap.bump);
            //}
            //else
            //{
            //    ProceduralPart.TextureSet textureSet = PPart.TextureSets.FirstOrDefault(set => set.name == PPart.textureSet);

            //    if(textureSet != null)
            //    {
            //        texTop = textureSet.ends;
            //        texBottom = textureSet.ends;
            //        bumpTop = null;
            //        bumpBottom = null;
            //    }
            //}

            //// Set shaders
            //if (!part.Modules.Contains("ModulePaintable"))
            //{
            //    Shader newShaderTop = Shader.Find(bumpTop != null ? "KSP/Bumped Specular" : "KSP/Specular");
            //    Shader newShaderBottom = Shader.Find(bumpBottom != null ? "KSP/Bumped Specular" : "KSP/Specular");

            //    //Debug.Log("new shader: " + newShader.name);
            //    if (newShaderTop != null)
            //        EndsMaterialTop.shader = newShaderTop;
            //    else
            //    {
            //        Debug.LogError("Could not find shader for top");
            //    }

            //    if (newShaderBottom != null)
            //        EndsMaterialBottom.shader = newShaderBottom;
            //    else
            //    {
            //        Debug.LogError("Could not find shader for top");
            //    }
            //}

            //if (null != texTop)
            //    EndsMaterialTop.SetTexture("_MainTex", texTop);
            //else
            //    Debug.Log("Could not find texture: " + selectedEndCaps.topCap.texture);

            //if (null != texBottom)
            //    EndsMaterialTop.SetTexture("_MainTex", texBottom);
            //else
            //    Debug.Log("Could not find texture: " + selectedEndCaps.bottomCap.texture);

            //if (null != bumpTop)
            //    EndsMaterialTop.SetTexture("_BumpMap", bumpTop);
            //else
            //    Debug.Log("Could not find bump tex: " + selectedEndCaps.topCap.bump);

            //if (null != bumpBottom)
            //    EndsMaterialTop.SetTexture("_BumpMap", bumpBottom);
            //else
            //    Debug.Log("Could not find bump tex: " + selectedEndCaps.bottomCap.bump);

            //EndsMaterialTop.SetColor("_SpecColor", selectedEndCaps.topCap.specular);
            //EndsMaterialBottom.SetColor("_SpecColor", selectedEndCaps.bottomCap.specular);
            
            //EndsMaterialTop.SetFloat("_Shininess", selectedEndCaps.topCap.shininess);
            //EndsMaterialBottom.SetFloat("_Shininess", selectedEndCaps.bottomCap.shininess);

            //EndsMaterialTop.SetTextureScale("_MainTex", selectedEndCaps.topCap.textureScale);
            //EndsMaterialBottom.SetTextureScale("_MainTex", selectedEndCaps.bottomCap.textureScale);

            //EndsMaterialTop.SetTextureScale("_BumpMap", selectedEndCaps.topCap.textureScale);
            //EndsMaterialBottom.SetTextureScale("_BumpMap", selectedEndCaps.bottomCap.textureScale);

            //Debug.Log("scale: " + selectedEndCaps.topCap.textureScale);

            //Vector2 topOffset = new Vector2((1f / selectedEndCaps.topCap.textureScale.x - 1f) / 2f,
            //                                    (1f / selectedEndCaps.topCap.textureScale.y - 1f) / 2f);

            //Vector2 bottomOffset = new Vector2((1f / selectedEndCaps.bottomCap.textureScale.x - 1f) / 2f,
            //                                    (1f / selectedEndCaps.bottomCap.textureScale.y - 1f) / 2f);

            //EndsMaterialTop.SetTextureOffset("_MainTex", topOffset);
            //EndsMaterialBottom.SetTextureOffset("_MainTex", bottomOffset);

            //EndsMaterialTop.SetTextureOffset("_BumpMap", topOffset);
            //EndsMaterialBottom.SetTextureOffset("_BumpMap", bottomOffset);
            
            
            //EndsMaterial.SetTexture("_MainTex", tex.ends);
            
        }

        #endregion

        #region Attachment Nodes

        protected List<AttachmentNode> attachmentNodes = new List<AttachmentNode>();

        protected struct AttachmentNode
        {
            public AttachNode Node;
            public ShapeCoordinates Position;
        }

        #endregion

    }
}