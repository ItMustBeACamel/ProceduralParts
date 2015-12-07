using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSPAPIExtensions;


namespace ProceduralParts
{
    class CapEditor : PartModule
    {
        ProceduralAbstractShape currentShape = null;

        EndCaps currentEndCaps = null;

        ProceduralPart pPart;

        EndCapProfile currentProfile;

        UI_ChooseOption chooseOption;

        [UI_ChooseOption(controlEnabled =true, scene = UI_Scene.Editor)]
        public string currentProfileName;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            pPart = GetComponent<ProceduralPart>();

            chooseOption = (UI_ChooseOption)Fields["currentProfileName"].uiControlEditor;

            

        }

        void Update()
        {          

            if (pPart.CurrentShape != currentShape)
            {
                Debug.Log("shape changed");
                currentShape = pPart.CurrentShape;
            }

            ProceduralAbstractSoRShape sorShape = null;

            if (currentShape != null)
                sorShape = currentShape as ProceduralAbstractSoRShape;

            if (sorShape != null)
            {
                if (sorShape.SelectedEndCaps != currentEndCaps)
                {
                    Debug.Log("caps changed");
                    currentEndCaps = sorShape.SelectedEndCaps;

                    if (currentEndCaps.topCap == null && currentEndCaps.bottomCap == null)
                    {
                        chooseOption.controlEnabled = false;
                    }
                    else
                    {
                        if (currentEndCaps.topCap == currentEndCaps.bottomCap)
                        {
                            string[] options = { "BOTH" };
                            chooseOption.options = options;
                            chooseOption.controlEnabled = true;

                        }
                        else
                        {
                            if (currentEndCaps.topCap != null && currentEndCaps.bottomCap == null)
                            {
                                string[] options = { "TOP" };
                                chooseOption.options = options;
                            }
                            else if (currentEndCaps.topCap == null && currentEndCaps.bottomCap != null)
                            {
                                string[] options = { "BOTTOM" };
                                chooseOption.options = options;
                            }
                            chooseOption.controlEnabled = true;
                        }
                    }
                }
            }       
        }

        T GetCurrentShape<T>() where T : ProceduralAbstractShape
        {
            if (currentShape == null)
                return null;
            else
                return currentShape as T;

        }

    }
}
