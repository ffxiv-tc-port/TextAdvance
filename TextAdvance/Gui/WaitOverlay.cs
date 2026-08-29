using Dalamud.Interface.Utility;
using TextAdvance.Executors;

namespace TextAdvance.Gui;

internal class WaitOverlay : Window
{
    public WaitOverlay() : base("TAWaitOverlay", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse, true)
    {
        this.IsOpen = true;
        this.Position = Vector2.Zero;
        this.RespectCloseHotkey = false;
    }

    internal long StartTime = 0;
    internal int Frame = 0;

    public override bool DrawConditions()
    {
        return P.TaskManager.IsBusy;
    }

    public override void PreDraw()
    {
        // Dalamud Window 基底類別的 PreDraw() 負責推每視窗不透明度(標題列右鍵選單那個滑桿)。
        // 覆寫而不呼叫 base 會讓那個內建功能對本視窗靜默半失效;base 的 push 要在最外層,
        // 才能與 PostDraw 結尾的 base.PostDraw() 構成後進先出的成對 pop。
        base.PreDraw();
        ImGui.SetNextWindowSize(ImGuiHelpers.MainViewport.Size);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, 0x00000033u.Vector4FromRGBA());
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor();
        base.PostDraw();
    }

    public override void Draw()
    {
        ImGui.SetWindowFocus();
        if (ImGui.GetFrameCount() - this.Frame > 1) this.StartTime = Environment.TickCount64;
        this.Frame = ImGui.GetFrameCount();
        CImGui.igBringWindowToDisplayFront(CImGui.igGetCurrentWindow());
        ImGui.Dummy(new(ImGuiHelpers.MainViewport.Size.X, ImGuiHelpers.MainViewport.Size.Y / 3));
        ImGuiEx.ImGuiLineCentered("Waitoverlay1", () => ImGuiEx.Text("Filling in request.".Loc()));
        ImGuiEx.ImGuiLineCentered("Waitoverlay2", () => ImGuiEx.Text("This can take couple seconds. If this process is stuck, please click the button below.".Loc()));
        ImGuiEx.Text("");
        var span = TimeSpan.FromMilliseconds(Environment.TickCount64 - this.StartTime);
        ImGuiEx.ImGuiLineCentered("Waitoverlay4", () => ImGuiEx.Text($"{span.Minutes:D2}:{span.Seconds:D2}"));
        ImGuiEx.Text("");
        ImGuiEx.Text("");
        ImGuiEx.ImGuiLineCentered("Waitoverlay3", () =>
        {
            if (ImGui.Button("Cancel".Loc()))
            {
                P.TaskManager.Abort();
                ExecRequestFill.DontFillThisWindow = true;
            }
        });
    }
}
