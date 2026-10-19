using ECommons.Configuration;
using ECommons.Interop;

namespace TextAdvance;

public enum RequestFillQualityPreference
{
    NQ = 0,  // Prefer Normal Quality items over HQ
    HQ = 1,  // Prefer High Quality items over NQ
    Any = 2        // Use first available (game default)
}

[Serializable]
public class Config : IEzConfig
{
    public int Version { get; set; } = 2;
    public HashSet<string> AutoEnableNames = [];
    public Button TempEnableButton = Button.NONE;
    public Button TempDisableButton = Button.NONE;
    public TerritoryConfig MainConfig = new();
    public Dictionary<uint, TerritoryConfig> TerritoryConditions = [];
    public bool GlobalOverridesLocal = false;
    public bool EnableOverlay = true;
    public Vector2 OverlayOffset = new(0, -10);
    public bool NotifyDisableManualState = false;
    public bool NotifyDisableOnLogin = false;
    public bool QTIEnabled = false;
    public List<PickRewardMethod> PickRewardOrder = [PickRewardMethod.Gear_coffer, PickRewardMethod.High_quality_gear, PickRewardMethod.Highest_vendor_value, PickRewardMethod.Gil_sacks, PickRewardMethod.Equipable_item_for_current_job];
    public bool PickRewardSilent = false;
    public bool ENpcFinder = false;
    public bool EObjFinder = false;
    public LimitedKeys FinderKey = LimitedKeys.LeftControlKey;
    public bool DontAutoDisable = false;
    public bool Navmesh = false;
    public bool NavmeshAutoInteract = false;
    public int Mount = 0;
    public bool NavStatusChat = false;
    public bool DisplayFunding = true;
    public bool EnableFlight = false;
    public uint? AllowUnsafeOptions = null;
    public bool EnableTeleportToFlag = false;
    public bool QTAEnabledWhenTADisable = false;
    public bool QTAFinderEnabledWhenTADisable = false;
    public float AutoInteractMaxRadius = 10f;
    public bool UseSprintPeloton = true;

