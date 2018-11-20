﻿using MoveIt;
using System.Collections.Generic;
using UnityEngine;

namespace CSL_ARMI
{
    public class AlignRotationAction : MoveIt.Action
    {
        public float newAngle;
        public bool followTerrain;
        public HashSet<InstanceState> savedStates = new HashSet<InstanceState>();
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

        public static Bounds GetTotalBounds()
        {
            Bounds totalBounds = default(Bounds);

            bool init = false;

            foreach (Instance instance in selection)
            {
                if (instance.id.Building > 0 || instance.id.Prop > 0)
                {
                    if (init)
                    {
                        totalBounds.Encapsulate(instance.GetBounds(true));
                    }
                    else
                    {
                        totalBounds = instance.GetBounds(true);
                        init = true;
                    }
                }
            }

            return totalBounds;
        }


        public override void Do()
        {
            Vector3 PoR;
            Matrix4x4 matrix = default(Matrix4x4);
            Bounds bounds = GetTotalBounds();
            float angleDelta, firstValidAngle = 0;

            foreach (InstanceState state in savedStates)
            {
                if (state.instance.isValid)
                {
                    if (state.instance is MoveableBuilding || state.instance is MoveableProp)
                    {
                        firstValidAngle = state.angle;
                        break;
                    }
                }
            }
            angleDelta = 0 - firstValidAngle + newAngle;
            PoR = bounds.center;
            //Debug.Log($"Ready for all, mode is {Mod.mode} - delta:{angleDelta}, PoR:{PoR}, bounds Size:{bounds.size}");

            foreach (InstanceState state in savedStates)
            {
                if (state.instance.isValid)
                {
                    if (state.instance is MoveableBuilding mb)
                    {
                        //BuildingState bs = (BuildingState)state;

                        if (Mathf.Abs(TerrainManager.instance.SampleOriginalRawHeightSmooth(mb.position) - mb.position.y) > 0.01f)
                        {
                            mb.AddFixedHeightFlag(mb.id.Building);
                        }
                        else
                        {
                            mb.RemoveFixedHeightFlag(mb.id.Building);
                        }

                        //float oldAngle = mb.angle;
                        if (Mod.mode == Mod.Mode.Each)
                        {
                            angleDelta = 0 - mb.angle + newAngle;
                            PoR = state.position;
                            //Debug.Log($"Each ({Mod.mode}) - delta:{angleDelta}, PoR:{PoR}, bounds Size:{bounds.size}");
                        }

                        matrix.SetTRS(PoR, Quaternion.AngleAxis(angleDelta * Mathf.Rad2Deg, Vector3.down), Vector3.one);
                        mb.Transform(state, ref matrix, 0f, angleDelta, PoR, followTerrain);

                        BuildingInfo prefab = (BuildingInfo)state.info;
                        ushort id = mb.id.Building;
                        Building building = BuildingManager.instance.m_buildings.m_buffer[id];

                        if (prefab.m_hasParkingSpaces != VehicleInfo.VehicleType.None)
                        {
                            BuildingManager.instance.UpdateParkingSpaces(id, ref building);
                        }

                        BuildingManager.instance.UpdateBuildingRenderer(id, true);

                        //Debug.Log($"Building {state.prefabName} #{mb.id.Building}:{BuildingManager.instance.m_buildings.m_buffer[mb.id.Building].m_angle} (delta:{angleDelta} MB-angle:{mb.angle}, new:{angle}, old:{oldAngle})");
                    }
                    else if (state.instance is MoveableProp mp)
                    {
                        if (Mod.mode == Mod.Mode.Each)
                        {
                            angleDelta = 0 - mp.angle + newAngle;
                            PoR = state.position;
                            //Debug.Log($"Each ({Mod.mode}) - delta:{angleDelta}, PoR:{PoR}, bounds Size:{bounds.size}");
                        }
                        matrix.SetTRS(PoR, Quaternion.AngleAxis(angleDelta * Mathf.Rad2Deg, Vector3.down), Vector3.one);
                        mp.Transform(state, ref matrix, 0f, angleDelta, PoR, followTerrain);

                        //state.instance.Move(state.instance.position, angle);
                    }
                    else
                    {
                        // Invalid Moveable Type
                        //Debug.Log($"State:{state.prefabName}");
                    }
                }
            }

            Mod.mode = Mod.Mode.Off;
            UpdateArea(bounds);
            UpdateArea(GetTotalBounds(false));
        }


        public override void Undo()
        {
            Bounds bounds = GetTotalBounds(false);

            foreach (InstanceState state in savedStates)
            {
                state.instance.SetState(state);
            }

            UpdateArea(bounds);
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
    }
}
