using System;
using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using IL.RoR2.Items;
using R2API;
using R2API.Utils;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace BetterBensRaincoat
{
    //This is an example plugin that can be put in BepInEx/plugins/BetterBensRaincoat/BetterBensRaincoat.dll to test out.
    //It's a small plugin that adds a relatively simple item to the game, and gives you that item whenever you press F2.

    //This attribute specifies that we have a dependency on R2API, as we're using it to add our item to the game.
    //You don't need this if you're not using R2API in your plugin, it's just to tell BepInEx to initialize R2API before this plugin so it's safe to use R2API.
    [BepInDependency(R2API.R2API.PluginGUID)]

    //This attribute is required, and lists metadata for your plugin.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]

    //We will be using 2 modules from R2API: ItemAPI to add our item and LanguageAPI to add our language tokens.
    [R2APISubmoduleDependency(nameof(ItemAPI), nameof(LanguageAPI), nameof(RecalculateStatsAPI))]

    //This is the main declaration of our plugin class. BepInEx searches for all classes inheriting from BaseUnityPlugin to initialize on startup.
    //BaseUnityPlugin itself inherits from MonoBehaviour, so you can use this as a reference for what you can declare and use in your plugin class: https://docs.unity3d.com/ScriptReference/MonoBehaviour.html
    public class BetterBensRaincoat : BaseUnityPlugin
    {
        //The Plugin GUID should be a unique ID for this plugin, which is human readable (as it is used in places like the config).
        //If we see this PluginGUID as it is on thunderstore, we will deprecate this mod. Change the PluginAuthor and the PluginName !
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "PhysicsFox";
        public const string PluginName = "BetterBensRaincoat";
        public const string PluginVersion = "1.1.1";

        private static ConfigFile config;
        private static ConfigEntry<float> configBarrierValue;
        private static ConfigEntry<bool> configVfx;
        
        //The Awake() method is run at the very start when the game is initialized.
        public void Awake()
        {
            //Init our logging class so that we can properly log for debugging
            Log.Init(Logger);
            config = base.Config;

            On.RoR2.Items.ImmuneToDebuffBehavior.OverrideDebuff_BuffDef_CharacterBody += (orig, buffDef, body) =>
            {
                if (!body.teamComponent || body.teamComponent.teamIndex != TeamIndex.Player)
                    return orig(buffDef, body);
                
                return buffDef.buffIndex != BuffIndex.None && buffDef.isDebuff && TryApplyOverride(body);
            };
            
            On.RoR2.Items.ImmuneToDebuffBehavior.OverrideDot += (orig, inflictDotInfo) =>
            {
                GameObject victimObject = inflictDotInfo.victimObject;
                CharacterBody body = (victimObject != null) ? victimObject.GetComponent<CharacterBody>() : null;
                
                if (body != null && (!body.teamComponent || body.teamComponent.teamIndex != TeamIndex.Player))
                    return orig(inflictDotInfo);
                
                return body && TryApplyOverride(body);
            };
            
            // Update description to match functionality
            var pickupToken = "ITEM_IMMUNETODEBUFF_PICKUP";
            var descToken = "ITEM_IMMUNETODEBUFF_DESC";
            LanguageAPI.Add(pickupToken,
                "Prevent debuffs, instead gaining a temporary barrier.");
            LanguageAPI.Add(descToken,
                "Whenever you would receive a debuff, instead receive <style=cIsUtility>10%</style> barrier <style=cStack>(+10% barrier per stack)</style>.");

            var section = "ItemAttributes";
            configBarrierValue = config.Bind(section, "BarrierAmount", 0.1f, 
                "Amount of barrier each stack of Ben's Raincoat gives per debuff");
            configVfx = config.Bind(section, "ShowVfx", true, 
                "Whether or not the raincoat effect should play. May want to hide VFX if photosensitive or just find it annoying");

            Log.LogInfo(nameof(Awake) + " done.");
        }

        private static bool TryApplyOverride(CharacterBody body)
        {
            RoR2.Items.ImmuneToDebuffBehavior component = body.GetComponent<RoR2.Items.ImmuneToDebuffBehavior>();
            if (component)
            {
                if (component.healthComponent)
                {
                    // Stacks 10% barrier per debuff taken per stack
                    component.healthComponent.AddBarrier(configBarrierValue.Value * component.stack * component.healthComponent.fullCombinedHealth);
                    
                    // Play VFX?
                    if (configVfx.Value)
                        EffectManager.SimpleImpactEffect(Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/ImmuneToDebuff/ImmuneToDebuffEffect.prefab").WaitForCompletion(), body.corePosition, Vector3.up, true);
                    
                    return true;
                }
            }
            return false;
        }

        private void RefreshBuffCount(CharacterBody body)
        {
            var coatCount = body.inventory.GetItemCount(RoR2.Items.ImmuneToDebuffBehavior.GetItemDef());
            body.SetBuffCount(DLC1Content.Buffs.ImmuneToDebuffReady.buffIndex, coatCount);
        }
        
        public void FixCounts()
        {
            // Fix Ben's Raincoat stack not updating in the buff bar
            var instances = PlayerCharacterMasterController.instances;
            if (instances == null)
                return;
        
            foreach (PlayerCharacterMasterController playerCharacterMaster in instances)
            {
                var body = playerCharacterMaster.master.GetBody();
                if (body == null)
                    continue;
                RefreshBuffCount(body);
            }
        }

        public void FixedUpdate()
        {
            FixCounts();
        }
    }
}
