namespace TextAdvance.Gui
{
    public static class TabSplatoon
    {
        public static void Draw()
        {
            ImGuiEx.TextWrapped("These functions require Splatoon plugin installed and enabled.".Loc());
            if (Svc.PluginInterface.InstalledPlugins.TryGetFirst(x => x.InternalName == "Splatoon", out var plugin))
            {
                if (plugin.IsLoaded)
                {
                    ImGuiEx.TextWrapped(EColor.Green, $"{"You have Splatoon installed and enabled.".Loc()} (v{plugin.Version})");
                }
                else
                {
                    ImGuiEx.TextWrapped(EColor.Red, $"{"You have Splatoon installed but not enabled.".Loc()} (v{plugin.Version})");
                }
            }
            else
            {
                ImGuiEx.TextWrapped(EColor.Red, "You do not have Splatoon installed.".Loc());
                if (ImGui.Button("Get Splatoon".Loc())) ShellStart("https://puni.sh/plugin/Splatoon");
            }
            ImGui.Checkbox("Display quest target indicators".Loc(), ref C.MainConfig.QTIQuestEnabled);
            ImGui.ColorEdit4("Quest target indicator color".Loc(), ref C.MainConfig.QTIQuestColor, ImGuiColorEditFlags.NoInputs);
            ImGui.Checkbox("Quest target indicator tether".Loc(), ref C.MainConfig.QTIQuestTether);
            ImGuiEx.SetNextItemWidthScaled(60f);
            ImGui.DragFloat("Quest target indicator thickness".Loc(), ref C.MainConfig.QTIQuestThickness, 0.02f, 1f, 10f);
            ImGui.Separator();
            ImGui.Checkbox("Enable event object finder".Loc(), ref C.EObjFinder);
            ImGui.Checkbox("Enable event NPC finder".Loc(), ref C.ENpcFinder);
            ImGuiEx.SetNextItemWidthScaled(150f);
            ImGuiEx.EnumCombo("Display only while holding key".Loc(), ref C.FinderKey);
        }
    }
}
