using ColossalFramework;
using Harmony;
using MoveIt;
using UnityEngine;
using System;

namespace CSL_ARMI
{
    [HarmonyPatch(typeof(MoveItTool))]
    [HarmonyPatch("StopAligningHeights")]
    class MIT_StopAligningHeights
    {
        public static void Prefix(MoveItTool __instance)
        {
            if (__instance.toolState == MoveItTool.ToolState.AligningHeights && Mod.active)
            {
                Mod.active = false;
                //Debug.Log($"Deactiviting (switched tool)");
            }
        }
    }


    [HarmonyPatch(typeof(MoveItTool))]
    [HarmonyPatch("OnLeftClick")]
    class MIT_OnLeftClick
    {
        public static bool Prefix(MoveItTool __instance, ref Instance ___m_hoverInstance, ref int ___m_nextAction)
        {
            if (__instance == null)
            {
                //Debug.Log("Null instance!");
                return true;
            }

            if (Mod.active)
            {
                float angle;

                if (___m_hoverInstance is MoveableBuilding mb)
                {
                    Building building = Singleton<BuildingManager>.instance.m_buildings.m_buffer[mb.id.Building];

                    angle = building.m_angle;
                }
                else if (___m_hoverInstance is MoveableProp mp)
                {
                    PropInstance prop = Singleton<PropManager>.instance.m_props.m_buffer[mp.id.Prop];
                    angle = prop.Angle;
                }
                else if (___m_hoverInstance is MoveableSegment ms)
                {
                    PropInstance segment = Singleton<PropManager>.instance.m_props.m_buffer[ms.id.Prop];
                    NetSegment[] segmentBuffer = NetManager.instance.m_segments.m_buffer;

                    Vector3 startPos = NetManager.instance.m_nodes.m_buffer[segmentBuffer[ms.id.NetSegment].m_startNode].m_position;
                    Vector3 endPos = NetManager.instance.m_nodes.m_buffer[segmentBuffer[ms.id.NetSegment].m_endNode].m_position;

                    //Debug.Log($"Vector:{endNode.x - startNode.x},{endNode.z - startNode.z} Start:{startNode.x},{startNode.z} End:{endNode.x},{endNode.z}");
                    angle = (float)Math.Atan2(endPos.z - startPos.z, endPos.x - startPos.x);
                }
                else
                {
                    //Debug.Log($"Wrong hover asset type {___m_hoverInstance}<{___m_hoverInstance.GetType()}>");
                    return Mod.Deactivate();
                }

                // Add action to queue for Undo
                AlignRotationAction action = new AlignRotationAction();
                action.angle = angle;
                ActionQueue.instance.Push(action);
                ___m_nextAction = Mod.TOOL_ACTION_DO;

                //Debug.Log($"ROTATION! Angle:{angle}, from {___m_hoverInstance}");
                return Mod.Deactivate();
            }

            return true;
        }
    }


    [HarmonyPatch(typeof(AlignHeightAction))]
    [HarmonyPatch("Undo")]
    class AHA_Undo
    {
        public static void Prefix(MoveItTool __instance)
        {
            Debug.Log($"AHA Undo");
        }
    }
}
