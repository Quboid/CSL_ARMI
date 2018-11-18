using Harmony;
using ICities;
using System.Collections.Generic;
using System.Reflection;
using MoveIt;
using UnityEngine;

namespace CSL_ARMI
{
    public class Mod : LoadingExtensionBase, IUserMod
    {
        public const MoveItTool.ToolState TOOL_KEY = (MoveItTool.ToolState)6;
        public const int TOOL_ACTION_DO = 1;
        private const float XFACTOR = 0.263671875f;
        private const float YFACTOR = 0.015625f;
        private const float ZFACTOR = 0.263671875f;

        public string Name => "Align Rotation for Move It";
        public string Description => "Press Alt+A in Move It and click on a building/prop/decal to align rotation";
        public static bool active = false;

        private static readonly string harmonyId = "quboid.csl_mods.csl_armi";
        private static HarmonyInstance harmonyInstance;
        private static readonly object padlock = new object();
        public static HarmonyInstance GetHarmonyInstance()
        {
            lock (padlock)
            {
                if (harmonyInstance == null)
                {
                    harmonyInstance = HarmonyInstance.Create(harmonyId);
                }

                return harmonyInstance;
            }
        }

        public override void OnLevelLoaded(LoadMode mode)
        {
            HarmonyInstance harmony = GetHarmonyInstance();
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }


        public static bool Deactivate()
        {
            MoveItTool tool = (MoveItTool)ColossalFramework.Singleton<ToolController>.instance.CurrentTool;
            tool.toolState = MoveItTool.ToolState.Default;
            active = false;
            UIToolOptionPanel.RefreshAlignHeightButton();
            Action.UpdateArea(Action.GetTotalBounds(false));
            return false;
        }
    }


    public class MIAlignThreading : ThreadingExtensionBase
    {
        private bool _processed = false;
        private HashSet<InstanceState> m_states = new HashSet<InstanceState>();
        //MoveItTool tool = ColossalFramework.Singleton<MoveItTool>.instance;

        public override void OnUpdate(float realTimeDelta, float simulationTimeDelta)
        {
            if (ColossalFramework.Singleton<ToolController>.instance.CurrentTool is MoveItTool tool)
            {
                if ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetKey(KeyCode.A))
                {
                    if (_processed) return;
                    _processed = true;

                    // Action
                    if (tool.toolState == MoveItTool.ToolState.AligningHeights)
                    {
                        if (Mod.active)
                        { // Switch Off
                            Mod.active = false;
                        }
                        else
                        { // Switch On
                            Mod.active = true;
                        }
                    }
                    else
                    {
                        if (Action.selection.Count > 0)
                        {
                            tool.StartAligningHeights();
                            Mod.active = true;
                        }
                    }

                    //Debug.Log($"Active:{Mod.active} toolState:{tool.toolState}");
                }
                else
                {
                    _processed = false;
                }
            }
        }
    }
}