    public bool GetEnableQuestAccept()
    {
        var ext = S.IPCProvider.GetActiveExternalControl();
        if (ext != null) return ext.Config.EnableQuestAccept ?? this.MainConfig.EnableQuestAccept;
        if (!(this.GlobalOverridesLocal && P.Enabled) && this.TerritoryConditions.TryGetValue(Svc.ClientState.TerritoryType, out var val))
        {
            return val.EnableQuestAccept;
        }
        return this.MainConfig.EnableQuestAccept;
    }
    public bool GetEnableQuestComplete()
    {
        var ext = S.IPCProvider.GetActiveExternalControl();
        if (ext != null) return ext.Config.EnableQuestComplete ?? this.MainConfig.EnableQuestComplete;
        if (!(this.GlobalOverridesLocal && P.Enabled) && this.TerritoryConditions.TryGetValue(Svc.ClientState.TerritoryType, out var val))
        {
            return val.EnableQuestComplete;
        }
        return this.MainConfig.EnableQuestComplete;
    }
    public bool GetEnableRequestHandin()
    {
        var ext = S.IPCProvider.GetActiveExternalControl();
        if (ext != null) return ext.Config.EnableRequestHandin ?? this.MainConfig.EnableRequestHandin;
        if (!(this.GlobalOverridesLocal && P.Enabled) && this.TerritoryConditions.TryGetValue(Svc.ClientState.TerritoryType, out var val))
        {
            return val.EnableRequestHandin;
        }
        return this.MainConfig.EnableRequestHandin;
    }
    public bool GetEnableCutsceneEsc()
    {
        var ext = S.IPCProvider.GetActiveExternalControl();
        if (ext != null) return ext.Config.EnableCutsceneEsc ?? this.MainConfig.EnableCutsceneEsc;
        if (!(this.GlobalOverridesLocal && P.Enabled) && this.TerritoryConditions.TryGetValue(Svc.ClientState.TerritoryType, out var val))
        {
            return val.EnableCutsceneEsc;
        }
        return this.MainConfig.EnableCutsceneEsc;
    }
    public bool GetEnableCutsceneSkipConfirm()
    {
        var ext = S.IPCProvider.GetActiveExternalControl();
        if (ext != null) return ext.Config.EnableCutsceneSkipConfirm ?? this.MainConfig.EnableCutsceneSkipConfirm;
        if (!(this.GlobalOverridesLocal && P.Enabled) && this.TerritoryConditions.TryGetValue(Svc.ClientState.TerritoryType, out var val))
        {
            return val.EnableCutsceneSkipConfirm;
        }
        return this.MainConfig.EnableCutsceneSkipConfirm;
    }
    public bool GetEnableTalkSkip()
    {
        var ext = S.IPCProvider.GetActiveExternalControl();
        if (ext != null) return ext.Config.EnableTalkSkip ?? this.MainConfig.EnableTalkSkip;
        if (!(this.GlobalOverridesLocal && P.Enabled) && this.TerritoryConditions.TryGetValue(Svc.ClientState.TerritoryType, out var val))
        {
            return val.EnableTalkSkip;
        }
        return this.MainConfig.EnableTalkSkip;
    }
    public bool GetEnableRequestFill()
    {
        var ext = S.IPCProvider.GetActiveExternalControl();
        if (ext != null) return ext.Config.EnableRequestFill ?? this.MainConfig.EnableRequestFill;
        if (!(this.GlobalOverridesLocal && P.Enabled) && this.TerritoryConditions.TryGetValue(Svc.ClientState.TerritoryType, out var val))
        {
            return val.EnableRequestFill;
        }
        return this.MainConfig.EnableRequestFill;
    }
    public RequestFillQualityPreference GetRequestFillQualityPreference()
    {
        var ext = S.IPCProvider.GetActiveExternalControl();
        if (ext != null) return ext.Config.RequestFillQualityPreference ?? this.MainConfig.RequestFillQualityPreference;
        if (!(this.GlobalOverridesLocal && P.Enabled) && this.TerritoryConditions.TryGetValue(Svc.ClientState.TerritoryType, out var val))
        {
            return val.RequestFillQualityPreference;
        }
        return this.MainConfig.RequestFillQualityPreference;
    }


