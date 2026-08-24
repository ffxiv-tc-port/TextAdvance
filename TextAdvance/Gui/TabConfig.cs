using Dalamud.Interface.Components;
using ECommons.SplatoonAPI;
using Lumina.Excel.Sheets;
using NightmareUI.PrimaryUI;

namespace TextAdvance.Gui;

internal static class TabConfig
{
    public static string Filter = "";
    internal static void Draw()
    {
        if (P.BlockList.Count > 0)
        {
            ImGuiEx.TextWrapped(EColor.RedBright, $"{"TextAdvance is stopped by these plugins:".Loc()} {string.Join(",", P.BlockList)}");
            if (ImGui.SmallButton("Remove all locks".Loc()))
            {
                P.BlockList.Clear();
            }
        }

        if (S.IPCProvider.IsInExternalControl())
        {
            ImGuiEx.TextWrapped(EColor.RedBright, $"{"TextAdvance is externally controlled by".Loc()} {S.IPCProvider.Requester}. {"All your settings are being ignored.".Loc()}");
            if (ImGui.SmallButton("Cancel external control".Loc()))
            {
                S.IPCProvider.Requester = null;
                S.IPCProvider.ExternalConfig = null;
            }
        }

        new NuiBuilder()
        .Section("Plugin operation".Loc())
        .Widget(() =>
        {
            ImGui.Checkbox("Enable plugin (non-persistent)".Loc(), ref P.Enabled);
            ImGui.Checkbox("Don't auto-disable plugin on logout".Loc(), ref C.DontAutoDisable);
            ImGui.Checkbox("Enable quest target indicators (global, persistent)".Loc(), ref C.QTIEnabled);
            ImGui.Indent();
            ImGui.Checkbox("Enable indicators when plugin is disabled".Loc(), ref C.QTAEnabledWhenTADisable);
            ImGui.Checkbox("Enable finder when plugin is disabled".Loc(), ref C.QTAFinderEnabledWhenTADisable);
            if (C.QTIEnabled)
            {
                if (!Splatoon.IsConnected())
                {
                    ImGuiEx.TextWrapped(EColor.PurpleBright, "You need to have Splatoon plugin installed and enabled for quest target indicators to work".Loc());
                }
            }
            ImGui.Unindent();
            ImGui.SetNextItemWidth(150f);
            ImGui.SliderFloat("Auto-interact max radius".Loc(), ref C.AutoInteractMaxRadius, 3f, 10f);
            ImGuiEx.HelpMarker("Lowering this radius may cause issues".Loc(), ImGuiColors.DalamudOrange, FontAwesomeIcon.ExclamationTriangle.ToIconString());
        })

        .Section("Keybinds".Loc())
        .Widget(() =>
        {
            ImGui.Text("Button to hold to temporarily disable plugin when active:".Loc());
            ImGui.SetNextItemWidth(200f);
            ImGuiEx.EnumCombo("##HoldDisable", ref C.TempDisableButton);
            ImGui.Text("Button to hold to temporarily enable plugin when inactive:".Loc());
            ImGui.SetNextItemWidth(200f);
            ImGuiEx.EnumCombo("##HoldEnable", ref C.TempEnableButton);
        })

        .Section("Functions".Loc())
        .Widget(() =>
        {
            ImGui.Checkbox("Automatic quest accept (QA)".Loc(), ref C.MainConfig.EnableQuestAccept);
            ImGuiComponents.HelpMarker("Automatically accepts new quests when talking to quest initiating NPC".Loc());
            ImGui.Checkbox("Automatic quest complete (QC)".Loc(), ref C.MainConfig.EnableQuestComplete);
            ImGuiComponents.HelpMarker("Automatically completes quests when talking to NPC that completes quest".Loc());
            ImGui.Checkbox("Automatic reward pick (RP)".Loc(), ref C.MainConfig.EnableRewardPick);
            ImGuiComponents.HelpMarker("Automatically picks quest completion reward based on simple rules that are configured below".Loc());
            ImGui.Checkbox("Automatic talk skip (TS)".Loc(), ref C.MainConfig.EnableTalkSkip);
            ImGuiComponents.HelpMarker("Automatically advances most of the subtitles. Some subtitles may only be advanced manually still.".Loc());
            ImGui.Checkbox("Auto-confirm request handins (RH)".Loc(), ref C.MainConfig.EnableRequestHandin);
            ImGuiComponents.HelpMarker("Automatically confirms most of item requests once filled. Some requests may not be automatically confirmed.".Loc());
            ImGui.Checkbox("Automatic request fill (RF)".Loc(), ref C.MainConfig.EnableRequestFill);
            ImGuiComponents.HelpMarker("Automatically fills item request window. Some requests may not be automatically filled.".Loc());
            if (C.MainConfig.EnableRequestFill)
            {
                ImGui.Indent();
                ImGui.SetNextItemWidth(200f);
                if (ImGui.BeginCombo("##RequestFillQuality", C.MainConfig.RequestFillQualityPreference.ToString()))
                {
                    if (ImGui.Selectable("NQ", C.MainConfig.RequestFillQualityPreference == RequestFillQualityPreference.NQ))
                        C.MainConfig.RequestFillQualityPreference = RequestFillQualityPreference.NQ;
                    if (ImGui.Selectable("HQ", C.MainConfig.RequestFillQualityPreference == RequestFillQualityPreference.HQ))
                        C.MainConfig.RequestFillQualityPreference = RequestFillQualityPreference.HQ;
                    if (ImGui.Selectable("Any".Loc(), C.MainConfig.RequestFillQualityPreference == RequestFillQualityPreference.Any))
                        C.MainConfig.RequestFillQualityPreference = RequestFillQualityPreference.Any;
                    ImGui.EndCombo();
                }
                ImGui.SameLine();
                ImGuiComponents.HelpMarker("NQ: Use Normal Quality items first, preserving valuable HQ items.\nHQ: Use High Quality items first.\nAny: Use first available (game default).".Loc());
                ImGui.Unindent();
            }
            ImGui.Checkbox("Automatic ESC press during cutscene (CS)".Loc(), ref C.MainConfig.EnableCutsceneEsc);
            ImGuiComponents.HelpMarker("Automatically presses ESC key during cutscene when cutscene is skippable. Does not skips normally unskippable cutscenes.".Loc());
            ImGui.Checkbox("Automatic cutscene skip confirmation (CC)".Loc(), ref C.MainConfig.EnableCutsceneSkipConfirm);
            ImGuiComponents.HelpMarker("Automatically confirms cutscene skips upon pressing ESC.".Loc());
            ImGui.Checkbox("Automatic interaction with quest-related object (IN)".Loc(), ref C.MainConfig.EnableAutoInteract);
            ImGuiComponents.HelpMarker("Automatically interacts with nearby quest-related NPCs and objects.".Loc());
            ImGui.Checkbox("Automatic key item use (KI)".Loc(), ref C.MainConfig.EnableUseEventItem);
            ImGuiComponents.HelpMarker("When a quest objective asks you to use a key item, automatically uses the appropriate key item on the quest target. Intended for doing quests manually; quest automation plugins like Questionable use items by themselves and do not need this.".Loc());
        })

        .Section("Overlay".Loc())
        .Widget(() =>
        {
            ImGui.Checkbox("Enable overlay when plugin is enabled".Loc(), ref C.EnableOverlay);
            if (C.EnableOverlay)
            {
                ImGui.SetNextItemWidth(100f);
                ImGui.DragFloat2("Overlay offset".Loc(), ref C.OverlayOffset);
            }
        })

        .Section("Notifications".Loc())
        .Widget(() =>
        {
            ImGui.Checkbox("Disable manual plugin state change notification".Loc(), ref C.NotifyDisableManualState);
            ImGui.Checkbox("Disable notification upon login if character has auto-enable on".Loc(), ref C.NotifyDisableOnLogin);
            ImGui.Checkbox("Disable reward pick chat notification".Loc(), ref C.PickRewardSilent);
        })

        .Section("Reward picking order".Loc())
        .Widget(() =>
        {
            ImGuiEx.TextWrapped("Please note: precision is not guaranteed. ".Loc());
            ImGuiEx.EnumOrderer("", C.PickRewardOrder);
        })

        .Section("Navigation".Loc())
        .Widget(() =>
        {
            ImGui.Checkbox("Enabled##autointeract".Loc(), ref C.Navmesh);
            ImGuiEx.PluginAvailabilityIndicator([new("vnavmesh")]);
            ImGui.Checkbox("Auto-interact upon arrival".Loc(), ref C.NavmeshAutoInteract);
            ImGui.SetNextItemWidth(200f);
            var current = C.Mount == -1 ? "Use no mount".Loc() : (C.Mount == 0 ? "Mount roulette".Loc() : $"{Utils.GetMountName(C.Mount)}");
            if (ImGui.BeginCombo($"##mount", current))
            {
                ImGui.InputTextWithHint($"##fltr", "Search".Loc(), ref Filter, 50);
                if (ImGui.Selectable("Use no mount".Loc(), C.Mount == -1)) C.Mount = -1;
                if (ImGui.Selectable("Mount roulette".Loc(), C.Mount == 0)) C.Mount = 0;
                foreach (var x in Svc.Data.GetExcelSheet<Mount>())
                {
                    var n = x.Singular.ExtractText();
                    if (n == "") continue;
                    if (Filter != "" && !n.Contains(Filter, StringComparison.OrdinalIgnoreCase)) continue;
                    if (C.Mount == x.RowId && ImGui.IsWindowAppearing()) ImGui.SetScrollHereY();
                    if (ImGui.Selectable($"{n}##{x.RowId}", C.Mount == x.RowId))
                    {
                        C.Mount = (int)x.RowId;
                    }
                }
                ImGui.EndCombo();
            }
            ImGui.Checkbox("Use sprint and peloton if possible".Loc(), ref C.UseSprintPeloton);
            ImGui.Checkbox("Print navigation status into chat".Loc(), ref C.NavStatusChat);
            ImGui.Checkbox("(very experimental) Allow flight".Loc(), ref C.EnableFlight);
            //ImGui.Checkbox($"Allow teleporting to the nearest Aetheryte to a flag", ref C.EnableTeleportToFlag);
        })

        .Draw();
    }
}
