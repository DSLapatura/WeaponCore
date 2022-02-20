using System.Collections.Generic;
using CoreSystems;
using CoreSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Input;
using VRageMath;

namespace WeaponCore.Data.Scripts.CoreSystems.Ui.Targeting
{
    internal partial class TargetUi
    {
        internal void ActivateSelector()
        {
            if (!_session.TrackingAi.IsGrid || _session.UiInput.FirstPersonView && !_session.UiInput.AltPressed) return;
            if (MyAPIGateway.Input.IsNewKeyReleased(MyKeys.Control) && !_session.UiInput.FirstPersonView && !_session.UiInput.CameraBlockView)
            {
                switch (_3RdPersonDraw)
                {
                    case ThirdPersonModes.None:
                        _3RdPersonDraw = ThirdPersonModes.DotNoTarget;
                        break;
                    case ThirdPersonModes.DotNoTarget:
                        _3RdPersonDraw = ThirdPersonModes.DotTarget;
                        break;
                    case ThirdPersonModes.DotTarget:
                        _3RdPersonDraw = ThirdPersonModes.Crosshair;
                        break;
                    case ThirdPersonModes.Crosshair:
                        _3RdPersonDraw = ThirdPersonModes.None;
                        break;
                }
            }

            var enableActivator = _3RdPersonDraw == ThirdPersonModes.Crosshair || _session.UiInput.FirstPersonView && _session.UiInput.AltPressed || _session.UiInput.CameraBlockView;
            if (enableActivator || !_session.UiInput.FirstPersonView && !_session.UiInput.CameraBlockView)
                DrawSelector(enableActivator);
        }

        internal bool ActivateDroneNotice()
        {
            return _session.TrackingAi.IsGrid && _session.TrackingAi.Construct.DroneAlert;
        }

        internal bool ActivateMarks()
        {
            return _session.TrackingAi.IsGrid && _session.ActiveMarks.Count > 0;
        }

        internal bool ActivateLeads()
        {
            return _session.LeadGroupActive;
        }

        internal void ResetCache()
        {
            _cachedPointerPos = false;
            _cachedTargetPos = false;
        }

        private void InitPointerOffset(double adjust)
        {
            var position = new Vector3D(_pointerPosition.X, _pointerPosition.Y, 0);
            var scale = 0.075 * _session.ScaleFov;

            position.X *= scale * _session.AspectRatio;
            position.Y *= scale;

            PointerAdjScale = adjust * scale;

            PointerOffset = new Vector3D(position.X, position.Y, -0.1);
            _cachedPointerPos = true;
        }

