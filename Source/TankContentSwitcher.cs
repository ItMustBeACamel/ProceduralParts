using KSPAPIExtensions;
using KSPAPIExtensions.PartMessage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ProceduralParts
{
    /// <summary>
    /// Module that allows switching the contents of a part between different resources.
    /// The possible contents are set up in the config file, and the user may switch beween
    /// the different possiblities.
    /// 
    /// This is a bit of a light-weight version of RealFuels.
    /// 
    /// One can set this module up on any existing fuel part (Maybe with module manager if you like) 
    /// if you set the volume property in the config file.
    /// 
    /// The class also accepts the message ChangeVolume(float volume) if attached to a dynamic resizing part
    /// such as ProceeduralTanks.
    /// </summary>
    public class TankContentSwitcher : PartModule
    {
        #region Callbacks
        public override void OnAwake()
        {
            base.OnAwake();
            PartMessageService.Register(this);
            //this.RegisterOnUpdateEditor(OnUpdateEditor);
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
                OnUpdateEditor();
        }

        public override void OnLoad(ConfigNode node)
        {
            if (!GameSceneFilter.AnyInitializing.IsLoaded())
                return;
            
            tankTypeOptions = new List<TankTypeOption>();
            foreach (ConfigNode optNode in node.GetNodes("TANK_TYPE_OPTION"))
            {
                TankTypeOption option = new TankTypeOption();
                option.Load(optNode);
                tankTypeOptions.Add(option);
            }
            
            // Unity for some daft reason, and not as according to it's own documentation, won't clone 
            // serializable member fields. Lets DIY.
            tankTypeOptionsSerialized = ObjectSerializer.Serialize(tankTypeOptions);


            try
            {
                if (bool.Parse(node.GetValue("useVolume")))
                {
                    tankVolumeName = PartVolumes.Tankage.ToString();
                }
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch { }
        }

        public override void OnSave(ConfigNode node)
        {
            // Force saved value for enabled to be true.
            node.SetValue("isEnabled", "True");
        }

        public override void OnStart(StartState state)
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (tankVolumeName != null)
                {
                    part.mass = mass;
                    MassChanged(mass);
                }
                isEnabled = enabled = false;
                return;
            }

            InitializeTankType();
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (tankVolume != 0)
                UpdateTankType();
            isEnabled = enabled = HighLogic.LoadedSceneIsEditor;

            if(state == StartState.Editor)
            {
                ProceduralPart pp = GetComponent<ProceduralPart>();
                if(null != pp)
                {
                    pp.AddCostCallback(GetCost);
                }
            }
        }

        public void OnUpdateEditor()
        {
            UpdateTankType();
        }

        #endregion

        #region Tank Volume

        /// <summary>
        /// Volume of part in kilolitres. 
        /// </summary>
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Volume", guiFormat="S4+3", guiUnits="L")]
        public float tankVolume;

        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true, guiName = "Mass")]
        public string massDisplay;

        [KSPField(isPersistant=true)]
        public float mass;

        [KSPField]
        public string tankVolumeName;

        /// <summary>
        /// Message sent from ProceduralAbstractShape when it updates.
        /// </summary>
        [PartMessageListener(typeof(PartVolumeChanged), scenes:~GameSceneFilter.Flight)]
        public void ChangeVolume(string volumeName, float volume)
        {
            if (volumeName != tankVolumeName)
                return;

            if (volume <= 0f)
                throw new ArgumentOutOfRangeException("volume");

            tankVolume = volume;

            UpdateMassAndResources(false);
        }
    
        #endregion

        #region Tank Type

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Tank Type"), UI_ChooseOption(scene=UI_Scene.Editor)]
        public string tankType;

        private TankTypeOption selectedTankType;
        
        public float GetCurrentCostMult()
        {
            if (null != selectedTankType)
                return selectedTankType.costMultiplier;
            else
                return 0; // tank type has not been initialized yet
        }

        // Callback for cost generation. Gets called by the ProceduralPart object.
        public float GetCost()
        {
            if (null != selectedTankType)
            {
                if(null != selectedTankType.costCurveNode)
                {
                    FloatCurve curve = new FloatCurve();
                    curve.Load(selectedTankType.costCurveNode);

                    switch(selectedTankType.costCurveType)
                    {
                        case TankTypeOption.CostCurveType.MASS:
                            return curve.Evaluate(mass);
                        case TankTypeOption.CostCurveType.VOLUME:
                            return curve.Evaluate(tankVolume);
                        default:
                            return 0;
                    }
                }
                else
                {
                    return selectedTankType.costPerkL * tankVolume + selectedTankType.costPerT * mass;
                }
            }
            else
                return 0; // tank type has not been initialized yet
        }

        private List<TankTypeOption> tankTypeOptions;

        // This should be private, but there's a bug in KSP.
        [SerializeField]
        public byte[] tankTypeOptionsSerialized;

        [Serializable]
        public class TankTypeOption : IConfigNode
        {
            public enum CostCurveType // to define the independent axis of the cost curve
            {
                VOLUME,
                MASS,
            }

            [Persistent]
            public string name;
            [Persistent]
            public float dryDensity;
            [Persistent]
            public float massConstant;
            [Persistent]
            public bool isStructural = false;
            [Persistent]
            public float costMultiplier = 1.0f;
            [Persistent]
            public float costPerT = 0.0f;
            [Persistent]
            public float costPerkL = 0.0f;
            [Persistent]
            public CostCurveType costCurveType = CostCurveType.VOLUME;
    
            //[NonSerialized] // this is marked as Serializable, but throws an exception when I try to Serialize it...ouch!
            //public FloatCurve dryDensityCurve = new FloatCurve();

            public ConfigNode densityNode;
            public ConfigNode costCurveNode;
            
           
            public List<TankResource> resources;

            public void Load(ConfigNode node)
            {
                ConfigNode.LoadObjectFromConfig(this, node);
                resources = new List<TankResource>();
                foreach (ConfigNode resNode in node.GetNodes("RESOURCE"))
                {
                    TankResource resource = new TankResource();
                    resource.Load(resNode);
                    resources.Add(resource);
                }
                resources.Sort();

                densityNode = node.GetNode("DRY_MASS");
                costCurveNode = node.GetNode("COST");

            }
            public void Save(ConfigNode node)
            {
                ConfigNode.CreateConfigFromObject(this, node);
                foreach (TankResource resource in resources)
                {
                    ConfigNode resNode = new ConfigNode("RESOURCE");
                    resource.Save(resNode);
                    node.AddNode(resNode);
                }

                if(null != densityNode)
                    node.AddNode(densityNode);
                if (null != costCurveNode)
                    node.AddNode(costCurveNode);


            }
        }


        [Serializable]
        public class TankResource : IConfigNode, IComparable<TankResource>
        {
            public enum ResourceCurveType
            {
                VOLUME_UNITS,
                MASS_UNITS,
                VOLUME_UNITS_PER_VOLUME,
                MASS_UNITS_PER_TON
            }

            
            [Persistent]
            public string name;
            [Persistent]
            public float unitsPerKL;
            [Persistent]
            public float unitsPerT;
            [Persistent]
            public float unitsConst;
            [Persistent]
            public bool isTweakable = true;
            [Persistent]
            public bool forceEmpty = false;

            [Persistent]
            public ConfigNode unitsCurveNode;

            [Persistent]
            public ResourceCurveType curveType = ResourceCurveType.VOLUME_UNITS;

            public void Load(ConfigNode node)
            {
                ConfigNode.LoadObjectFromConfig(this, node);
                unitsCurveNode = node.GetNode("UNITS");
            }
            public void Save(ConfigNode node)
            {
                ConfigNode.CreateConfigFromObject(this, node);
                node.AddNode(unitsCurveNode);
            }

            int IComparable<TankResource>.CompareTo(TankResource other)
            {
                return other == null ? 1 : 
                    string.Compare(name, other.name, StringComparison.Ordinal);
            }

            public double GetMaxAmount(float volume, float mass)
            {
                if(null != unitsCurveNode)
                {
                    FloatCurve amountCurve = new FloatCurve();
                    amountCurve.Load(unitsCurveNode);
                    
                    switch(curveType)
                    {
                        case ResourceCurveType.MASS_UNITS:
                            if (mass >= amountCurve.minTime && mass <= amountCurve.maxTime)
                                return Math.Round(amountCurve.Evaluate(mass), 2);
                            else
                                break;
                            
                        case ResourceCurveType.VOLUME_UNITS:
                            if (volume >= amountCurve.minTime && volume <= amountCurve.maxTime)
                                return Math.Round(amountCurve.Evaluate(volume), 2);
                            else
                                break;

                        case ResourceCurveType.MASS_UNITS_PER_TON:
                            if (mass >= amountCurve.minTime && mass <= amountCurve.maxTime)
                                return Math.Round(amountCurve.Evaluate(mass) * mass, 2);
                            else
                                break;
                        
                        case ResourceCurveType.VOLUME_UNITS_PER_VOLUME:
                            if (volume >= amountCurve.minTime && volume <= amountCurve.maxTime)
                                return Math.Round(amountCurve.Evaluate(volume) * volume, 2);
                            else
                                break;
                    }
                }
                
                return Math.Round(unitsConst + volume * unitsPerKL + mass * unitsPerT, 2);
            }
        }

        private void InitializeTankType()
        {
            // Have to DIY to get the part options deserialized
            if (tankTypeOptionsSerialized != null)
                ObjectSerializer.Deserialize(tankTypeOptionsSerialized, out tankTypeOptions);

            Fields["tankVolume"].guiActiveEditor = tankVolumeName != null; 
            Fields["massDisplay"].guiActiveEditor = tankVolumeName != null;

            if (tankTypeOptions == null || tankTypeOptions.Count == 0)
            {
                Debug.LogError("*TCS* No part type options available");
                return;
            }

            BaseField field = Fields["tankType"];
            UI_ChooseOption options = (UI_ChooseOption)field.uiControlEditor;

            options.options = tankTypeOptions.ConvertAll(opt => opt.name).ToArray();

            field.guiActiveEditor = (tankTypeOptions.Count > 1);

            if (string.IsNullOrEmpty(tankType))
                tankType = tankTypeOptions[0].name;
        }

        private void UpdateTankType()
        {
            if (tankTypeOptions == null || ( selectedTankType != null && selectedTankType.name == tankType))
                return;

            TankTypeOption oldTankType = selectedTankType;

            selectedTankType = tankTypeOptions.Find(opt => opt.name == tankType);

            if (selectedTankType == null)
            {
                if (oldTankType == null)
                {
                    Debug.LogWarning("*TCS* Initially selected part type '" + tankType + "' does not exist, reverting to default");
                    selectedTankType = tankTypeOptions[0];
                    tankType = selectedTankType.name;
                }
                else 
                {
                    Debug.LogWarning("*TCS* Selected part type '" + tankType + "' does not exist, reverting to previous");
                    selectedTankType = oldTankType;
                    tankType = selectedTankType.name;
                    if (HighLogic.LoadedSceneIsEditor)
                        GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
                    return;
                }
            }

            if (selectedTankType.isStructural)
                Fields["tankVolume"].guiActiveEditor = false;
            else
                Fields["tankVolume"].guiActiveEditor = tankVolumeName != null;

            UpdateMassAndResources(true);
            if (HighLogic.LoadedSceneIsEditor)
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
        }

        #endregion

        #region Resources

        [PartMessageEvent]
        public event PartMassChanged MassChanged;
        [PartMessageEvent]
        public event PartResourceListChanged ResourceListChanged;
        [PartMessageEvent]
        public event PartResourceMaxAmountChanged MaxAmountChanged;
        [PartMessageEvent]
        public event PartResourceInitialAmountChanged InitialAmountChanged;

        private void UpdateMassAndResources(bool typeChanged)
        {
            // Wait for the first update...
            if (selectedTankType == null)
                return;

            if (tankVolumeName != null)
            {
                if (null != selectedTankType.densityNode)
                {
                    FloatCurve dryDensityCurve = new FloatCurve();
                    dryDensityCurve.Load(selectedTankType.densityNode);

                    Debug.Log("density curve != null");
                    if (tankVolume >= dryDensityCurve.minTime &&
                    tankVolume <= dryDensityCurve.maxTime)
                    {
                        Debug.Log("inside curve bounds");
                        part.mass = mass = dryDensityCurve.Evaluate(tankVolume);
                    }
                    else
                        part.mass = mass = selectedTankType.dryDensity * tankVolume + selectedTankType.massConstant;

                }
                else
                    part.mass = mass = selectedTankType.dryDensity * tankVolume + selectedTankType.massConstant;
                
                MassChanged(mass);
            }

            // Update the resources list.
            if (typeChanged || !UpdateResources())
                RebuildResources();

            if (tankVolumeName != null)
            {
                double resourceMass = part.Resources.Cast<PartResource>().Sum(r => r.maxAmount*r.info.density);

                float totalMass = part.mass + (float)resourceMass;
                if (selectedTankType.isStructural)
                    massDisplay = MathUtils.FormatMass(totalMass);
                else
                    massDisplay = "Dry: " + MathUtils.FormatMass(part.mass) + " / Wet: " + MathUtils.FormatMass(totalMass);
            }
        }

        [PartMessageListener(typeof(PartResourceInitialAmountChanged), scenes: GameSceneFilter.AnyEditor)]
        public void ResourceChanged(PartResource resource, double amount)
        {
            if(selectedTankType == null)
                return;

            TankResource tankResource = selectedTankType.resources.Find(r => r.name == name);
            if (tankResource == null || !tankResource.forceEmpty)
                return;

            if(resource != null && resource.amount > 0)
            {
                resource.amount = 0;
                InitialAmountChanged(resource, resource.amount);
            }
        }
        
        private bool UpdateResources()
        {
            // Hopefully we can just fiddle with the existing part resources.
            // This saves having to make the window dirty, which breaks dragging on sliders.
            if (part.Resources.Count != selectedTankType.resources.Count)
            {
                Debug.LogWarning("*TCS* Selected and existing resource counts differ");
                return false;
            }

            for (int i = 0; i < part.Resources.Count; ++i)
            {
                PartResource partRes = part.Resources[i];
                TankResource tankRes = selectedTankType.resources[i];

                if (partRes.resourceName != tankRes.name)
                {
                    Debug.LogWarning("*TCS* Selected and existing resource names differ");
                    return false;
                }

                double maxAmount = tankRes.GetMaxAmount(tankVolume, mass); //(float)Math.Round(tankRes.unitsConst + tankVolume * tankRes.unitsPerKL + mass * tankRes.unitsPerT, 2);

                // ReSharper disable CompareOfFloatsByEqualityOperator
                if (partRes.maxAmount == maxAmount)
                    continue;

                if (tankRes.forceEmpty)
                    partRes.amount = 0;
                else if (partRes.maxAmount == 0)
                    partRes.amount = maxAmount;
                else
                {
                    SIPrefix pfx = maxAmount.GetSIPrefix();
                    partRes.amount = pfx.Round(partRes.amount * maxAmount / partRes.maxAmount, 4);
                }
                partRes.maxAmount = maxAmount;
                // ReSharper restore CompareOfFloatsByEqualityOperator

                MaxAmountChanged(partRes, partRes.maxAmount);
                InitialAmountChanged(partRes, partRes.amount);
            }

            return true;
        }

        private void RebuildResources()
        {
            // Purge the old resources
            foreach (PartResource res in part.Resources)
                Destroy(res);
            part.Resources.list.Clear();

            // Build them afresh. This way we don't need to do all the messing around with reflection
            // The downside is the UIPartActionWindow gets maked dirty and rebuit, so you can't use 
            // the sliders that affect part contents properly cos they get recreated underneith you and the drag dies.
            foreach (TankResource res in selectedTankType.resources)
            {
                double maxAmount = res.GetMaxAmount(tankVolume, part.mass); //Math.Round(res.unitsConst + tankVolume * res.unitsPerKL + part.mass * res.unitsPerT, 2);

                ConfigNode node = new ConfigNode("RESOURCE");
                node.AddValue("name", res.name);
                node.AddValue("maxAmount", maxAmount);
                node.AddValue("amount", res.forceEmpty ? 0 : maxAmount);
                node.AddValue("isTweakable", res.isTweakable);
                part.AddResource(node);
            }

            UIPartActionWindow window = part.FindActionWindow();
            if (window != null)
                window.displayDirty = true;

            ResourceListChanged();
        }

        #endregion

        #region Message passing for EPL

        // Extraplanetary launchpads needs these messages sent.
        // From the next update of EPL, this won't be required.

        [PartMessageListener(typeof(PartResourcesChanged))]
        public void ResourcesModified()
        {
            BaseEventData data = new BaseEventData(BaseEventData.Sender.USER);
            data.Set("part", part);
            part.SendEvent("OnResourcesModified", data, 0);
        }

        private float oldmass;

        [PartMessageListener(typeof(PartMassChanged))]
        public void MassModified(float paramMass)
        {
            BaseEventData data = new BaseEventData(BaseEventData.Sender.USER);
            data.Set("part", part);
            data.Set<float>("oldmass", oldmass);
            part.SendEvent("OnMassModified", data, 0);

            oldmass = paramMass;
        }

        #endregion
    }


    public class TankContentSwitcherRealFuels : PartModule
    {
        public override void OnAwake()
        {
            base.OnAwake();
            PartMessageService.Register(this);
            //this.RegisterOnUpdateEditor(OnUpdateEditor);
        }
        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
                OnUpdateEditor();
        }

    #if false
        [KSPField(guiName = "Tank Type", guiActive = false, guiActiveEditor = true, isPersistant = true), UI_ChooseOption(scene = UI_Scene.Editor)]
        public string type;
        private string oldType;

        [KSPField]
        public string[] typesAvailable;
    #endif

        [KSPField(isPersistant=true, guiActive = false, guiActiveEditor = true, guiName = "Utilization", guiFormat = "F0", guiUnits = "%"),
         UI_FloatEdit(scene = UI_Scene.Editor, minValue=0, maxValue=100, incrementSlide = 1f)]
        public float utilization = 100f;
        private float oldUtilization;

        [KSPField(isPersistant=true)]
        public float partVolume;

        /// <summary>
        /// Volume of part in kilolitres. 
        /// </summary>
        [KSPField(guiActive = false, guiActiveEditor = true, guiName = "Volume", guiFormat = "S3+3", guiUnits = "L")]
        public float tankVolume;

        [KSPField(guiActive = false, guiActiveEditor = true, guiName = "Mass")]
        public string massDisplay;

        // ReSharper disable once InconsistentNaming
        [KSPField(isPersistant = false, guiActiveEditor = true, guiActive = false, guiName = "Real Fuels"),
         UI_Toggle(enabledText="GUI", disabledText="GUI")]
        public bool showRFGUI;

        /// <summary>
        /// Real Fuels uses larger volumes than is really there. This factor is applied to the volume prior to it
        /// arriving in real fuels.
        /// </summary>
        [KSPField]
        public float volumeScale = 867.7219117f;


        public override void OnSave(ConfigNode node)
        {
            // Force saved value for enabled to be true.
            node.SetValue("isEnabled", "True");
        }

        private object moduleFuelTanks;
        private MethodInfo fuelManagerGUIMethod;
        private MethodInfo changeVolume;

        public override void OnStart(StartState state)
        {
            if (!part.Modules.Contains("ModuleFuelTanks")
                || HighLogic.LoadedSceneIsFlight)
            {
                isEnabled = enabled = false;
                return;
            }

            moduleFuelTanks = part.Modules["ModuleFuelTanks"];
            fuelManagerGUIMethod = moduleFuelTanks.GetType().GetMethod("fuelManagerGUI", BindingFlags.Public | BindingFlags.Instance);
            changeVolume = moduleFuelTanks.GetType().GetMethod("ChangeVolume", BindingFlags.Public | BindingFlags.Instance);

            GameEvents.onPartActionUIDismiss.Add(p => { if (p == part) showRFGUI = false; });

    #if false
            BaseField typeField = Fields["type"];
            if (typesAvailable != null && typesAvailable.Length > 0)
            {
                UI_ChooseOption typeEditor = (UI_ChooseOption)typeField.uiControlEditor;
                typeEditor.options = typesAvailable;

                if (type == null || !typesAvailable.Contains(type))
                    type = typesAvailable[0];
            }
            else
            {
                typeField.guiActiveEditor = false;
            }
            UpdateTankType();
    #endif
            if (partVolume > 0)
                ChangeVolume(PartVolumes.Tankage.ToString(), partVolume);

            isEnabled = enabled = HighLogic.LoadedSceneIsEditor;
        }

        public void OnUpdateEditor()
        {
            Fields["showRFGUI"].guiActiveEditor = EditorLogic.fetch.editorScreen == EditorLogic.EditorScreen.Parts;

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (utilization != oldUtilization)
            {
                ChangeVolume(PartVolumes.Tankage.ToString(), partVolume);
                oldUtilization = utilization;
            }

            UpdateTankType();
        }

        public void OnGUI()
        {
            EditorLogic editor = EditorLogic.fetch;
            if (!HighLogic.LoadedSceneIsEditor || !showRFGUI || !editor || editor.editorScreen != EditorLogic.EditorScreen.Parts)
            {
                return;
            }

            //Rect screenRect = new Rect(0, 365, 430, (Screen.height - 365));
            Rect screenRect = new Rect((Screen.width-438), 365, 438, (Screen.height - 365));
            //Color reset = GUI.backgroundColor;
            //GUI.backgroundColor = Color.clear;
            GUILayout.Window(part.name.GetHashCode(), screenRect, windowID => fuelManagerGUIMethod.Invoke(moduleFuelTanks, new object[] { windowID }), "Fuel Tanks for " + part.partInfo.title);
            //GUI.backgroundColor = reset;
        }

        /// <summary>
        /// Message sent from ProceduralAbstractShape when it updates.
        /// </summary>
        [PartMessageListener(typeof(PartVolumeChanged), scenes:GameSceneFilter.AnyEditor)]
        public void ChangeVolume(string volumeName, float volume)
        {
            if (volumeName != PartVolumes.Tankage.ToString())
                return;

            // Need to call ChangeVolume in Modular Fuel Tanks
            partVolume = volume;
            tankVolume = volume * utilization / 100f;
            if (tankVolume > 0 && moduleFuelTanks != null)
                changeVolume.Invoke(moduleFuelTanks, new object[] { Math.Round(tankVolume * volumeScale) });
        }

        [KSPEvent (guiActive=false, active = true)]
        public void OnMassModified(BaseEventData data)
        {
            if (HighLogic.LoadedSceneIsFlight || moduleFuelTanks == null)
                return;

            UpdateMassDisplay();
        }

        [KSPEvent(guiActive=false, active=true)]
        public void OnResourcesModified(BaseEventData data)
        {
            if (HighLogic.LoadedSceneIsFlight || moduleFuelTanks == null)
                return;

            UIPartActionWindow window = part.FindActionWindow();
            if (window != null)
                window.displayDirty = true;
        }

        private void UpdateTankType()
        {
    #if false
            if (type == null || oldType == type)
                return;

            //part.SendPartMessage(PartMessageScope.Default | PartMessageScope.IgnoreAttribute, "SwitchTankType", type);
            //UpdateMassDisplay();

            oldType = type;
    #endif
        }

        private void UpdateMassDisplay()
        {
            if (part == null)
                return;

            if (part.Resources.Count == 0)
                massDisplay = MathUtils.FormatMass(part.mass);
            else
            {
                double resourceMass = part.Resources.Cast<PartResource>().Sum(r => r.maxAmount*r.info.density);

                float totalMass = part.mass + (float)resourceMass;
                massDisplay = "Dry: " + MathUtils.FormatMass(part.mass) + " / Wet: " + MathUtils.FormatMass(totalMass);
            }
        }
    }
}