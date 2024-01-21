﻿using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using RotationSolver.Basic.Configuration;
using System.Linq;
using System.Text.RegularExpressions;

namespace RotationSolver.Basic.Actions;

public struct ActionTargetInfo(IBaseAction _action)
{
    public readonly bool TargetArea => _action.Action.TargetArea;

    public readonly float Range => ActionManager.GetActionRange(_action.Info.ID);

    public readonly float EffectRange => (ActionID)_action.Info.ID == ActionID.LiturgyOfTheBellPvE ? 20 : _action.Action.EffectRange;

    public readonly bool IsSingleTarget => _action.Action.CastType == 1;

    private static bool NoAOE
    {
        get
        {
            if (!Service.Config.GetValue(PluginConfigBool.UseAOEAction)) return true;

            if (DataCenter.IsManual)
            {
                if (!Service.Config.GetValue(PluginConfigBool.UseAOEWhenManual)) return true;
            }

            return Service.Config.GetValue(PluginConfigBool.ChooseAttackMark)
                && !Service.Config.GetValue(PluginConfigBool.CanAttackMarkAOE)
                && MarkingHelper.HaveAttackChara(DataCenter.HostileTargets);
        }
    }

    #region Target Finder.
    //The delay of finding the targets.
    private readonly ObjectListDelay<GameObject> _canTargets = new (() => (1, 3));
    public readonly IEnumerable<GameObject> CanTargets
    {
        get
        {
            _canTargets.Delay(TargetFilter.GetObjectInRadius(DataCenter.AllTargets, Range)
                .Where(GeneralCheck).Where(CanUseTo).Where(InViewTarget).Where(_action.Setting.CanTarget));
            return _canTargets;
        }
    }

    public readonly IEnumerable<BattleChara> CanAffects
    {
        get
        {
            if (EffectRange == 0) return [];
            return TargetFilter.GetObjectInRadius(_action.Setting.IsFriendly
                ? DataCenter.PartyMembers
                : DataCenter.HostileTargets,
                Range + EffectRange).Where(GeneralCheck);
        }
    }

    private static bool InViewTarget(GameObject gameObject)
    {
        if (Service.Config.GetValue(PluginConfigBool.OnlyAttackInView))
        {
            if (!Svc.GameGui.WorldToScreen(gameObject.Position, out _)) return false;
        }
        if (Service.Config.GetValue(PluginConfigBool.OnlyAttackInVisionCone))
        {
            Vector3 dir = gameObject.Position - Player.Object.Position;
            Vector2 dirVec = new(dir.Z, dir.X);
            double angle = Player.Object.GetFaceVector().AngleTo(dirVec);
            if (angle > Math.PI * Service.Config.GetValue(PluginConfigFloat.AngleOfVisionCone) / 360)
            {
                return false;
            }
        }
        return true;
    }
    private readonly unsafe bool CanUseTo(GameObject tar)
    {
        if (tar == null || !Player.Available) return false;
        var tarAddress = tar.Struct();

        if ((ActionID)_action.Info.ID != ActionID.AethericMimicryPvE
            && !ActionManager.CanUseActionOnTarget(_action.Info.AdjustedID, tarAddress)) return false;

        return tar.CanSee();
    }

    private readonly bool GeneralCheck(GameObject gameObject)
    {
        return CheckStatus(gameObject) 
            && CheckTimeToKill(gameObject)
            && CheckResistance(gameObject);
    }

    private readonly bool CheckStatus(GameObject gameObject)
    {
        if (_action.Setting.TargetStatus == null || !_action.Config.ShouldCheckStatus) return true;

        return gameObject.WillStatusEndGCD(_action.Config.StatusGcdCount, 0,
            _action.Setting.TargetStatusFromSelf, _action.Setting.TargetStatus);
    }

    private readonly bool CheckResistance(GameObject gameObject)
    {
        if (_action.Info.AttackType == AttackType.Magic) //TODO: special attack type resistance.
        {
            if (gameObject.HasStatus(false, StatusID.MagicResistance))
            {
                return false;
            }
        }
        if (Range >= 20) // Range
        {
            if(gameObject.HasStatus(false, StatusID.RangedResistance, StatusID.EnergyField))
            {
                return false;
            }
        }

        return true;
    }

