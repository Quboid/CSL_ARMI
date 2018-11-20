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
        public string Name => "Align Rotation for Move It";
        public string Description => "Press Alt+A in Move It and click on a building/prop/decal to align rotation";

        public enum Mode {Off, Each, All };
        public const MoveItTool.ToolState TOOL_KEY = (MoveItTool.ToolState)6;
        public const int TOOL_ACTION_DO = 1;
        public static Mode mode = Mode.Off;

        private static readonly string harmonyId = "quboid.csl_mods.csl_armi";
        private static HarmonyInstance harmonyInstance;
        private static readonly object padlock = new object();


        public override void OnLevelLoaded(LoadMode loadMode)
        {
            HarmonyInstance harmony = GetHarmonyInstance();
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }


        public static bool Deactivate(bool switchMode = true)
        {
            if (switchMode)
            {
                mode = Mode.Off;
            }
            MoveItTool tool = (MoveItTool)ColossalFramework.Singleton<ToolController>.instance.CurrentTool;
            tool.toolState = MoveItTool.ToolState.Default;
            UIToolOptionPanel.RefreshAlignHeightButton();
            Action.UpdateArea(Action.GetTotalBounds(false));
            return false;
        }


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
                    if (Mod.mode != Mod.Mode.Off)
                    { // Switch Off
                        Mod.Deactivate();
                    }
                    else
                    {
                        if (Action.selection.Count > 0)
                        {
                            if (tool.toolState != MoveItTool.ToolState.AligningHeights)
                            {
                                tool.StartAligningHeights();
                            }
                            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) {
                                Mod.mode = Mod.Mode.All;
                            }
                            else
                            {
                                Mod.mode = Mod.Mode.Each;
                            }
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
