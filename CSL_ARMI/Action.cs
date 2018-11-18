using MoveIt;
using System.Collections.Generic;
using UnityEngine;

namespace CSL_ARMI
{
    public class AlignRotationAction : Action
    {
        public float angle;
        private HashSet<InstanceState> m_states = new HashSet<InstanceState>();

        public AlignRotationAction()
        {
            foreach (Instance instance in selection)
            {
                if (instance.isValid)
                {
                    m_states.Add(instance.GetState());
                }
            }
        }


        public override void Do()
        {
            foreach (InstanceState state in m_states)
            {
                Debug.Log($"State:{state.prefabName}");
                if (state.instance.isValid)
                {
                    if (state.instance is MoveableBuilding mb)
                    {
                        BuildingInfo info = (BuildingInfo)state.info;
                        if (info.m_subBuildings != null && info.m_subBuildings.Length != 0)
                        {
                            //Debug.Log($"Can't align building with sub-buildings");
                            continue;
                        }

                        float terrainHeight = TerrainManager.instance.SampleOriginalRawHeightSmooth(state.instance.position);
                        if (Mathf.Abs(terrainHeight - state.instance.position.y) > 0.01f)
                        {
                            mb.AddFixedHeightFlag(mb.id.Building);
                        }
                        else
                        {
                            mb.RemoveFixedHeightFlag(mb.id.Building);
                        }
                        //Debug.Log($"Building {mb.id.Building}:{mb.angle} ({BuildingManager.instance.m_buildings.m_buffer[mb.id.Building].m_angle}), new:{angle}");
                    }
                    //else if (state.instance is MoveableProp mp)
                    //{
                    //    Debug.Log($"Prop {mp.id.Prop}:{mp.angle} ({PropManager.instance.m_props.m_buffer[mp.id.Prop].m_angle},{PropManager.instance.m_props.m_buffer[mp.id.Prop].Angle}), new:{angle}");
                    //}
                    state.instance.Move(state.instance.position, angle);
                }
            }

            UpdateArea(GetTotalBounds(false));
        }


        public override void Undo()
        {
            foreach (InstanceState state in m_states)
            {
                state.instance.SetState(state);
            }

            UpdateArea(GetTotalBounds(false));
        }

        public override void ReplaceInstances(Dictionary<Instance, Instance> toReplace)
        {
            foreach (InstanceState state in m_states)
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