    private readonly bool CheckTimeToKill(GameObject gameObject)
    {
        if (gameObject is not BattleChara b) return false;
        var time = b.GetTimeToKill();
        return float.IsNaN(time) || time >= _action.Config.TimeToKill;
    }

    #endregion

    /// <summary>
    /// Take a little long time..
    /// </summary>
    /// <returns></returns>
    internal readonly TargetResult? FindTarget(bool skipAoeCheck)
    {
        var range = Range;
        var player = Player.Object;

        if (range == 0 && EffectRange == 0)
        {
            return new(player, [], player.Position);
        }

        var canTargets = CanTargets;
        var canAffects = CanAffects;

        if (_action.Action.TargetArea)
        {
            return FindTargetArea(canTargets, canAffects, range, player);
        }

        var targets = GetMostCanTargetObjects(canTargets, canAffects,
            skipAoeCheck ? 0 : _action.Config.AoeCount);
        var target = FindTargetByType(targets, _action.Setting.TargetType);
        if (target == null) return null;

        return new(target, [.. GetAffects(target, canAffects)], target.Position);
    }

    private readonly TargetResult? FindTargetArea(IEnumerable<GameObject> canTargets, IEnumerable<GameObject> canAffects,
        float range, PlayerCharacter player)
    {
        if (_action.Setting.TargetType is TargetType.Move)
        {
            return FindTargetAreaMove(range);
        }
        else if (_action.Setting.IsFriendly)
        {
            if (!Service.Config.GetValue(PluginConfigBool.UseGroundBeneficialAbility)) return null;
            if (!Service.Config.GetValue(PluginConfigBool.UseGroundBeneficialAbilityWhenMoving) && DataCenter.IsMoving) return null;

            return FindTargetAreaFriend(range, canAffects, player);
        }
        else
        {
            return FindTargetAreaHostile(canTargets, canAffects, _action.Config.AoeCount);
        }
    }


    private readonly TargetResult? FindTargetAreaHostile(IEnumerable<GameObject> canTargets, IEnumerable<GameObject> canAffects, int aoeCount)
    {
        var target = GetMostCanTargetObjects(canTargets, canAffects, aoeCount)
            .OrderByDescending(ObjectHelper.GetHealthRatio).FirstOrDefault();
        if (target == null) return null;
        return new(target, [..GetAffects(target, canAffects)], target.Position);
    }

    private TargetResult? FindTargetAreaMove(float range)
    {
        if (Service.Config.GetValue(PluginConfigBool.MoveAreaActionFarthest))
        {
            Vector3 pPosition = Player.Object.Position;
            if (Service.Config.GetValue(PluginConfigBool.MoveTowardsScreenCenter)) unsafe
                {
                    var camera = CameraManager.Instance()->CurrentCamera;
                    var tar = camera->LookAtVector - camera->Object.Position;
                    tar.Y = 0;
                    var length = ((Vector3)tar).Length();
                    if (length == 0) return null;
                    tar = tar / length * range;
                    return new(Player.Object, [], new Vector3(pPosition.X + tar.X,
                        pPosition.Y, pPosition.Z + tar.Z));
                }
            else
            {
                float rotation = Player.Object.Rotation;
                return new(Player.Object, [], new Vector3(pPosition.X + (float)Math.Sin(rotation) * range,
                    pPosition.Y, pPosition.Z + (float)Math.Cos(rotation) * range));
            }
        }
        else
        {
            var availableCharas = DataCenter.AllTargets.Where(b => b.ObjectId != Player.Object.ObjectId);
            var target = FindTargetByType(TargetFilter.GetObjectInRadius(availableCharas, range), TargetType.Move);
            if (target == null) return null;
            return new(target, [], target.Position);
        }
    }


