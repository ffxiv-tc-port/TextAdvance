using ECommons.SimpleGui;

namespace TextAdvance.Gui;

public class ConfigGui : ConfigWindow
{
    Overlay Overlay = new();
    WaitOverlay WaitOverlay = new();
    ProgressOverlay ProgressOverlay = new();
    private ConfigGui()
    {
        EzConfigGui.Init(this);
        EzConfigGui.WindowSystem.AddWindow(Overlay);
        EzConfigGui.WindowSystem.AddWindow(WaitOverlay);
        EzConfigGui.WindowSystem.AddWindow(ProgressOverlay);
    }

    public override void Draw()
    {
        if (ImGui.BeginChild("Child", new(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - ImGui.GetFrameHeightWithSpacing())))
        {
            // 私有 TC fork 不需要上游贊助 banner;KoFiTransparent 傳 null 以繞開
            // ECommons 此 pin 的 PatreonBanner.RightTransparentTab → ImGuiEx.BeginTabItem(label, flags)
            // throw stub(EzTabBar stub bug class),否則開設定視窗必崩。
            ImGuiEx.EzTabBar("TextAdvanceTab", null,
                ("General config".Loc(), TabConfig.Draw, null, true),
                ("Target indicators".Loc(), TabSplatoon.Draw, null, true),
                ("Auto-enable".Loc(), TabChars.Draw, null, true),
                ("Per area config".Loc(), TabTerritory.Draw, null, true),
                InternalLog.ImGuiTab(),
                ("Debug".Loc(), TabDebug.Draw, ImGuiColors.DalamudGrey3, true)
                );
        }
        ImGui.EndChild();
    }

    public override void PreDraw()
    {
        base.PreDraw();
    }
}
