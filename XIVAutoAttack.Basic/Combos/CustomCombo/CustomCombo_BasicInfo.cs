﻿using ImGuiScene;
using Lumina.Data.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using XIVAutoAttack.Actions.BaseAction;
using XIVAutoAttack.Configuration;
using XIVAutoAttack.Data;
using XIVAutoAttack.Helpers;

namespace XIVAutoAttack.Combos.CustomCombo
{
    public abstract partial class CustomCombo<TCmd> : CustomComboActions, ICustomCombo, IDisposable where TCmd : Enum
    {
        internal static readonly uint[] RangePhysicial = new uint[] { 23, 31, 38, 5 };
        public abstract uint[] JobIDs { get; }
        public static bool IsTargetDying
        {
            get
            {
                if (Target == null) return false;
                return Target.IsDying();
            }
        }

        public static bool IsTargetBoss
        {
            get
            {
                if (Target == null) return false;
                return Target.IsBoss();
            }
        }

        public string Description => string.Join('\n', DescriptionDict.Select(pair => pair.Key.ToString() + " → " + pair.Value));

        public virtual SortedList<DescType, string> DescriptionDict { get; } = new SortedList<DescType, string>();


        public static bool HaveSwift => Player.HaveStatus(Swiftcast.BuffsProvide);

        public virtual bool HaveShield => true;


        public TextureWrap Texture { get; }
        protected CustomCombo()
        {
            Texture = Service.DataManager.GetImGuiTextureIcon(IconSet.GetJobIcon(this));
        }

        public ActionConfiguration Config
        {
            get
            {
                var con = CreateConfiguration();
                if (Service.Configuration.ActionsConfigurations.TryGetValue(Name, out var lastcom))
                {
                    if (con.IsTheSame(lastcom)) return lastcom;
                }
                //con.Supply(lastcom);
                Service.Configuration.ActionsConfigurations[Name] = con;
                Service.Configuration.Save();
                return con;
            }
        }
        private protected virtual ActionConfiguration CreateConfiguration()
        {
            return new ActionConfiguration();
        }

        public void Dispose()
        {
            Texture.Dispose();
        }

        ~CustomCombo()
        {
            Dispose();
        }
    }
}