    private readonly TargetResult? FindTargetAreaFriend(float range, IEnumerable<GameObject> canAffects, PlayerCharacter player)
    {
        var strategy = Service.Config.GetValue(PluginConfigInt.BeneficialAreaStrategy);
        switch (strategy)
        {
            case 0: // Find from list
            case 1: // Only the list
                OtherConfiguration.BeneficialPositions.TryGetValue(Svc.ClientState.TerritoryType, out var pts);

                pts ??= [];

                if (pts.Length == 0)
                {
                    if (DataCenter.TerritoryContentType == TerritoryContentType.Trials ||
                        DataCenter.TerritoryContentType == TerritoryContentType.Raids
                        && DataCenter.AllianceMembers.Count(p => p is PlayerCharacter) == 8)
                    {
                        pts = pts.Union(new Vector3[] { Vector3.Zero, new(100, 0, 100) }).ToArray();
                    }
                }

                if (pts.Length > 0)
                {
                    var closest = pts.MinBy(p => Vector3.Distance(player.Position, p));
                    var rotation = new Random().NextDouble() * Math.Tau;
                    var radius = new Random().NextDouble() * 1;
                    closest.X += (float)(Math.Sin(rotation) * radius);
                    closest.Z += (float)(Math.Cos(rotation) * radius);
                    if (Vector3.Distance(player.Position, closest) < player.HitboxRadius + EffectRange)
                    {
                        return new(player, [.. GetAffects(closest, canAffects)], closest);
                    }
                }

                if (strategy == 1) return null;
                break;

            case 2: // Target
                if (Svc.Targets.Target != null && Svc.Targets.Target.DistanceToPlayer() < range)
                {
                    var target = Svc.Targets.Target;
                    return new(target, [.. GetAffects(target.Position, canAffects)], target.Position);
                }
                break;
        }

        if (Svc.Targets.Target is BattleChara b && b.DistanceToPlayer() < range &&
            b.IsBossFromIcon() && b.HasPositional() && b.HitboxRadius <= 8)
        {
            return new(b, [.. GetAffects(b.Position, canAffects)], b.Position);
        }
        else
        {
            var effectRange = EffectRange;
            var attackT = FindTargetByType(DataCenter.AllianceMembers.GetObjectInRadius(range + effectRange), TargetType.BeAttacked);

            if (attackT == null)
            {
                return new(player, [.. GetAffects(player.Position, canAffects)], player.Position);
            }
            else
            {
                var disToTankRound = Vector3.Distance(player.Position, attackT.Position) + attackT.HitboxRadius;

                if (disToTankRound < effectRange
                    || disToTankRound > 2 * effectRange - player.HitboxRadius)
                {
                    return new(player, [.. GetAffects(player.Position, canAffects)], player.Position);
                }
                else
                {
                    Vector3 directionToTank = attackT.Position - player.Position;
                    var MoveDirection = directionToTank / directionToTank.Length() * Math.Max(0, disToTankRound - effectRange);
                    return new(player, [.. GetAffects(player.Position, canAffects)], player.Position + MoveDirection);
                }
            }
        }
    }

    private readonly IEnumerable<GameObject> GetAffects(Vector3 point, IEnumerable<GameObject> canAffects)
    {
        foreach (var t in canAffects)
        {
            if (Vector3.Distance(point, t.Position) - t.HitboxRadius <= EffectRange)
            {
                yield return t;
            }
        }
    }

    private readonly IEnumerable<GameObject> GetAffects(GameObject tar, IEnumerable<GameObject> canAffects)
    {
        foreach (var t in canAffects)
        {
            if (CanGetTarget(tar, t))
            {
                yield return t;
            }
        }
    }

    #region Get Most Target
    private readonly IEnumerable<GameObject> GetMostCanTargetObjects(IEnumerable<GameObject> canTargets, IEnumerable<GameObject> canAffects, int aoeCount)
    {
        if (IsSingleTarget || EffectRange <= 0) return canTargets;
        if (!_action.Setting.IsFriendly && NoAOE) return [];

        List<GameObject> objectMax = new(canTargets.Count());

        foreach (var t in canTargets)
        {
            int count = CanGetTargetCount(t, canAffects);

            if (count == aoeCount)
            {
                objectMax.Add(t);
            }
            else if (count > aoeCount)
            {
                aoeCount = count;
                objectMax.Clear();
                objectMax.Add(t);
            }
        }
        return objectMax;
    }

    private readonly int CanGetTargetCount(GameObject target, IEnumerable<GameObject> canAffects)
    {
        int count = 0;
        foreach (var t in canAffects)
        {
            if (target != t && !CanGetTarget(target, t)) continue;

            if (Service.Config.GetValue(PluginConfigBool.NoNewHostiles)
                && t.TargetObject == null)
            {
                return 0;
            }
            count++;
        }

        return count;
    }