        private MyEntity _firstStageEnt;
        internal void SelectTarget(bool manualSelect = true, bool firstStage = false)
        {
            var s = _session;
            var ai = s.TrackingAi;

            if (s.Tick - MasterUpdateTick > 300 || MasterUpdateTick < 300 && _masterTargets.Count == 0)
                BuildMasterCollections(ai);

            if (!_cachedPointerPos) InitPointerOffset(0.05);
            if (!_cachedTargetPos) InitTargetOffset();
            var cockPit = s.ActiveCockPit;
            Vector3D end;

            if (s.UiInput.CameraBlockView)
            {
                var offetPosition = Vector3D.Transform(PointerOffset, s.CameraMatrix);
                AimPosition = offetPosition;
                AimDirection = Vector3D.Normalize(AimPosition - s.CameraPos);
                end = offetPosition + (AimDirection * ai.MaxTargetingRange);
            }
            else if (!s.UiInput.FirstPersonView)
            {
                var offetPosition = Vector3D.Transform(PointerOffset, s.CameraMatrix);
                AimPosition = offetPosition;
                AimDirection = Vector3D.Normalize(AimPosition - s.CameraPos);
                end = offetPosition + (AimDirection * ai.MaxTargetingRange);
            }
            else
            {
                if (!s.UiInput.AltPressed && ai.IsGrid)
                {
                    AimDirection = cockPit.PositionComp.WorldMatrixRef.Forward;
                    AimPosition = cockPit.PositionComp.WorldAABB.Center;
                    end = AimPosition + (AimDirection * s.TrackingAi.MaxTargetingRange);
                }
                else
                {
                    var offetPosition = Vector3D.Transform(PointerOffset, s.CameraMatrix);
                    AimPosition = offetPosition;
                    AimDirection = Vector3D.Normalize(AimPosition - s.CameraPos);
                    end = offetPosition + (AimDirection * ai.MaxTargetingRange);
                }
            }

            var foundTarget = false;
            var rayOnlyHitSelf = false;
            var rayHitSelf = false;
            var manualTarget = ai.Session.PlayerDummyTargets[ai.Session.PlayerId].ManualTarget;
            var paintTarget = ai.Session.PlayerDummyTargets[ai.Session.PlayerId].PaintedTarget;
            var mark = s.UiInput.MouseButtonRightReleased;
            MyEntity closestEnt = null;
            _session.Physics.CastRay(AimPosition, end, _hitInfo);

            var t1 = ai.Construct.Focus.PrevTarget;

            for (int i = 0; i < _hitInfo.Count; i++)
            {

                var hit = _hitInfo[i];
                closestEnt = hit.HitEntity.GetTopMostParent() as MyEntity;

                var hitGrid = closestEnt as MyCubeGrid;

                if (hitGrid != null && ai.IsGrid && hitGrid.IsSameConstructAs(ai.GridEntity))
                {
                    rayHitSelf = true;
                    rayOnlyHitSelf = true;
                    continue;
                }

                if (rayOnlyHitSelf) rayOnlyHitSelf = false;

                if (manualSelect)
                {
                    if (hitGrid == null || !_masterTargets.ContainsKey(hitGrid))
                        continue;

                    if (firstStage)
                    {
                        LastConfrimedEntity = null;
                        if (t1 == closestEnt.EntityId)
                            LastConfrimedEntity = closestEnt;

                        _firstStageEnt = hitGrid;

                    }
                    else
                    {
                        if (hitGrid == _firstStageEnt) {

                            if (mark && LastConfrimedEntity == closestEnt && (t1 == closestEnt.EntityId)) {
                                paintTarget.Update(hit.Position, s.Tick, closestEnt);
                            }

                            s.SetTarget(hitGrid, ai, _masterTargets);
                        }

                        _firstStageEnt = null;
                    }

                    return;
                }

                foundTarget = true;
                manualTarget.Update(hit.Position, s.Tick, closestEnt);
                break;
            }
            if (rayHitSelf)
            {
                ReticleOnSelfTick = s.Tick;
                ReticleAgeOnSelf++;
                if (rayOnlyHitSelf && !mark) manualTarget.Update(end, s.Tick);
            }
            else ReticleAgeOnSelf = 0;

            Vector3D hitPos;
            bool foundOther = false;
            if (!foundTarget && RayCheckTargets(AimPosition, AimDirection, out closestEnt, out hitPos, out foundOther, !manualSelect))
            {
                foundTarget = true;
                if (manualSelect)
                {
                    if (firstStage)
                        _firstStageEnt = closestEnt;
                    else
                    {
                        if (closestEnt == _firstStageEnt)
                            s.SetTarget(closestEnt, ai, _masterTargets);

                        _firstStageEnt = null;
                    }

                    return;
                }
                manualTarget.Update(hitPos, s.Tick, closestEnt);
            }

            if (!manualSelect)
            {
                var activeColor = closestEnt != null && !_masterTargets.ContainsKey(closestEnt) || foundOther ? Color.DeepSkyBlue : Color.Red;

                var voxel = closestEnt as MyVoxelBase;
                _reticleColor = closestEnt != null && voxel == null ? activeColor : Color.White;
                if (voxel == null)
                {
                    LastSelectableTick = _session.Tick;
                    LastSelectedEntity = closestEnt;
                }

                if (!foundTarget)
                {
                    if (mark)
                        paintTarget.Update(end, s.Tick);
                    else
                        manualTarget.Update(end, s.Tick);
                }
            }
        }

        internal bool GetSelectableEntity(out Vector3D position)
        {

            if (_session.Tick - LastSelectableTick < 60)
            {
                var skip = false;
                var ai = _session.TrackingAi;

                MyEntity focusEnt;
                if (ai != null && LastSelectedEntity != null && ai.Construct.Focus.GetPriorityTarget(ai, out focusEnt))
                {
                    var focusGrid = focusEnt as MyCubeGrid;
                    var lastEntityGrid = LastSelectedEntity as MyCubeGrid;

                    if (LastSelectedEntity.MarkedForClose || focusEnt == LastSelectedEntity || focusGrid != null && lastEntityGrid != null && focusGrid.IsSameConstructAs(lastEntityGrid))
                        skip = true;
                }

                if (LastSelectedEntity != null && !skip && _session.CameraFrustrum.Contains(LastSelectedEntity.PositionComp.WorldVolume) != ContainmentType.Disjoint)
                {
                    position = LastSelectedEntity.PositionComp.WorldAABB.Center;
                    return true;
                }
            }

            position = Vector3D.Zero;
            return false;
        }

        internal void SelectNext()
        {
            var s = _session;
            var ai = s.TrackingAi;

            if (!_cachedPointerPos) InitPointerOffset(0.05);
            if (!_cachedTargetPos) InitTargetOffset();
            var updateTick = s.Tick - _cacheIdleTicks > 300 || _endIdx == -1 || _sortedMasterList.Count - 1 < _endIdx;

            if (updateTick && !UpdateCache(s.Tick) || s.UiInput.ShiftPressed || s.UiInput.ControlKeyPressed || s.UiInput.AltPressed || s.UiInput.CtrlPressed) return;

            var canMoveForward = _currentIdx + 1 <= _endIdx;
            var canMoveBackward = _currentIdx - 1 >= 0;
            if (s.UiInput.WheelForward)
                if (canMoveForward)
                    _currentIdx += 1;
                else _currentIdx = 0;
            else if (s.UiInput.WheelBackward)
                if (canMoveBackward)
                    _currentIdx -= 1;
                else _currentIdx = _endIdx;

            var ent = _sortedMasterList[_currentIdx];
            if (ent == null || ent.MarkedForClose || ai.NoTargetLos.ContainsKey(ent))
            {
                _endIdx = -1;
                return;
            }

            s.SetTarget(ent, ai, _masterTargets);
        }