    public Vector4 GetQTAQuestColor()
    {
        if (!(this.GlobalOverridesLocal && P.Enabled) && this.TerritoryConditions.TryGetValue(Svc.ClientState.TerritoryType, out var val))
        {
            return val.QTIQuestColor;
        }
        return this.MainConfig.QTIQuestColor;
    }
    public bool GetQTAQuestTether()
    {
        if (!(this.GlobalOverridesLocal && P.Enabled) && this.TerritoryConditions.TryGetValue(Svc.ClientState.TerritoryType, out var val))
        {
            return val.QTIQuestTether;
        }
        return this.MainConfig.QTIQuestTether;
    }
    public float GetQTAQuestThickness()
    {
        if (!(this.GlobalOverridesLocal && P.Enabled) && this.TerritoryConditions.TryGetValue(Svc.ClientState.TerritoryType, out var val))
        {
            return val.QTIQuestThickness;
        }
        return this.MainConfig.QTIQuestThickness;
    }
    public bool GetQTAQuestHideWhenTargeted()
    {
        if (!(this.GlobalOverridesLocal && P.Enabled) && this.TerritoryConditions.TryGetValue(Svc.ClientState.TerritoryType, out var val))
        {
            return val.QTIQuestHideWhenTargeted;
        }
        return this.MainConfig.QTIQuestHideWhenTargeted;
    }
    public bool GetQTAQuestEnabled()
    {
        if (!(this.GlobalOverridesLocal && P.Enabled) && this.TerritoryConditions.TryGetValue(Svc.ClientState.TerritoryType, out var val))
        {
            return val.QTIQuestEnabled;
        }
        return this.MainConfig.QTIQuestEnabled;
    }
    public bool GetQTAlwaysEnabled()
    {
        if (!(this.GlobalOverridesLocal && P.Enabled) && this.TerritoryConditions.TryGetValue(Svc.ClientState.TerritoryType, out var val))
        {
            return val.QTIAlwaysEnabled;
        }
        return this.MainConfig.QTIAlwaysEnabled;
    }
    public bool GetEnableRewardPick()
    {
        var ext = S.IPCProvider.GetActiveExternalControl();
        if (ext != null) return ext.Config.EnableRewardPick ?? this.MainConfig.EnableRewardPick;
        if (!(this.GlobalOverridesLocal && P.Enabled) && this.TerritoryConditions.TryGetValue(Svc.ClientState.TerritoryType, out var val))
        {
            return val.EnableRewardPick;
        }
        return this.MainConfig.EnableRewardPick;
    }
    public bool GetEnableAutoInteract()
    {
        var ext = S.IPCProvider.GetActiveExternalControl();
        if (ext != null) return ext.Config.EnableAutoInteract ?? this.MainConfig.EnableAutoInteract;
        if (!(this.GlobalOverridesLocal && P.Enabled) && this.TerritoryConditions.TryGetValue(Svc.ClientState.TerritoryType, out var val))
        {
            return val.EnableAutoInteract;
        }
        return this.MainConfig.EnableAutoInteract;
    }
    public bool GetEnableUseEventItem()
    {
        var ext = S.IPCProvider.GetActiveExternalControl();
        if (ext != null) return ext.Config.EnableUseEventItem ?? this.MainConfig.EnableUseEventItem;
        if (!(this.GlobalOverridesLocal && P.Enabled) && this.TerritoryConditions.TryGetValue(Svc.ClientState.TerritoryType, out var val))
        {
            return val.EnableUseEventItem;
        }
        return this.MainConfig.EnableUseEventItem;
    }
}

public enum Button
{
    NONE, ALT, SHIFT, CTRL
}

[Serializable]
public class TerritoryConfig
{
    public bool EnableQuestAccept = true;
    public bool EnableQuestComplete = true;
    public bool EnableRewardPick = true;
    public bool EnableRequestHandin = true;
    public bool EnableCutsceneEsc = true;
    public bool EnableCutsceneSkipConfirm = true;
    public bool EnableTalkSkip = true;
    public bool EnableRequestFill = true;
    public RequestFillQualityPreference RequestFillQualityPreference = RequestFillQualityPreference.Any;
    public bool EnableAutoInteract = false;
    public bool EnableUseEventItem = false;

    public bool QTIQuestEnabled = true;
    public Vector4 QTIQuestColor = EColor.PurpleBright;
    public bool QTIQuestTether = true;
    public float QTIQuestThickness = 2f;
    public bool QTIQuestHideWhenTargeted = false;
    public bool QTIAlwaysEnabled = false;

    public bool IsEnabled()
    {
        return this.EnableQuestAccept || this.EnableQuestComplete || this.EnableRequestHandin || this.EnableCutsceneEsc || this.EnableCutsceneSkipConfirm || this.EnableTalkSkip || this.EnableRequestFill || this.EnableRewardPick || this.EnableAutoInteract || this.EnableUseEventItem;
    }
}

[Serializable]
public class ExternalTerritoryConfig
{
    public bool? EnableQuestAccept = null;
    public bool? EnableQuestComplete = null;
    public bool? EnableRewardPick = null;
    public bool? EnableRequestHandin = null;
    public bool? EnableCutsceneEsc = null;
    public bool? EnableCutsceneSkipConfirm = null;
    public bool? EnableTalkSkip = null;
    public bool? EnableRequestFill = null;
    public RequestFillQualityPreference? RequestFillQualityPreference = null;
    public bool? EnableAutoInteract = null;
    public bool? EnableUseEventItem = null;
}