    const double _alpha = Math.PI / 3;
    private readonly bool CanGetTarget(GameObject target, GameObject subTarget)
    {
        if (target == null) return false;

        var pPos = Player.Object.Position;
        Vector3 dir = target.Position - pPos;
        Vector3 tdir = subTarget.Position - pPos;

        switch (_action.Action.CastType)
        {
            case 2: // Circle
                return Vector3.Distance(target.Position, subTarget.Position) - subTarget.HitboxRadius <= EffectRange;

            case 3: // Sector
                if (subTarget.DistanceToPlayer() > EffectRange) return false;
                tdir += dir / dir.Length() * target.HitboxRadius / (float)Math.Sin(_alpha);
                return Vector3.Dot(dir, tdir) / (dir.Length() * tdir.Length()) >= Math.Cos(_alpha);

            case 4: //Line
                if (subTarget.DistanceToPlayer() > EffectRange) return false;

                return Vector3.Cross(dir, tdir).Length() / dir.Length() <= 2 + target.HitboxRadius
                    && Vector3.Dot(dir, tdir) >= 0;

            case 10: //Donut
                var dis = Vector3.Distance(target.Position, subTarget.Position) - subTarget.HitboxRadius;
                return dis <= EffectRange && dis >= 8;
        }

        Svc.Log.Debug(_action.Action.Name.RawString + "'s CastType is not valid! The value is " + _action.Action.CastType.ToString());
        return false;
    }
    #endregion

