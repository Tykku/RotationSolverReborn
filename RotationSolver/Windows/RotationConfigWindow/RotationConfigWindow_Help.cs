﻿using ImGuiNET;
using RotationSolver.Commands;
using RotationSolver.Localization;
using System.Numerics;

namespace RotationSolver.Windows.RotationConfigWindow
{
    internal partial class RotationConfigWindow
    {
        private void DrawHelpTab()
        {
            ImGui.Text(LocalizationManager.RightLang.ConfigWindow_HelpItem_Description);

            if (ImGui.BeginChild("Help Infomation", new Vector2(0f, -1f), true))
            {
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0f, 5f));

                StateCommandType.Smart.DisplayCommandHelp(getHelp: EnumTranslations.ToHelp);
                ImGui.Separator();

                StateCommandType.Manual.DisplayCommandHelp(getHelp: EnumTranslations.ToHelp);
                ImGui.Separator();

                StateCommandType.Cancel.DisplayCommandHelp(getHelp: EnumTranslations.ToHelp);
                ImGui.Separator();

                SpecialCommandType.HealArea.DisplayCommandHelp(getHelp: EnumTranslations.ToHelp);
                ImGui.Separator();

                SpecialCommandType.HealSingle.DisplayCommandHelp(getHelp: EnumTranslations.ToHelp);
                ImGui.Separator();

                SpecialCommandType.DefenseArea.DisplayCommandHelp(getHelp: EnumTranslations.ToHelp);
                ImGui.Separator();

                SpecialCommandType.DefenseSingle.DisplayCommandHelp(getHelp: EnumTranslations.ToHelp);
                ImGui.Separator();

                SpecialCommandType.EsunaShield.DisplayCommandHelp(getHelp: EnumTranslations.ToHelp);
                ImGui.Separator();

                SpecialCommandType.RaiseShirk.DisplayCommandHelp(getHelp: EnumTranslations.ToHelp);
                ImGui.Separator();

                SpecialCommandType.MoveForward.DisplayCommandHelp(getHelp: EnumTranslations.ToHelp);
                ImGui.Separator();

                SpecialCommandType.MoveBack.DisplayCommandHelp(getHelp: EnumTranslations.ToHelp);
                ImGui.Separator();

                SpecialCommandType.AntiRepulsion.DisplayCommandHelp(getHelp: EnumTranslations.ToHelp);
                ImGui.Separator();

                SpecialCommandType.Break.DisplayCommandHelp(getHelp: EnumTranslations.ToHelp);
                ImGui.Separator();

                SpecialCommandType.EndSpecial.DisplayCommandHelp(getHelp: EnumTranslations.ToHelp);
                ImGui.Separator();
            }
            ImGui.PopStyleVar();
        }
    }
}