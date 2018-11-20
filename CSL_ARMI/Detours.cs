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
                    //Debug.Log($"Wrong hover asset type {___m_hoverInstance}<{___m_hoverInstance.GetType()}>");
                    return Mod.Deactivate();
                }

                // Add action to queue, also enables Undo/Redo
                AlignRotationAction action = new AlignRotationAction();
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

            //Debug.Log($"MB Transform");
            if (state.subStates != null)
            {
                //Debug.Log($"MB subState not null");
                foreach (InstanceState subState in state.subStates)
                {
                    //Debug.Log($"MB subState");
                    if (subState is BuildingState bs)
                    {
                        //Debug.Log($"MB subState is BuildingState");
                        if (bs.subStates != null)
                        {
                            //Debug.Log($"MB subSubStates not null");
                            foreach (InstanceState subSubState in bs.subStates)
                            {
                                //Debug.Log($"MB subSubState");
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

            //Debug.Log($"GS0 - {buildingState.subStates}");
            if (buildingState.subStates != null)
            {
                foreach (InstanceState subState in buildingState.subStates)
                {
                    //Debug.Log($"GS 2{subState}");
                    if (subState != null)
                    {
                        if (subState is BuildingState subBuildingState)
                        {
                            //Debug.Log($"GS4");
                            if (subBuildingState.instance != null && subBuildingState.instance.isValid)
                            {
                                BuildingState ss = (BuildingState)subState;
                                MoveableBuilding subInstance = (MoveableBuilding)subBuildingState.instance;
                                subSubStates.Clear();

                                //Debug.Log($"GS5 - {subInstance.subInstances}");
                                ushort parent = buildingBuffer[subInstance.id.Building].m_parentBuilding; // Hack to get around Move It's single layer check
                                buildingBuffer[subInstance.id.Building].m_parentBuilding = 0;
                                foreach (Instance subSubInstance in subInstance.subInstances)
                                {
                                    //Debug.Log($"GS6");
                                    if (subSubInstance != null && subSubInstance.isValid)
                                    {
                                        //Debug.Log($"GS7 {subSubInstance}");
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
                //Debug.Log($"SS1");
                foreach (InstanceState subState in buildingState.subStates)
                {
                    //Debug.Log($"SS2 {subState}");
                    if (subState != null)
                    {
                        if (subState is BuildingState subBuildingState)
                        {
                            //Debug.Log($"SS4");
                            if (subBuildingState.instance != null && subBuildingState.instance.isValid)
                            {
                                BuildingState ss = (BuildingState)subState;
                                MoveableBuilding subInstance = (MoveableBuilding)subBuildingState.instance;
                                //Debug.Log($"SS5 - {subInstance.subInstances}");
                                if (ss.subStates != null)
                                {
                                    foreach (InstanceState subSubState in ss.subStates)
                                    {
                                        //Debug.Log($"SS6");
                                        if (subSubState != null)
                                        {
                                            //Debug.Log($"SS7 {subSubState}");
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