    #region TargetFind
    private readonly GameObject? FindTargetByType(IEnumerable<GameObject> gameObjects, TargetType type)
    {
        switch (type) // Filter the objects.
        {
            case TargetType.Death:
                gameObjects = gameObjects.Where(ObjectHelper.IsDeathToRaise);
                break;

            case TargetType.Move:
                break;

            default:
                gameObjects = gameObjects.Where(ObjectHelper.IsAlive);
                break;
        }

        return type switch //Find the object.
        {
            TargetType.Provoke => FindProvokeTarget(),
            TargetType.Dispel => FindWeakenTarget(),
            TargetType.Death => FindDeathPeople(),
            TargetType.Move => FindTargetForMoving(),
            TargetType.Heal => FindHealTarget(_action.Config.AutoHealRatio),
            TargetType.BeAttacked => FindBeAttackedTarget(),
            TargetType.Interrupt => FindInterruptTarget(),
            TargetType.Tank => FindTankTarget(),
            _ => FindHostile(),
        };

        GameObject? FindProvokeTarget()
        {
            var loc = Player.Object.Position;

            return gameObjects.FirstOrDefault(target =>
            {
                //Removed the listed names.
                IEnumerable<string> names = Array.Empty<string>();
                if (OtherConfiguration.NoProvokeNames.TryGetValue(Svc.ClientState.TerritoryType, out var ns1))
                    names = names.Union(ns1);

                if (names.Any(n => !string.IsNullOrEmpty(n) && new Regex(n).Match(target.Name.ToString()).Success)) return false;

                //Target can move or two big and has a target
                if ((target.GetObjectNPC()?.Unknown12 == 0 || target.HitboxRadius >= 5)
                && (target.TargetObject?.IsValid() ?? false))
                {
                    //the target is not a tank role
                    if (Svc.Objects.SearchById(target.TargetObjectId) is BattleChara battle
                        && !battle.IsJobCategory(JobRole.Tank)
                        && (Vector3.Distance(target.Position, loc) > 5))
                    {
                        return true;
                    }
                }
                return false;
            });
        }

        GameObject? FindDeathPeople()
        {
            var deathParty = gameObjects.Where(ObjectHelper.IsParty);

            if (deathParty.Any())
            {
                var deathT = deathParty.GetJobCategory(JobRole.Tank);
                int TCount = DataCenter.PartyTanks.Count();

                if (TCount > 0 && deathT.Count() == TCount)
                {
                    return deathT.FirstOrDefault();
                }

                var deathH = deathParty.GetJobCategory(JobRole.Healer);

                if (deathH.Any()) return deathH.FirstOrDefault();

                if (deathT.Any()) return deathT.FirstOrDefault();

                return deathParty.FirstOrDefault();
            }

            if (gameObjects.Any())
            {
                var deathAllH = gameObjects.GetJobCategory(JobRole.Healer);
                if (deathAllH.Any()) return deathAllH.FirstOrDefault();

                var deathAllT = gameObjects.GetJobCategory(JobRole.Tank);
                if (deathAllT.Any()) return deathAllT.FirstOrDefault();

                return gameObjects.FirstOrDefault();
            }

            return null;
        }

        GameObject? FindTargetForMoving()
        {
            const float DISTANCE_TO_MOVE = 3;

            if (Service.Config.GetValue(PluginConfigBool.MoveTowardsScreenCenter))
            {
                return FindMoveTargetScreenCenter();
            }
            else
            {
                return FindMoveTargetFaceDirection();
            }

            GameObject? FindMoveTargetScreenCenter()
            {
                var pPosition = Player.Object.Position;
                if (!Svc.GameGui.WorldToScreen(pPosition, out var playerScrPos)) return null;

                var tars = gameObjects.Where(t =>
                {
                    if (t.DistanceToPlayer() < DISTANCE_TO_MOVE) return false;

                    if (!Svc.GameGui.WorldToScreen(t.Position, out var scrPos)) return false;

                    var dir = scrPos - playerScrPos;

                    if (dir.Y > 0) return false;

                    return Math.Abs(dir.X / dir.Y) <= Math.Tan(Math.PI * Service.Config.GetValue(PluginConfigFloat.MoveTargetAngle) / 360);
                }).OrderByDescending(ObjectHelper.DistanceToPlayer);

                return tars.FirstOrDefault();
            }

            GameObject? FindMoveTargetFaceDirection()
            {
                Vector3 pPosition = Player.Object.Position;
                Vector2 faceVec = Player.Object.GetFaceVector();

                var tars = gameObjects.Where(t =>
                {
                    if (t.DistanceToPlayer() < DISTANCE_TO_MOVE) return false;

                    Vector3 dir = t.Position - pPosition;
                    Vector2 dirVec = new(dir.Z, dir.X);
                    double angle = faceVec.AngleTo(dirVec);
                    return angle <= Math.PI * Service.Config.GetValue(PluginConfigFloat.MoveTargetAngle) / 360;
                }).OrderByDescending(ObjectHelper.DistanceToPlayer);

                return tars.FirstOrDefault();
            }
        }

        GameObject? FindHealTarget(float healRatio)
        {
            if (!gameObjects.Any()) return null;

            if (IBaseAction.AutoHealCheck)
            {
                gameObjects = gameObjects.Where(o => o.GetHealthRatio() < healRatio);
            }

            var partyMembers = gameObjects.Where(ObjectHelper.IsParty);

            return GeneralHealTarget(partyMembers)
                ?? GeneralHealTarget(gameObjects)
                ?? partyMembers.FirstOrDefault(t => t.HasStatus(false, StatusHelper.TankStanceStatus))
                ?? partyMembers.FirstOrDefault()
                ?? gameObjects.FirstOrDefault(t => t.HasStatus(false, StatusHelper.TankStanceStatus))
                ?? gameObjects.FirstOrDefault();

            static GameObject? GeneralHealTarget(IEnumerable<GameObject> objs)
            {
                objs = objs.Where(StatusHelper.NeedHealing).OrderBy(ObjectHelper.GetHealthRatio);

                var healerTars = objs.GetJobCategory(JobRole.Healer);
                var tankTars = objs.GetJobCategory(JobRole.Tank);

                var healerTar = healerTars.FirstOrDefault();
                if (healerTar != null && healerTar.GetHealthRatio() < Service.Config.GetValue(PluginConfigFloat.HealthHealerRatio))
                    return healerTar;

                var tankTar = tankTars.FirstOrDefault();
                if (tankTar != null && tankTar.GetHealthRatio() < Service.Config.GetValue(PluginConfigFloat.HealthTankRatio))
                    return tankTar;

                var tar = objs.FirstOrDefault();
                if (tar?.GetHealthRatio() < 1) return tar;

                return null;
            }
        }

        GameObject? FindInterruptTarget()
        {
            gameObjects = gameObjects.Where(ObjectHelper.CanInterrupt);
            return FindHostile();
        }

        GameObject? FindHostile()
        {
            if (gameObjects == null || !gameObjects.Any()) return null;

            if (Service.Config.GetValue(PluginConfigBool.FilterStopMark))
            {
                var cs = MarkingHelper.FilterStopCharaes(gameObjects);
                if (cs?.Any() ?? false) gameObjects = cs;
            }

            if (DataCenter.TreasureCharas.Length > 0)
            {
                var b = gameObjects.FirstOrDefault(b => b.ObjectId == DataCenter.TreasureCharas[0]);
                if (b != null) return b;
                gameObjects = gameObjects.Where(b => !DataCenter.TreasureCharas.Contains(b.ObjectId));
            }

            var highPriority = gameObjects.Where(ObjectHelper.IsTopPriorityHostile);
            if (highPriority.Any())
            {
                gameObjects = highPriority;
            }

            return FindHostileRaw();
        }

        GameObject? FindHostileRaw()
        {
            gameObjects = type switch
            {
                TargetType.Small => gameObjects.OrderBy(p => p.HitboxRadius),
                TargetType.HighHP => gameObjects.OrderByDescending(p => p is BattleChara b ? b.CurrentHp : 0),
                TargetType.LowHP => gameObjects.OrderBy(p => p is BattleChara b ? b.CurrentHp : 0),
                TargetType.HighMaxHP => gameObjects.OrderByDescending(p => p is BattleChara b ? b.MaxHp : 0),
                TargetType.LowMaxHP => gameObjects.OrderBy(p => p is BattleChara b ? b.MaxHp : 0),
                _ => gameObjects.OrderByDescending(p => p.HitboxRadius),
            };
            return gameObjects.FirstOrDefault();
        }

        GameObject? FindBeAttackedTarget()
        {
            if (!gameObjects.Any()) return null;
            var attachedT = gameObjects.Where(tank => tank.TargetObject?.TargetObject == tank);

            if (!attachedT.Any())
            {
                attachedT = gameObjects.Where(tank => tank.HasStatus(false, StatusHelper.TankStanceStatus));
            }

            if (!attachedT.Any())
            {
                attachedT = gameObjects.GetJobCategory(JobRole.Tank);
            }

            if (!attachedT.Any())
            {
                attachedT = gameObjects;
            }

            return attachedT.OrderBy(ObjectHelper.GetHealthRatio).FirstOrDefault();
        }

        GameObject? FindWeakenTarget()
        {
            var weakenPeople = gameObjects.Where(o => o is BattleChara b && b.StatusList.Any(StatusHelper.CanDispel));
            var dyingPeople = weakenPeople.Where(o => o is BattleChara b && b.StatusList.Any(StatusHelper.IsDangerous));

            if (dyingPeople.Any())
            {
                return dyingPeople.OrderBy(ObjectHelper.DistanceToPlayer).First();
            }
            else if (weakenPeople.Any())
            {
                return weakenPeople.OrderBy(ObjectHelper.DistanceToPlayer).First();
            }
            return null;
        }

        GameObject? FindTankTarget()
        {
            return TargetFilter.GetJobCategory(gameObjects, JobRole.Tank)?.FirstOrDefault()
                ?? gameObjects.FirstOrDefault();
        }
    }
    #endregion
}

public enum TargetType : byte
{
    Tank,

    Interrupt,

    Provoke,

    Death,

    Dispel,

    Move,

    BeAttacked,

    Heal,

    /// <summary>
    /// Find the target whose hit box is biggest.
    /// </summary>
    Big,

    /// <summary>
    /// Find the target whose hit box is smallest.
    /// </summary>
    Small,

    /// <summary>
    /// Find the target whose hp is highest.
    /// </summary>
    HighHP,

    /// <summary>
    /// Find the target whose hp is lowest.
    /// </summary>
    LowHP,

    /// <summary>
    /// Find the target whose max hp is highest.
    /// </summary>
    HighMaxHP,

    /// <summary>
    /// Find the target whose max hp is lowest.
    /// </summary>
    LowMaxHP,
}

public readonly record struct TargetResult(GameObject Target, GameObject[] AffectedTargets, Vector3 Position);