        private bool UpdateCache(uint tick)
        {
            _cacheIdleTicks = tick;
            var ai = _session.TrackingAi;
            var focus = ai.Construct.Data.Repo.FocusData;
            _currentIdx = 0;
            BuildMasterCollections(ai);

            for (int i = 0; i < _sortedMasterList.Count; i++)
                if (focus.Target == _sortedMasterList[i].EntityId) _currentIdx = i;
            _endIdx = _sortedMasterList.Count - 1;
            return _endIdx >= 0;
        }

        internal void BuildMasterCollections(Ai ai)
        {
            _masterTargets.Clear();
            for (int i = 0; i < ai.Construct.RefreshedAis.Count; i++)
            {

                var subTargets = ai.Construct.RefreshedAis[i].SortedTargets;
                for (int j = 0; j < subTargets.Count; j++)
                {
                    var tInfo = subTargets[j];
                    if (tInfo.Target.MarkedForClose || tInfo.Target is IMyCharacter) continue;
                    GridMap gridMap;
                    var controlType = tInfo.Drone ? TargetControl.Drone : tInfo.IsGrid && _session.GridToInfoMap.TryGetValue((MyCubeGrid)tInfo.Target, out gridMap) && gridMap.PlayerControllers.Count > 0 ? TargetControl.Player : tInfo.IsGrid && !_session.GridHasPower((MyCubeGrid)tInfo.Target) ? TargetControl.Trash : TargetControl.Other;
                    _masterTargets[tInfo.Target] = new MyTuple<float, TargetControl>(tInfo.OffenseRating, controlType);
                    _toPruneMasterDict[tInfo.Target] = tInfo;
                }
            }
            _sortedMasterList.Clear();
            _toSortMasterList.AddRange(_toPruneMasterDict.Values);
            _toPruneMasterDict.Clear();

            _toSortMasterList.Sort(_session.TargetCompare);

            for (int i = 0; i < _toSortMasterList.Count; i++)
                _sortedMasterList.Add(_toSortMasterList[i].Target);

            _toSortMasterList.Clear();
            MasterUpdateTick = ai.Session.Tick;
        }

        private bool RayCheckTargets(Vector3D origin, Vector3D dir, out MyEntity closestEnt, out Vector3D hitPos, out bool foundOther, bool checkOthers = false)
        {
            var ai = _session.TrackingAi;
            var closestDist1 = double.MaxValue;
            var closestDist2 = double.MaxValue;

            closestEnt = null;
            MyEntity backUpEnt = null;
            foreach (var info in _masterTargets.Keys)
            {
                var hit = info as MyCubeGrid;
                if (hit == null) continue;
                var ray = new RayD(origin, dir);

                var entVolume = info.PositionComp.WorldVolume;
                var entCenter = entVolume.Center;
                var dist1 = ray.Intersects(entVolume);
                if (dist1 < closestDist1)
                {
                    closestDist1 = dist1.Value;
                    closestEnt = hit;
                }

                double dist;
                Vector3D.DistanceSquared(ref entCenter, ref origin, out dist);

                if (dist > 640000)
                {
                    var inflated = info.PositionComp.WorldVolume;
                    inflated.Radius *= 3;
                    var dist2 = ray.Intersects(inflated);
                    if (dist2 < closestDist2)
                    {
                        closestDist2 = dist2.Value;
                        backUpEnt = hit;
                    }
                }

            }

            foundOther = false;
            if (checkOthers)
            {
                for (int i = 0; i < ai.Obstructions.Count; i++)
                {
                    var otherEnt = ai.Obstructions[i];
                    if (otherEnt is MyCubeGrid)
                    {
                        var ray = new RayD(origin, dir);
                        var entVolume = otherEnt.PositionComp.WorldVolume;
                        var entCenter = entVolume.Center;

                        var dist1 = ray.Intersects(entVolume);
                        if (dist1 < closestDist1)
                        {
                            closestDist1 = dist1.Value;
                            closestEnt = otherEnt;
                            foundOther = true;
                        }

                        double dist;
                        Vector3D.DistanceSquared(ref entCenter, ref origin, out dist);

                        if (dist > 640000)
                        {
                            var inflated = entVolume;
                            inflated.Radius *= 3;
                            var dist2 = ray.Intersects(inflated);
                            if (dist2 < closestDist2)
                            {
                                closestDist1 = dist2.Value;
                                backUpEnt = otherEnt;
                                foundOther = true;
                            }
                        }
                    }
                }
            }

            if (closestEnt == null)
                closestEnt = backUpEnt;

            if (closestDist1 < double.MaxValue)
                hitPos = origin + (dir * closestDist1);
            else if (closestDist2 < double.MaxValue)
                hitPos = origin + (dir * closestDist2);
            else hitPos = Vector3D.Zero;

            return closestEnt != null;
        }
    }
}
