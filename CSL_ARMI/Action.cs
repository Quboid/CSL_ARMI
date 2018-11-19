using MoveIt;
using System.Collections.Generic;
using UnityEngine;

namespace CSL_ARMI
{
    public class AlignRotationAction : MoveIt.Action
    {
        public float angle;
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


        public override void Do()
        {
            Vector3 PoR;
            Matrix4x4 matrix4x = default(Matrix4x4);
            Bounds bounds = GetTotalBounds(false);

            foreach (InstanceState state in savedStates)
            {
                if (state.instance.isValid)
                {
                    // Rotate in-place
                    if (state.instance is MoveableBuilding mb)
                    {
                        BuildingState bs = (BuildingState)state;

                        float terrainHeight = TerrainManager.instance.SampleOriginalRawHeightSmooth(mb.position);
                        if (Mathf.Abs(terrainHeight - mb.position.y) > 0.01f)
                        {
                            followTerrain = false;
                            mb.AddFixedHeightFlag(mb.id.Building);
                        }
                        else
                        {
                            followTerrain = true;
                            mb.RemoveFixedHeightFlag(mb.id.Building);
                        }

                        float oldAngle = mb.angle;
                        float angleDelta = 0 - mb.angle + angle;
                        PoR = state.position;
                        matrix4x.SetTRS(PoR, Quaternion.AngleAxis(angleDelta * Mathf.Rad2Deg, Vector3.down), Vector3.one);
                        mb.Transform(state, ref matrix4x, 0f, angleDelta, PoR, followTerrain);

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
                    else
                    {
                        //Debug.Log($"State:{state.prefabName}");
                        state.instance.Move(state.instance.position, angle);
                    }
                }
            }

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
