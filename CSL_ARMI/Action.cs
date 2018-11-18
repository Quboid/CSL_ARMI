using ColossalFramework;
using MoveIt;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace CSL_ARMI
{
    public class AlignRotationAction : TransformAction
    {
        public float angle;
        protected static Building[] buildingBuffer = BuildingManager.instance.m_buildings.m_buffer;

        public AlignRotationAction()
        {
            foreach (Instance instance in selection)
            {
                if (instance.isValid)
                {
                    savedStates.Add(instance.GetState());
                }
            }
        }


        public override void Do()
        {
            Vector3 PoR = new Vector3(0f, 0f, 0f);
            Matrix4x4 matrix4x = default(Matrix4x4);

            foreach (InstanceState state in savedStates)
            {
                Debug.Log($"State:{state.prefabName}");
                if (state.instance.isValid)
                {
                    // Rotate around central point


                    // Rotate in-place
                    if (state.instance is MoveableBuilding mb)
                    {
                        PoR = state.position;
                        matrix4x.SetTRS(PoR, Quaternion.AngleAxis(angleDelta * Mathf.Rad2Deg, Vector3.down), Vector3.one);
                        mb.Transform(state, ref matrix4x, 0f, angle, PoR, followTerrain);


                        BuildingInfo prefab = (BuildingInfo)state.info;
                        ushort id = mb.id.Building;
                        Building building = BuildingManager.instance.m_buildings.m_buffer[id];

                        float terrainHeight = TerrainManager.instance.SampleOriginalRawHeightSmooth(mb.position);
                        if (Mathf.Abs(terrainHeight - mb.position.y) > 0.01f)
                        {
                            mb.AddFixedHeightFlag(mb.id.Building);
                        }
                        else
                        {
                            mb.RemoveFixedHeightFlag(mb.id.Building);
                        }

                        //if (info.m_subBuildings != null && info.m_subBuildings.Length != 0)
                        //{
                        //    //Debug.Log($"Can't align building with sub-buildings");
                        //    continue;
                        //}

                        if (prefab.m_hasParkingSpaces != VehicleInfo.VehicleType.None)
                        {
                            BuildingManager.instance.UpdateParkingSpaces(id, ref building);
                        }


                        BuildingManager.instance.UpdateBuildingRenderer(id, true);

                        Debug.Log($"Building {mb.id.Building}:{mb.angle} ({BuildingManager.instance.m_buildings.m_buffer[mb.id.Building].m_angle}), new:{angle}");
                        Debug.Log($"{state.position},{state.instance.position}");
                    }
                    else
                    {
                        state.instance.Move(state.instance.position, angle);
                    }
                    //else if (state.instance is MoveableProp mp)
                    //{
                    //    Debug.Log($"Prop {mp.id.Prop}:{mp.angle} ({PropManager.instance.m_props.m_buffer[mp.id.Prop].m_angle},{PropManager.instance.m_props.m_buffer[mp.id.Prop].Angle}), new:{angle}");
                    //}
                }
            }

            UpdateArea(GetTotalBounds(false));
        }


        public override void Undo()
        {
            foreach (InstanceState state in savedStates)
            {
                state.instance.SetState(state);
            }

            UpdateArea(GetTotalBounds(false));
        }


        public override void ReplaceInstances(Dictionary<Instance, Instance> toReplace)
        {
            foreach (InstanceState state in savedStates)
            {
                if (toReplace.ContainsKey(state.instance))
                {
                    DebugUtils.Log("AlignRotationAction Replacing: " + state.instance.id.RawData + " -> " + toReplace[state.instance].id.RawData);
                    state.ReplaceInstance(toReplace[state.instance]);
                }
            }
        }


        private static void AddToGrid(ushort building, ref Building data)
        {
            int num = Mathf.Clamp((int)(data.m_position.x / 64f + 135f), 0, 269);
            int num2 = Mathf.Clamp((int)(data.m_position.z / 64f + 135f), 0, 269);
            int num3 = num2 * 270 + num;
            while (!Monitor.TryEnter(BuildingManager.instance.m_buildingGrid, SimulationManager.SYNCHRONIZE_TIMEOUT))
            {
            }
            try
            {
                buildingBuffer[(int)building].m_nextGridBuilding = BuildingManager.instance.m_buildingGrid[num3];
                BuildingManager.instance.m_buildingGrid[num3] = building;
            }
            finally
            {
                Monitor.Exit(BuildingManager.instance.m_buildingGrid);
            }
        }

        private static void RemoveFromGrid(ushort building, ref Building data)
        {
            BuildingManager buildingManager = BuildingManager.instance;

            BuildingInfo info = data.Info;
            int num = Mathf.Clamp((int)(data.m_position.x / 64f + 135f), 0, 269);
            int num2 = Mathf.Clamp((int)(data.m_position.z / 64f + 135f), 0, 269);
            int num3 = num2 * 270 + num;
            while (!Monitor.TryEnter(buildingManager.m_buildingGrid, SimulationManager.SYNCHRONIZE_TIMEOUT))
            {
            }
            try
            {
                ushort num4 = 0;
                ushort num5 = buildingManager.m_buildingGrid[num3];
                int num6 = 0;
                while (num5 != 0)
                {
                    if (num5 == building)
                    {
                        if (num4 == 0)
                        {
                            buildingManager.m_buildingGrid[num3] = data.m_nextGridBuilding;
                        }
                        else
                        {
                            buildingBuffer[(int)num4].m_nextGridBuilding = data.m_nextGridBuilding;
                        }
                        break;
                    }
                    num4 = num5;
                    num5 = buildingBuffer[(int)num5].m_nextGridBuilding;
                    if (++num6 > 49152)
                    {
                        CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                        break;
                    }
                }
                data.m_nextGridBuilding = 0;
            }
            finally
            {
                Monitor.Exit(buildingManager.m_buildingGrid);
            }
            if (info != null)
            {
                Singleton<RenderManager>.instance.UpdateGroup(num * 45 / 270, num2 * 45 / 270, info.m_prefabDataLayer);
            }
        }
    }
}
