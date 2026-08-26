using Dalamud.Interface.Components;

namespace TextAdvance.Gui;

internal static class TabTerritory
{
    private static uint SelectedKey = uint.MaxValue;
    private static bool OnlyModded = false;
    private static string Filter = string.Empty;
    internal static void Draw()
    {
        ImGui.Checkbox("Global enable overrides local settings".Loc(), ref C.GlobalOverridesLocal);
        ImGuiEx.TextWrapped(("If this checkbox is checked, when enabling plugin with /at command per area settings will become irrelevant " +
            "and global settings will be used.\nOtherwise per area settings will always be used, regardless of plugin's global state.").Loc());
        ImGuiEx.Text("Current plugin state: globally ".Loc());
        ImGui.SameLine(0, 0);
        ImGuiEx.Text(P.Enabled ? ImGuiColors.ParsedGreen : ImGuiColors.DalamudRed, (P.Enabled ? "enabled" : "disabled").Loc());
        ImGui.SameLine(0, 0);
        ImGuiEx.Text(", locally ".Loc());
        ImGui.SameLine(0, 0);
        ImGuiEx.Text(P.IsTerritoryEnabled() ? ImGuiColors.ParsedGreen : ImGuiColors.DalamudRed, (P.IsTerritoryEnabled() ? "enabled" : "disabled").Loc());
        ImGuiEx.SetNextItemFullWidth();
        if (ImGui.BeginCombo("##terrselect", P.TerritoryNames.TryGetValue(SelectedKey, out var selected) ? selected : "Select an area...".Loc()))
        {
            ImGui.SetNextItemWidth(200f);
            ImGui.InputTextWithHint("##selectflts", "Filter".Loc(), ref Filter, 50);
            ImGui.SameLine();
            ImGui.Checkbox("Only modified".Loc(), ref OnlyModded);
            if (P.TerritoryNames.TryGetValue(Svc.ClientState.TerritoryType, out var current) && ImGui.Selectable($"{"Current:".Loc()} {current}"))
            {
                SelectedKey = Svc.ClientState.TerritoryType;
            }
            ImGui.Separator();
            foreach (var x in P.TerritoryNames)
            {
                if (Filter != string.Empty && !x.Value.Contains(Filter, StringComparison.OrdinalIgnoreCase)) continue;
                if (OnlyModded && !C.TerritoryConditions.ContainsKey(x.Key)) continue;
                if (ImGui.Selectable(x.Value, C.TerritoryConditions.ContainsKey(x.Key)))
                {
                    SelectedKey = x.Key;
                }
                if (ImGui.IsWindowAppearing() && SelectedKey == x.Key)
                {
                    ImGui.SetScrollHereY();
                }
            }
            ImGui.EndCombo();
        }
        if (P.TerritoryNames.ContainsKey(SelectedKey))
        {
            if (C.TerritoryConditions.TryGetValue(SelectedKey, out var settings))
            {
                if (ImGui.Button("Remove custom settings".Loc()))
                {
                    C.TerritoryConditions.Remove(SelectedKey);
                }
                ImGui.Checkbox("Automatic quest accept".Loc(), ref settings.EnableQuestAccept);
                ImGui.Checkbox("Automatic quest complete".Loc(), ref settings.EnableQuestComplete);
                ImGui.Checkbox("Automatic reward pick (RP) (BETA)".Loc(), ref settings.EnableRewardPick);
                ImGui.Checkbox("Automatic talk skip".Loc(), ref settings.EnableTalkSkip);
                ImGui.Checkbox("Semi-automatic request handin".Loc(), ref settings.EnableRequestHandin);
                ImGui.Checkbox("Automatic request fill (RF) (NEW!)".Loc(), ref settings.EnableRequestFill);
                ImGui.Checkbox("Automatic ESC press during cutscene".Loc(), ref settings.EnableCutsceneEsc);
                ImGui.Checkbox("Automatic cutscene skip confirmation".Loc(), ref settings.EnableCutsceneSkipConfirm);
                ImGui.Checkbox("Automatic interaction with quest-related object (IN)".Loc(), ref settings.EnableAutoInteract);
                ImGuiComponents.HelpMarker("Automatically interacts with nearby quest-related NPCs and objects.".Loc());
                ImGui.Checkbox("Automatic key item use (KI)".Loc(), ref settings.EnableUseEventItem);
                ImGuiComponents.HelpMarker("When a quest objective asks you to use a key item, automatically uses the appropriate key item on the quest target. Intended for doing quests manually; quest automation plugins like Questionable use items by themselves and do not need this.".Loc());
                ImGui.Separator();
                ImGui.Checkbox("Display quest target indicators".Loc(), ref settings.QTIQuestEnabled);
                ImGui.ColorEdit4("Quest target indicator color".Loc(), ref settings.QTIQuestColor, ImGuiColorEditFlags.NoInputs);
                ImGui.Checkbox("Quest target indicator tether".Loc(), ref settings.QTIQuestTether);
                ImGui.SetNextItemWidth(60f);
                ImGui.DragFloat("Quest target indicator thickness".Loc(), ref settings.QTIQuestThickness, 0.02f, 1f, 10f);
            }
            else
            {
                ImGuiEx.Text("No custom settings are present for this area.".Loc());
                if (ImGui.Button("Create custom settings".Loc()))
                {
                    C.TerritoryConditions[SelectedKey] = new();
                }
            }
        }
    }
}
