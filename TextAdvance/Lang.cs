namespace TextAdvance;

internal static class Lang
{
    internal static string[] AcceptStr = ["Accept", "接受", "Annehmen", "Accepter", "受注"];
    internal static string[] SkipCutsceneStr = ["Skip cutscene?", "要跳过这段过场动画吗？", "要跳過這段過場動畫嗎？", "Videosequenz überspringen?", "Passer la scène cinématique ?", "このカットシーンをスキップしますか？"];
    internal static string[] YesStr = ["Yes.", "是", "Ja", "Oui", "はい"];
    internal static string[] CompleteStr = ["Complete", "完成", "Abschließen", "Accepter", "コンプリート"];

    /*Scaevan accessories of fending coffer
    Scaevan accessories of slaying coffer
    Scaevan accessories of aiming coffer
    Scaevan accessories of healing coffer
    Scaevan accessories of casting coffer*/

    // 台服(TC)注意:這些是拿去比對「物品名稱」(遊戲顯示文字)的,不是內部識別名。
    // 只留英文字面時在台服恆為 false,會讓 TrySelectCoffer 完全靠圖示退化成亂選箱子。
    // 繁中詞出自 7.20 EXD dump 的 Item 表(例:44275~44286「月使*裝備箱/飾品箱」、
    // 44287~44291「信條*飾品箱」):
    //   裝備箱(7 種) 禦敵/制敵/強襲/游擊/精準/治癒/詠咒
    //   飾品箱(5 種) 禦敵/強攻/精準/治癒/詠咒
    // 保留英文字面當 OR 後備,不影響其他語言客戶端。
    internal static string[] CofferOfAiming = ["aiming", "精準"];
    internal static string[] CofferOfStriking = ["striking", "強襲"];
    internal static string[] CofferOfHealing = ["healing", "治癒"];
    internal static string[] CofferOfSlaying = ["slaying", "強攻"];
    internal static string[] CofferOfCasting = ["casting", "詠咒"];
    internal static string[] CofferOfFending = ["fending", "禦敵"];
    internal static string[] CofferOfMaiming = ["maiming", "制敵"];
    internal static string[] CofferOfScouting = ["scouting", "游擊"];
}
