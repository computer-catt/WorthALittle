using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace WorthALittle;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class WorthALittle : BaseUnityPlugin {
    private readonly ConfigEntry<float> _multiplier;
    private readonly ConfigEntry<int> _minValue;
    private readonly ConfigEntry<int> _maxValue;
    
    public WorthALittle() { /* bind settings */
        _multiplier = Config.Bind("General",
            "Value multiplier", 
            1f,
            "Feel free to make it worthalot!!");

        _minValue = Config.Bind("General", 
            "Minimum valuable value", 
            100, 
            "The minimum cost a valuable *something* can generate at.");
        
        _maxValue = Config.Bind(
            "General", 
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
    
    /// <summary>
    ///  the goal of this hook was pretty much "make the scripts and values look like a valuable object and not a notvaluableobject"
    ///  to its credit it does exactly that
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabHinge), nameof(PhysGrabHinge.OnJointBreak)), HarmonyPostfix]
    public static void SetBrokenHingeValuable(PhysGrabHinge __instance) { // TODO make this work in multiplayer
        Logger.LogInfo($"Object \"{__instance.gameObject.name}\" has been broken off its hinge, making valuable!");
        GameObject worthALittle = __instance.gameObject;
        
        Destroy(__instance); // destroy phys grab hinge
        
        // disable hinge audiosource stuff
        if (__instance.audioSource.gameObject.TryGetComponent(out AudioLowPassLogic a)) a.AudioLowpassFilter.enabled = false;
        if (a && a.AudioLowpassFilter) a.AudioLowpassFilter.enabled = false;
        __instance.audioSource.enabled = false;
        
        
        if (worthALittle.TryGetComponent(out HingeJoint b)) Destroy(b); // destroy hingeobject if possible
        
        var notValuable = worthALittle.GetComponent<NotValuableObject>();
        Destroy(notValuable);
        
        PhysGrabObject physgrab = worthALittle.GetComponent<PhysGrabObject>();
        physgrab.isNonValuable = false;
        physgrab.isValuable = true;
        
        // disable hinge
        physgrab.hinge = null;
        physgrab.hasHinge = false;
        
        physgrab.hasNeverBeenGrabbed = true; // reset default invulnerability
        
        ValuableObject valuableALittle = worthALittle.AddComponent<ValuableObject>();
        
        // override the value
        float costAmount = physgrab.massOriginal*physgrab.massOriginal * Instance._multiplier.Value;
        int roundedCost = (int)Mathf.Round(costAmount) * 100; // round to nearest hundred
        valuableALittle.dollarValueOverride = Mathf.Clamp(roundedCost, Instance._minValue.Value, Instance._maxValue.Value); 
        
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
        
        PhysGrabObjectImpactDetector impactDetector = worthALittle.GetComponent<PhysGrabObjectImpactDetector>();
        
        // disable hinge here too
        impactDetector.isHinge = false;
        impactDetector.isBrokenHinge = false;
        
        impactDetector.isValuable = true; // make valuable
        impactDetector.valuableObject = valuableALittle; // set valuable reference
    }
}