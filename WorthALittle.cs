using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace WorthALittle;

/// <summary>
/// alright so basically first it initializes the settings, logger, sets the instance, harmony, patches everything and declares itself loaded.
/// then the patches add a "WorthALittleMore" wrapper component to any object with a PhysGrabHinge which gets used when the hinge breaks.
/// when MakeValuable is called on the worthalittlemore component it handles the rpc and makes the hinge object valuable on all clients.
/// then there's also another patch which is a postfix on valuable awake, the original script adds a Halloween contact in there so i run after it so that i can disable it.
/// the disabling i took after the moneybag which automatically disables their contacts and makes it uncountable in halloween stacking
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class WorthALittle : BaseUnityPlugin {
    public readonly ConfigEntry<float> Multiplier;
    public readonly ConfigEntry<bool> DoRange;
    public readonly ConfigEntry<bool> Stackable;
    public readonly ConfigEntry<int> MinValue;
    public readonly ConfigEntry<int> MaxValue;
    
    public WorthALittle() { /* bind settings */
        Multiplier = Config.Bind("General",
            "Value multiplier", 
            1f,
            "Feel free to make it worthalot!!");
        
        Stackable = Config.Bind("General",
            "Halloween stackable",
            false,
            "Determines if the valuables count in stacking");
        
        DoRange = Config.Bind("Bounds",
            "Enable clamp between min and max",
            true,
            "Determines if the values below are actually used");
        
        MinValue = Config.Bind("Bounds", 
            "Minimum valuable value", 
            100, 
            "The minimum cost a valuable *something* can generate at.");
        
        MaxValue = Config.Bind(
            "Bounds", 
            "Maximum valuable value", 
            5000, 
            "The maximum cost a valuable *something* can generate at.");

    }
    
    internal static WorthALittle Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger => Instance._logger;
    private ManualLogSource _logger => base.Logger;
    internal Harmony? Harmony { get; set; }
    
    private void Awake() { // set stuff up
        Instance = this;
        Harmony ??= new Harmony($"at.kibbyc.{MyPluginInfo.PLUGIN_GUID}");
        Harmony.PatchAll(typeof(WorthALittle));
        Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} has loaded!");
    }
    
    [HarmonyPatch(typeof(PhysGrabHinge), nameof(PhysGrabHinge.Awake)), HarmonyPostfix]
    public static void EntryPointMaybe(PhysGrabHinge __instance) => __instance.gameObject.AddComponent<WorthALittleMore>();

    [HarmonyPatch(typeof(PhysGrabHinge), nameof(PhysGrabHinge.OnJointBreak)), HarmonyPostfix]
    public static void Break(PhysGrabHinge __instance) => __instance.GetComponent<WorthALittleMore>().MakeValuable();
    
    [HarmonyPatch(typeof(ValuableObject), nameof(ValuableObject.Awake)), HarmonyPostfix]
    public static void MaybeDisableHalloweenStacking(ValuableObject __instance) {
        if (Instance.Stackable.Value) return; // if stacking is allowed dont disable stacking
        if (!__instance.gameObject.TryGetComponent(out WorthALittleMore obj)) return; // return if the object isnt one that we added a custom component to
        if (!__instance.TryGetComponent(out ExtractionHalloweenContact a)) return; // return if there's no halloween contacts
        a.enabled = false;
    }
    
    /// <summary>
    ///  the goal of this hook was pretty much "make the scripts and values look like a valuable object and not a notvaluableobject"
    ///  to its credit it does exactly that
    /// </summary>
    public static void SetBrokenHingeValuable(PhysGrabHinge? hinge) {
        
        if (!hinge) {
            Logger.LogError("hinge object is null, cant continue.");
            return;
        }
        GameObject worthALittle = hinge.gameObject;
        Logger.LogInfo($"Object \"{worthALittle.name}\" has been broken off its hinge, making valuable!");
        
        // get variables
        if (!worthALittle.TryGetComponent(out PhysGrabObject physGrab)) {
            Logger.LogError("physGrab is null, cant continue.");
            return;
        }
        if (!worthALittle.TryGetComponent(out NotValuableObject notValuable)) {
            Logger.LogError("NotValuable is null, cant continue.");
            return;
        }
        if (!worthALittle.TryGetComponent(out PhysGrabObjectImpactDetector impactDetector)) {
            Logger.LogError("ImpactDetector is null, cant continue.");
            return;
        }
        
        DisableHingeAudioSource(hinge);
        SetPhysGrabValues(physGrab);
        var valuableALittle = SetValuableObject(worthALittle, physGrab, notValuable);
        SetImpactDetectorValues(impactDetector, valuableALittle);
        
        // clean up
        Destroy(worthALittle.GetComponent<HingeJoint>()); // destroy hingeobject if possible
        Destroy(hinge); // destroy phys grab hinge
        Destroy(notValuable);
    }

    private static void DisableHingeAudioSource(PhysGrabHinge instance) {
        if (!instance.audioSource) {
            Logger.LogInfo("The hinge object doesnt have a audiosource to disable");
            return;    
        }
        
        if (instance.audioSource.gameObject.TryGetComponent(out AudioLowPassLogic a)) a.enabled = false;
        if (a && a.AudioLowpassFilter) a.AudioLowpassFilter.enabled = false;
        instance.audioSource.enabled = false;
    }
    
    private static void SetPhysGrabValues(PhysGrabObject physGrab) {
        physGrab.isNonValuable = false;
        physGrab.isValuable = true;
        
        // disable hinge
        physGrab.hinge = null;
        physGrab.hasHinge = false;
        
        physGrab.hasNeverBeenGrabbed = true; // reset default invulnerability
    }

    private static ValuableObject SetValuableObject(GameObject worthALittle, PhysGrabObject physGrab, NotValuableObject notValuable) {
        ValuableObject valuableALittle = worthALittle.AddComponent<ValuableObject>();
        
        // override the value
        float costAmount = physGrab.massOriginal*physGrab.massOriginal * Instance.Multiplier.Value;
        costAmount = (int)Mathf.Round(costAmount) * 100; // round to nearest hundred
        if (Instance.DoRange.Value) 
            costAmount = Mathf.Clamp(costAmount, Instance.MinValue.Value, Instance.MaxValue.Value);
        valuableALittle.dollarValueOverride = (int)costAmount;
        
        // TODO invalidate the new valuable from being used for item stacking cs it'd probably be too op
        // maybe i should make that a config option
        
        // the notvaluable object comes with a handful of preset values that the valuable object usually spawns with as well so i set them here
        // they dont actually change like anything because most of these arent used in the script itself but in other scripts (namely PhysGrabObjectImpactDetector)
        // which have already been initialized and had their values set, i just set these values for parity / compatibility
        // tbh it might be worth it to re-initialize the other scripts as well
        // valuable preset doesnt need to be set because we override it above
        valuableALittle.durabilityPreset = notValuable.durabilityPreset;
        valuableALittle.particleColors = notValuable.particleColors;
        valuableALittle.audioPreset = notValuable.audioPreset;
        valuableALittle.audioPresetPitch = notValuable.audioPresetPitch;
        valuableALittle.physAttributePreset = notValuable.physAttributePreset; // does smth
        return valuableALittle;
    }
    
    private static void SetImpactDetectorValues(PhysGrabObjectImpactDetector impactDetector, ValuableObject valuableALittle) {
        // disable hinge here too
        impactDetector.isHinge = false;
        impactDetector.isBrokenHinge = false;
        
        impactDetector.isValuable = true; // make valuable
        impactDetector.valuableObject = valuableALittle; // set valuable reference
    }
}