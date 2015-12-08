using UnityEngine;
using KSPAPIExtensions;

namespace ProceduralParts
{
    class CapTextureTweaker : PartModule
    {
        CapEditor capEditor;
        ProceduralAbstractShape currentShape = null;

        //EndCaps currentEndCaps = null;

        ProceduralPart pPart;

        EndCapProfile currentProfile = null;

        [KSPField(category = "textureTweaker", guiActive = false, guiActiveEditor = true, guiFormat = "F4", guiName = "tex offset X"),
            UI_FloatEdit(scene = UI_Scene.Editor, incrementLarge =0.1f, incrementSmall =0.01f, incrementSlide = 0.001f)]
        public float texOffsetX;

        [KSPField(category = "textureTweaker", guiActive = false, guiActiveEditor = true, guiFormat = "F4", guiName = "tex offset Y"),
            UI_FloatEdit(scene = UI_Scene.Editor, incrementLarge = 0.1f, incrementSmall = 0.01f, incrementSlide = 0.001f)]
        public float texOffsetY;

        [KSPField(category = "textureTweaker", guiActive = false, guiActiveEditor = true, guiFormat = "F4", guiName = "tex scale X"),
            UI_FloatEdit(scene = UI_Scene.Editor, incrementLarge = 0.1f, incrementSmall = 0.01f, incrementSlide = 0.001f)]
        public float texScaleX;

        [KSPField(category = "textureTweaker", guiActive = false, guiActiveEditor = true, guiFormat = "F4", guiName = "tex scale Y"),
            UI_FloatEdit(scene = UI_Scene.Editor, incrementLarge = 0.1f, incrementSmall = 0.01f, incrementSlide = 0.001f)]
        public float texScaleY;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            pPart = GetComponent<ProceduralPart>();
            capEditor = GetComponent<CapEditor>();

        }

        void Update()
        {
            //ProceduralAbstractSoRShape sorShape = null;
            Vector2 texOffset = new Vector2(texOffsetX, texOffsetY);
            Vector2 texScale = new Vector2(texScaleX, texScaleY);

            /////////////////////////////////////////////////////////////

            if (pPart.CurrentShape != currentShape)
            {
                Debug.Log("shape changed");
                currentShape = pPart.CurrentShape;
            }

            if(null != capEditor)
            {
                if(currentProfile != capEditor.CurrentProfile)
                {
                    // currently selected Profile has changed
                    currentProfile = capEditor.CurrentProfile;

                    if (null != currentProfile)
                    {
                        texOffset = currentProfile.textureOffset;
                        texScale = currentProfile.textureScale;
                        texOffsetX = texOffset.x;
                        texOffsetY = texOffset.y;

                        texScaleX = texScale.x;
                        texScaleY = texScale.y;

                        Fields["texOffsetX"].guiActiveEditor = true;
                        Fields["texOffsetY"].guiActiveEditor = true;
                        Fields["texScaleX"].guiActiveEditor  = true;
                        Fields["texScaleY"].guiActiveEditor  = true;
                    }
                    else
                    {
                        Fields["texOffsetX"].guiActiveEditor = false;
                        Fields["texOffsetY"].guiActiveEditor = false;
                        Fields["texScaleX"].guiActiveEditor  = false;
                        Fields["texScaleY"].guiActiveEditor  = false;
                    }
                }
                else if(null != currentProfile)
                {
                    bool updateCap = false;

                    if (texOffset != currentProfile.textureOffset)
                    {
                        currentProfile.textureOffset = texOffset;
                        updateCap = true;
                    }

                    if (texScale != currentProfile.textureScale)
                    {
                        currentProfile.textureScale = texScale;
                        updateCap = true;
                    }

                    if (updateCap)
                    {
                        Debug.Log("Updating end cap");
                        currentShape.UpdateEndCapsTexture();
                    }
                }
            }


            /////////////////////////////////////////////////////////////

            //if (pPart.CurrentShape != currentShape)
            //{
            //    Debug.Log("shape changed");
            //    currentShape = pPart.CurrentShape;
            //}

            //if (currentShape != null)
            //    sorShape = currentShape as ProceduralAbstractSoRShape;

            //if (sorShape != null)
            //{
            //    if (sorShape.SelectedEndCaps != currentEndCaps)
            //    {
            //        Debug.Log("caps changed");
            //        currentEndCaps = sorShape.SelectedEndCaps;

            //        if (currentEndCaps != null)
            //        {
            //            texOffset = currentEndCaps.topCap.textureOffset;
            //            texScale = currentEndCaps.topCap.textureScale;
            //            texOffsetX = texOffset.x;
            //            texOffsetY = texOffset.y;

            //            texScaleX = texScale.x;
            //            texScaleY = texScale.y;
            //        }
            //    }
            //    else
            //    {
            //        bool updateCap = false;

            //        if (texOffset != currentEndCaps.topCap.textureOffset)
            //        {
            //            currentEndCaps.topCap.textureOffset = texOffset;
            //            updateCap = true;
            //        }

            //        if (texScale != currentEndCaps.topCap.textureScale)
            //        {
            //            currentEndCaps.topCap.textureScale = texScale;
            //            updateCap = true;
            //        }

            //        if(updateCap)
            //        {
            //            Debug.Log("Updating end cap");
            //            currentShape.UpdateEndCapsTexture();
            //        }
            //    }

            //}

        }
    }
}
