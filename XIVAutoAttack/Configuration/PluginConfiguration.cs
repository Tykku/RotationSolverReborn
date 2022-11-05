using Dalamud.Configuration;
using Dalamud.Game.ClientState.Keys;
using System;
using System.Collections.Generic;
using System.Numerics;
using XIVAutoAttack.Data;

namespace XIVAutoAttack.Configuration;

[Serializable]
public class PluginConfiguration : IPluginConfiguration
{
    public int Version { get; set; } = 6;
    public int HostileCount { get; set; } = 3;
    public int PartyCount { get; set; } = 2;
    public int VoiceVolume { get; set; } = 80;
    public SortedSet<string> EnabledActions { get; private set; } = new SortedSet<string>();
    public List<ActionEventInfo> Events { get; private set; } = new List<ActionEventInfo>();
    public Dictionary<string, ActionConfiguration> ActionsConfigurations { get; private set; } = new Dictionary<string, ActionConfiguration>();
    public int TargetToHostileType { get; set; } = 1;
    public int AddDotGCDCount { get; set; } = 2;

    public bool AutoBreak { get; set; } = true;
    public bool OnlyGCD { get; set; } = false;
    public bool NoDefenceAbility { get; set; } = false;
    public bool NeverReplaceIcon { get; set; } = false;
    public bool AlwaysLowBlow { get; set; } = true;
    public bool AutoDefenseForTank { get; set; } = true;
    public bool AutoProvokeForTank { get; set; } = true;
    public bool AutoUseTrueNorth { get; set; } = true;
    public bool ChangeTargetForFate { get; set; } = true;
    public bool MoveTowardsScreen { get; set; } = true;
    public bool AutoSayingOut { get; set; } = false;
    public bool UseDtr { get; set; } = true;
    public bool SayingLocation { get; set; } = true;
    public bool TextLocation { get; set; } = true;
    public bool UseToast { get; set; } = true;
    public bool RaiseAll { get; set; } = false;
    public bool CheckForCasting { get; set; } = true;
    public bool PoslockCasting { get; set; } = false;
    public VirtualKey PoslockModifier { get; set; } = VirtualKey.CONTROL;
    public bool RaisePlayerByCasting { get; set; } = true;
    public bool RaisePlayerBySwift { get; set; } = true;
    public bool AutoShield { get; set; } = true;
    public bool AddEnemyListToHostile { get; set; } = false;
    public bool UseAOEWhenManual { get; set; } = false;
    public bool UseItem { get; set; } = false;
    public bool SayoutLocationWrong { get; set; } = true;
    public bool ChooseAttackMark { get; set; } = true;
    public bool AttackMarkAOE { get; set; } = true;
    public bool FilterStopMark { get; set; } = true;
    public bool UseOverlayWindow { get; set; } = true;
    public bool TeachingMode { get; set; } = true;
    public Vector3 TeachingModeColor { get; set; } = new (0f, 1f, 0.8f);
    public bool KeyBoardNoise { get; set; } = true;
    public bool UseAreaAbilityFriendly { get; set; } = true;
    public float ObjectMinRadius { get; set; } = 0f;
    public float HealthDifference { get; set; } = 0.25f;
    public float HealthAreaAbility { get; set; } = 0.75f;
    public float HealthAreafSpell { get; set; } = 0.65f;
    public float HealthSingleAbility { get; set; } = 0.7f;
    public float HealthSingleSpell { get; set; } = 0.55f;
    public float HealthForDyingTank { get; set; } = 0.15f;
    public float InterruptibleTime { get; set; } = 0.5f;
    public float SpecialDuration { get; set; } = 3;
    public float WeaponInterval { get; set; } = 0.67f;
    public float WeaponFaster { get; set; } = 0.08f;
    public float WeaponDelay { get; set; } = 0;
    public string LocationText { get; set; } = string.Empty;

    public List<TargetingType> TargetingTypes { get; set; } = new List<TargetingType>();
    public int TargetingIndex { get; set; } = 0;
    public void Save()
    {
        Service.Interface.SavePluginConfig(this);
    }
}
