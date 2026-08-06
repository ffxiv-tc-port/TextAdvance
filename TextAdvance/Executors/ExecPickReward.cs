using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.ChatMethods;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;

namespace TextAdvance.Executors
{
    internal static unsafe class ExecPickReward
    {
        internal static bool IsEnabled = false;
        internal static readonly uint[] CofferIcons = [26557, 26509, 26558, 26559, 26560, 26561, 26562, 25916, 26564, 26565, 26566, 26567,];
        internal static readonly uint[] GilIcons = [26001];
        internal static Random Random = new();

        internal static void Init()
        {
            Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "JournalResult", OnJournalResultSetup);
        }

        private static void OnJournalResultSetup(AddonEvent type, AddonArgs args)
        {
            var addon = (AtkUnitBase*)args.Addon.Address;

            // 🔴 NodeList[7] 是裸解參考:PostSetup 當下版面不一定已經建好,NodeList 可能是 null
            // 或不足 8 格,取到的節點也可能是 null。讀空指標會丟出 AccessViolationException,
            // 那是 corrupted-state exception,try/catch 與任何例外隔離都攔不到,只能事前擋。
            // 讀不出來就這次不挑獎勵(維持原本「不動作」的安全方向),並留一行使用者回報得到的紀錄。
            if (addon->UldManager.NodeList == null || addon->UldManager.NodeListCount <= 7)
            {
                PluginLog.Information("JournalResult: 節點清單尚未建好(NodeListCount 不足),這次不自動挑選獎勵。");
                return;
            }
            var canvasNode = (AtkComponentNode*)addon->UldManager.NodeList[7];
            if (canvasNode == null)
            {
                PluginLog.Information("JournalResult: 獎勵清單節點是空指標,這次不自動挑選獎勵。");
                return;
            }
            var canvas = canvasNode->Component;
            if (canvas == null)
            {
                PluginLog.Information("JournalResult: 獎勵清單節點還沒有 Component,這次不自動挑選獎勵。");
                return;
            }
            PluginLog.Information($"Component: {(nint)canvas:X16}");
            if (IsEnabled)
            {
                var r = new ReaderJournalResult(addon);
                if (r.OptionalRewards.Count > 0)
                {
                    PluginLog.Information($"Preparing to select optional reward item. Candidates: ({r.OptionalRewards.Count})\n{r.OptionalRewards.Select(x => $"ID:{x.ItemID} / Icon:{x.IconID} / Amount:{x.Amount} / Name:{x.Name} ").Print("\n")}");
                    foreach (var x in r.OptionalRewards)
                    {
                        if (Svc.Data.GetExcelSheet<Item>().GetRowOrDefault(x.ItemID) == null)
                        {
                            DuoLog.Warning($"Encountered unknown item id: {x.ItemID}. Selecting cancelled. Please report this error with logs and screenshot.");
                            return;
                        }
                    }
                    foreach (var x in C.PickRewardOrder)
                    {
                        {
                            if (x == PickRewardMethod.Gil_sacks && TrySelectGil(r.OptionalRewards, out var index))
                            {
                                PluginLog.Information($"Selecting {index} = {r.OptionalRewards[index].Name} because it's gil sack");
                                if (!C.PickRewardSilent) ChatPrinter.Green($"[TextAdvance] Auto-selected optional reward {index + 1}/{r.OptionalRewards.Count}: {r.OptionalRewards[index].Name} (gil)");
                                S.Memory.PickRewardItemUnsafe((nint)canvas, index);
                                return;
                            }
                        }
                        {
                            if (x == PickRewardMethod.Highest_vendor_value && TrySelectHighestVendorValue(r.OptionalRewards, out var index))
                            {
                                PluginLog.Information($"Selecting {index} = {r.OptionalRewards[index].Name} because it's highest vendor value");
                                if (!C.PickRewardSilent) ChatPrinter.Green($"[TextAdvance] Auto-selected optional reward {index + 1}/{r.OptionalRewards.Count}: {r.OptionalRewards[index].Name} (highest value)");
                                S.Memory.PickRewardItemUnsafe((nint)canvas, index);
                                return;
                            }
                        }
                        {
                            if (x == PickRewardMethod.Gear_coffer && TrySelectCoffer(r.OptionalRewards, out var index))
                            {
                                PluginLog.Information($"Selecting {index} = {r.OptionalRewards[index].Name} because it's coffer");
                                if (!C.PickRewardSilent) ChatPrinter.Green($"[TextAdvance] Auto-selected optional reward {index + 1}/{r.OptionalRewards.Count}: {r.OptionalRewards[index].Name} (coffer)");
                                S.Memory.PickRewardItemUnsafe((nint)canvas, index);
                                return;
                            }
                        }
                        {
                            if (x == PickRewardMethod.Equipable_item_for_current_job && TrySelectCurrentJobItem(r.OptionalRewards, out var index))
                            {
                                PluginLog.Information($"Selecting {index} = {r.OptionalRewards[index].Name} because it's current job item");
                                if (!C.PickRewardSilent) ChatPrinter.Green($"[TextAdvance] Auto-selected optional reward {index + 1}/{r.OptionalRewards.Count}: {r.OptionalRewards[index].Name} (equipable)");
                                S.Memory.PickRewardItemUnsafe((nint)canvas, index);
                                return;
                            }
                        }
                        {
                            if (x == PickRewardMethod.High_quality_gear && TrySelectHighQualityGear(r.OptionalRewards, out var index))
                            {
                                PluginLog.Information($"Selecting {index} = {r.OptionalRewards[index].Name} because it's high quality gear item");
                                if (!C.PickRewardSilent) ChatPrinter.Green($"[TextAdvance] Auto-selected optional reward {index + 1}/{r.OptionalRewards.Count}: {r.OptionalRewards[index].Name} (HQ gear item)");
                                S.Memory.PickRewardItemUnsafe((nint)canvas, index);
                                return;
                            }
                        }
                    }
                    var rand = Random.Next(r.OptionalRewards.Count);
                    PluginLog.Information($"Selecting random reward: {rand} - {r.OptionalRewards[rand].Name}");
                    if (!C.PickRewardSilent) ChatPrinter.Green($"[TextAdvance] Auto-selected optional reward {rand + 1}/{r.OptionalRewards.Count}: {r.OptionalRewards[rand].Name} (random)");
                    S.Memory.PickRewardItemUnsafe((nint)canvas, rand);
                    return;
                }
            }
        }

