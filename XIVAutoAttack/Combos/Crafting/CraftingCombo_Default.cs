﻿using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XIVAutoAttack.Actions;
using XIVAutoAttack.Helpers;

namespace XIVAutoAttack.Combos.Crafting
{
    internal class CraftingCombo_Default : CraftingCombo
    {
        public CraftingCombo_Default()
        {
        }

        public override string Description => "测试";

        public override string GameVersion => "6.2";

        public override string Author => "测试";

        public override bool TryInvoke(out IAction newAction)
        {
            //需要推质量
            if (CanHQ && MaxQuality != CurrentQuality)
            {
                //第一步，直接闲静
                if (StepNumber == 0 && Reflect.ShouldUse(out newAction)) return true;

                bool highQuality = CraftCondition is Updaters.CraftCondition.Good or Updaters.CraftCondition.Excellent;

                //高质量。
                if (highQuality)
                {
                    //比尔格说可以直接带走
                    if(ByregotsBlessing.Quality > MaxQuality - CurrentQuality)
                    {
                        if (ByregotsBlessing.ShouldUse(out newAction)) return true;
                    }
                    //集中加工
                    if (PreciseTouch.ShouldUse(out newAction)) return true;
                }

                //如果没有长期简约啊。
                if(WasteNotTime == 0)
                {
                    //弄点简约喽。这段还需要考量一下。
                    if (WasteNot2.ShouldUse(out newAction)) return true;
                }

                //比尔格说可以直接带走
                if (ByregotsBlessing.Quality > MaxQuality - CurrentQuality)
                {
                    if (ByregotsBlessing.ShouldUse(out newAction)) return true;
                }
                //如果两个比尔格说可以带走,直接阔步！
                if (ByregotsBlessing.Quality * 2 > MaxQuality - CurrentQuality)
                {
                    if (GreatStrides.ShouldUse(out newAction)) return true;
                }

                //如果一个普通制作还不能送走
                if (BasicSynthesis.Progress <= MaxProgress - CurrentProgress)
                {
                    //一个高速制作会把它送走 最终确认一下
                    if(RapidSynthesis.Progress > MaxProgress - CurrentProgress)
                    {
                        if(FinalAppraisal.ShouldUse(out newAction)) return true;
                    }

                    //高速制作
                    if(RapidSynthesis.ShouldUse(out newAction)) return true;
                }

                //能随便送走，但是质量还不行，普通推质量。

            }

            //第一步，直接坚信
            if (StepNumber == 0 && MuscleMemory.ShouldUse(out newAction)) return true;

            //好了，制作一下。
            if (BasicSynthesis.ShouldUse(out newAction)) return true;

            newAction = null;
            return false;
        }
    }
}