using ColossalFramework;
using Harmony;
using MoveIt;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CSL_ARMI
{
    [HarmonyPatch(typeof(MoveItTool))]
    [HarmonyPatch("StopAligningHeights")]
    class MIT_StopAligningHeights
    {
        public static void Prefix(MoveItTool __instance)
        {
            if (__instance.toolState == MoveItTool.ToolState.AligningHeights && Mod.mode != Mod.Mode.Off)
            {
                // User switched tool
                Mod.mode = Mod.Mode.Off;
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

            if (Mod.mode != Mod.Mode.Off)
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
                    //Debug.Log($"Wrong hover asset type <{___m_hoverInstance.GetType()}>");
                    return Mod.Deactivate();
                }

                // Add action to queue, also enables Undo/Redo
                AlignRotationAction action;
                switch (Mod.mode)
                {
                    case Mod.Mode.All:
                        action = new AlignGroupRotationAction();
                        break;

                    default:
                        action = new AlignEachRotationAction();
                        break;
                }
                action.newAngle = angle;
                action.followTerrain = MoveItTool.followTerrain;
                ActionQueue.instance.Push(action);
                ___m_nextAction = Mod.TOOL_ACTION_DO;

                //Debug.Log($"Angle:{angle}, from {___m_hoverInstance}");
                return Mod.Deactivate(false);
            }

            return true;
        }
    }


    [HarmonyPatch(typeof(MoveableBuilding))]
    [HarmonyPatch("Transform")]
    class MB_Transform
    {
        public static void Postfix(InstanceState instanceState, ref Matrix4x4 matrix4x, float deltaAngle, Vector3 center, bool followTerrain)
        {
            BuildingState state = instanceState as BuildingState;
            Vector3 newPosition = matrix4x.MultiplyPoint(state.position - center);

            if (state.subStates != null)
            {
                foreach (InstanceState subState in state.subStates)
                {
                    if (subState is BuildingState bs)
                    {
                        if (bs.subStates != null)
                        {
                            foreach (InstanceState subSubState in bs.subStates)
                            {
                                Vector3 subPosition = subSubState.position - center;
                                subPosition = matrix4x.MultiplyPoint(subPosition);
                                subPosition.y = subSubState.position.y - state.position.y + newPosition.y;

                                subSubState.instance.Move(subPosition, subSubState.angle + deltaAngle);
                            }
                        }
                    }
                }
            }
        }
    }


    /* Move It! sub-sub-building fix */

    [HarmonyPatch(typeof(MoveableBuilding))]
    [HarmonyPatch("GetState")]
    class MB_GetState
    {
        protected static Building[] buildingBuffer = BuildingManager.instance.m_buildings.m_buffer;

        public static InstanceState Postfix(InstanceState state, ref MoveableBuilding __instance)
        {
            List<InstanceState> subSubStates = new List<InstanceState>();
            BuildingState buildingState = (BuildingState)state;

            if (buildingState.subStates != null)
            {
                foreach (InstanceState subState in buildingState.subStates)
                {
                    if (subState != null)
                    {
                        if (subState is BuildingState subBuildingState)
                        {
                            if (subBuildingState.instance != null && subBuildingState.instance.isValid)
                            {
                                BuildingState ss = (BuildingState)subState;
                                MoveableBuilding subInstance = (MoveableBuilding)subBuildingState.instance;
                                subSubStates.Clear();

                                ushort parent = buildingBuffer[subInstance.id.Building].m_parentBuilding; // Hack to get around Move It's single layer check
                                buildingBuffer[subInstance.id.Building].m_parentBuilding = 0;
                                foreach (Instance subSubInstance in subInstance.subInstances)
                                {
                                    if (subSubInstance != null && subSubInstance.isValid)
                                    {
                                        subSubStates.Add(subSubInstance.GetState());
                                    }
                                }
                                buildingBuffer[subInstance.id.Building].m_parentBuilding = parent;

                                if (subSubStates.Count > 0)
                                {
                                    ss.subStates = subSubStates.ToArray();
                                }
                            }
                        }
                    }
                }
            }

            return state;
        }
    }


    [HarmonyPatch(typeof(MoveableBuilding))]
    [HarmonyPatch("SetState")]
    class MB_SetState
    {
        protected static Building[] buildingBuffer = BuildingManager.instance.m_buildings.m_buffer;

        public static void Postfix(InstanceState state, MoveableBuilding __instance)
        {
            if (!(state is BuildingState buildingState)) {
                return;
            }

            //Debug.Log($"SS0 - {buildingState.subStates}");
            if (buildingState.subStates != null)
            {
                foreach (InstanceState subState in buildingState.subStates)
                {
                    if (subState != null)
                    {
                        if (subState is BuildingState subBuildingState)
                        {
                            if (subBuildingState.instance != null && subBuildingState.instance.isValid)
                            {
                                BuildingState ss = (BuildingState)subState;
                                MoveableBuilding subInstance = (MoveableBuilding)subBuildingState.instance;
                                if (ss.subStates != null)
                                {
                                    foreach (InstanceState subSubState in ss.subStates)
                                    {
                                        if (subSubState != null)
                                        {
                                            subSubState.instance.SetState(subSubState);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