        internal static void Shutdown()
        {
            Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "JournalResult", OnJournalResultSetup);
            Random = null;
        }

        internal static string[] DetermineGearCoffer()
        {
            if (Player.Object.GetRole() == CombatRole.Tank) return Lang.CofferOfFending;
            if (Player.Object.GetRole() == CombatRole.Healer) return Lang.CofferOfHealing;
            if (Player.Job.GetUpgradedJob().EqualsAny(Job.BRD, Job.DNC, Job.MCH)) return Lang.CofferOfAiming;
            if (Player.Job.GetUpgradedJob().EqualsAny(Job.DRG, Job.RPR)) return Lang.CofferOfMaiming;
            if (Player.Job.GetUpgradedJob().EqualsAny(Job.NIN, Job.VPR)) return Lang.CofferOfScouting;
            if (Player.Object.ClassJob.Value.Role == 2) return Lang.CofferOfStriking; //other melees; armour coffers for melee say "striking"/強襲, never "slaying"
            if (Player.Object.GetRole() == CombatRole.DPS) return Lang.CofferOfCasting; //only casters left
            return null; //doh/dol
        }

        internal static string[] DetermineAccessoryCoffer()
        {
            if (Player.Object.GetRole() == CombatRole.Tank) return Lang.CofferOfFending;
            if (Player.Object.GetRole() == CombatRole.Healer) return Lang.CofferOfHealing;
            if (Player.Job.GetUpgradedJob().EqualsAny(Job.BRD, Job.DNC, Job.MCH, Job.NIN, Job.VPR)) return Lang.CofferOfAiming;
            if (Player.Object.ClassJob.Value.Role == 3) return Lang.CofferOfCasting; //phys rangeds are excluded before
            if (Player.Object.GetRole() == CombatRole.DPS) return Lang.CofferOfSlaying; //only non-aiming melee left; accessory coffers for melee say "slaying"/強攻, never "striking"
            return null; //doh/dol
        }

        internal static bool TrySelectCoffer(List<ReaderJournalResult.OptionalReward> data, out int index)
        {
            List<int> possible = [];
            var accessoryString = DetermineAccessoryCoffer();
            var gearString = DetermineGearCoffer();
            for (var i = 0; i < data.Count; i++)
            {
                if (gearString != null && data[i].Name.ContainsAny(StringComparison.OrdinalIgnoreCase, gearString))
                {
                    PluginLog.Information($"Gear string was {gearString.Print()}");
                    index = i;
                    return true;
                }
                if (accessoryString != null && data[i].Name.ContainsAny(StringComparison.OrdinalIgnoreCase, accessoryString))
                {
                    PluginLog.Information($"Accessory string was {accessoryString.Print()}");
                    index = i;
                    return true;
                }
            }
            for (var i = 0; i < data.Count; i++)
            {
                var d = data[i];
                if (CofferIcons.Contains(d.IconID))
                {
                    possible.Add(i);
                }
            }
            // 只有一個裝備箱候選時沒有歧義,直接選它。
            if (possible.Count == 1)
            {
                index = possible[0];
                return true;
            }
            // 多個候選但職能字串一個都比不到:原本會「隨機挑一個裝備箱」,
            // 在台服因為職能字串曾經只有英文而恆為 false,等於永遠亂選(坦克箱給法師),
            // 而且完全靜默。改成放棄本規則並留下可辨識的紀錄,交給後面的規則處理。
            if (possible.Count > 1)
            {
                PluginLog.Information($"Coffer rule: {possible.Count} coffer candidates but none matched the job strings " +
                    $"(gear: {(gearString == null ? "<null>" : gearString.Print())} / accessory: {(accessoryString == null ? "<null>" : accessoryString.Print())}). " +
                    $"Refusing to pick at random; falling through to the next reward rule.");
            }
            index = default;
            return false;
        }

        internal static bool TrySelectGil(List<ReaderJournalResult.OptionalReward> data, out int index)
        {
            for (var i = 0; i < data.Count; i++)
            {
                var d = data[i];
                if (GilIcons.Contains(d.IconID))
                {
                    index = i;
                    return true;
                }
            }
            index = default;
            return false;
        }

        internal static bool TrySelectHighestVendorValue(List<ReaderJournalResult.OptionalReward> data, out int index)
        {
            var value = 0u;
            index = 0;
            for (var i = 0; i < data.Count; i++)
            {
                var d = data[i];
                var item = Svc.Data.GetExcelSheet<Item>().GetRowOrDefault(d.ItemID);
                if (item != null && item.Value.PriceLow * d.Amount > value)
                {
                    index = i;
                    value = item.Value.PriceLow * d.Amount;
                }
            }
            return value > 0;
        }

        internal static bool TrySelectCurrentJobItem(List<ReaderJournalResult.OptionalReward> data, out int index)
        {
            List<int> possible = [];
            if (Player.Available)
            {
                for (var i = 0; i < data.Count; i++)
                {
                    var d = data[i];
                    var item = Svc.Data.GetExcelSheet<Item>().GetRowOrDefault(d.ItemID);
                    if (item != null && item.Value.ClassJobCategory.ValueNullable != null && item.Value.ClassJobCategory.Value.IsJobInCategory((Job)Player.Object.ClassJob.RowId))
                    {
                        possible.Add(i);
                    }
                }
            }
            if (possible.Count > 0)
            {
                index = possible[Random.Next(possible.Count)];
                return true;
            }
            index = default;
            return false;
        }

        internal static readonly uint[] GearCats = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 40, 41, 42, 43, 84, 87, 88, 89, 96, 97, 98, 99, 105, 106, 107, 108, 109];
        internal static bool TrySelectHighQualityGear(List<ReaderJournalResult.OptionalReward> data, out int index)
        {
            List<int> possible = [];
            for (var i = 0; i < data.Count; i++)
            {
                var d = data[i];
                var item = Svc.Data.GetExcelSheet<Item>().GetRowOrDefault(d.ItemID);
                if (d.IsHQ && item != null && item.Value.ItemUICategory.ValueNullable?.RowId.EqualsAny(GearCats) == true)
                {
                    possible.Add(i);
                }
            }
            if (possible.Count > 0)
            {
                index = possible[Random.Next(possible.Count)];
                return true;
            }
            index = default;
            return false;
        }
    }
}
