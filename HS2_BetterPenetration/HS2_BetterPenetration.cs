﻿using BepInEx;
using BepInEx.Configuration;
using BepInEx.Bootstrap;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Reflection;
using AIChara;
using Core_BetterPenetration;
using System.Linq;

namespace HS2_BetterPenetration
{
    [BepInPlugin("animal42069.HS2betterpenetration", "HS2 Better Penetration", VERSION)]
    [BepInDependency("com.deathweasel.bepinex.uncensorselector", "3.10")]
    [BepInDependency("com.joan6694.illusionplugins.bonesframework", "1.4.1")]
    [BepInProcess("HoneySelect2")]
    [BepInProcess("HoneySelect2VR")]
    public class HS2_BetterPenetration : BaseUnityPlugin
    {
        public const string VERSION = "3.0.0.0";
        private const int MaleLimit = 2;
        private const int FemaleLimit = 2;
        private const bool _useSelfColliders = true;

        private static readonly List<float> frontOffsets = new List<float> { -0.35f, 0.25f, 0f, -0.65f };
        private static readonly List<float> backOffsets = new List<float> { -0.05f, 0.25f, 0.05f, 0.05f };
        private static readonly List<bool> frontPointsInward = new List<bool> { false, false, false, false };
        private static readonly List<bool> backPointsInward = new List<bool> { false, false, true, true };

        private static readonly ConfigEntry<float>[] _danSoftness = new ConfigEntry<float>[MaleLimit];
        private static readonly ConfigEntry<float>[] _danColliderHeadLength = new ConfigEntry<float>[MaleLimit];
        private static readonly ConfigEntry<float>[] _danColliderRadius = new ConfigEntry<float>[MaleLimit];
        private static readonly ConfigEntry<float>[] _danColliderVerticalCenter = new ConfigEntry<float>[MaleLimit];
        private static readonly ConfigEntry<float>[] _fingerColliderLength = new ConfigEntry<float>[MaleLimit];
        private static readonly ConfigEntry<float>[] _fingerColliderRadius = new ConfigEntry<float>[MaleLimit];
        private static readonly ConfigEntry<float>[] _telescopeThreshold = new ConfigEntry<float>[MaleLimit];
        private static readonly ConfigEntry<bool>[] _forceTelescope = new ConfigEntry<bool>[MaleLimit];
        private static readonly ConfigEntry<bool>[] _useFingerColliders = new ConfigEntry<bool>[MaleLimit];

        private static ConfigEntry<float> _clippingDepth;
        private static ConfigEntry<float> _kokanOffsetForward;
        private static ConfigEntry<float> _kokanOffsetUp;
        private static ConfigEntry<float> _headOffsetForward;
        private static ConfigEntry<float> _headOffsetUp;
        private static ConfigEntry<bool> _useKokanFix;
        private static ConfigEntry<float> _kokanFixPositionY;
        private static ConfigEntry<float> _kokanFixPositionZ;
        private static ConfigEntry<float> _kokanFixRotationX;
        private static readonly ConfigEntry<float>[] _frontCollisionOffset = new ConfigEntry<float>[frontOffsets.Count];
        private static readonly ConfigEntry<float>[] _backCollisionOffset = new ConfigEntry<float>[backOffsets.Count];

        private static Harmony harmony;
        private static HScene hScene;
        private static bool patched = false;
        private static bool inHScene = false;
        private static bool loadingCharacter = false;
        private static bool twoDans;

        private void Awake()
        {
            for (int maleNum = 0; maleNum < _danColliderHeadLength.Length; maleNum++)
            {
                (_fingerColliderLength[maleNum] = Config.Bind("Male " + (maleNum + 1) + " Options", "Finger Collider: Length", 0.6f, "Lenght of the finger colliders.")).SettingChanged += (s, e) =>
                { UpdateFingerColliders(); };
                (_fingerColliderRadius[maleNum] = Config.Bind("Male " + (maleNum + 1) + " Options", "Finger Collider: Radius", 0.2f, "Radius of the finger colliders.")).SettingChanged += (s, e) =>
                { UpdateFingerColliders(); };
                (_danColliderHeadLength[maleNum] = Config.Bind("Male " + (maleNum + 1) + " Options", "Penis Collider: Length of Head", 0.35f, "Distance from the center of the head bone to the tip, used for collision purposes.")).SettingChanged += (s, e) =>
                { UpdateDanColliders(); };
                (_danColliderRadius[maleNum] = Config.Bind("Male " + (maleNum + 1) + " Options", "Penis Collider: Radius of Shaft", 0.32f, "Radius of the shaft collider.")).SettingChanged += (s, e) =>
                { UpdateDanColliders(); };
                (_danColliderVerticalCenter[maleNum] = Config.Bind("Male " + (maleNum + 1) + " Options", "Penis Collider: Vertical Center", -0.03f, "Vertical Center of the shaft collider")).SettingChanged += (s, e) =>
                { UpdateDanColliders(); };
                (_danSoftness[maleNum] = Config.Bind("Male " + (maleNum + 1) + " Options", "Penis: Softness", 0.15f, "Set the softness of the penis.  A value of 0 means maximum hardness, the penis will remain the same length at all times.  A value greater than 0 will cause the penis to begin to telescope after penetration.  A small value can make it appear there is friction during penetration.")).SettingChanged += (s, e) =>
                { UpdateDanOptions(); };
                (_telescopeThreshold[maleNum] = Config.Bind("Male " + (maleNum + 1) + " Options", "Limiter: Telescope Threshold", 0.6f, "Allow the penis to begin telescoping after it has penetrated a certain amount. 0 = never telescope, 0.5 = allow telescoping after the halfway point, 1 = always allow telescoping.")).SettingChanged += (s, e) =>
                { UpdateDanOptions(); };
                (_forceTelescope[maleNum] = Config.Bind("Male " + (maleNum + 1) + " Options", "Limiter: Telescope Always", true, "Force the penis to always telescope at the threshold point, instead of only doing it when it prevents clipping.")).SettingChanged += (s, e) =>
                { UpdateDanOptions(); };
                (_useFingerColliders[maleNum] = Config.Bind("Male " + (maleNum + 1) + " Options", "Finger Collider: Enable", true, "Use finger colliders")).SettingChanged += (s, e) =>
                { UpdateDanOptions(); };
            }

            (_clippingDepth = Config.Bind("Female Options", "Clipping Depth", 0.25f, "Set how close to body surface to limit penis for clipping purposes. Smaller values will result in more clipping through the body, larger values will make the shaft wander further away from the intended penetration point.")).SettingChanged += (s, e) =>
            { UpdateCollisionOptions(); };
            for (int offset = 0; offset < frontOffsets.Count; offset++)
                (_frontCollisionOffset[offset] = Config.Bind("Female Options", "Clipping Offset: Front Collision " + offset, frontOffsets[offset], "Individual offset on colision point, to improve clipping")).SettingChanged += (s, e) =>
                { UpdateCollisionOptions(); };
            for (int offset = 0; offset < backOffsets.Count; offset++)
                (_backCollisionOffset[offset] = Config.Bind("Female Options", "Clipping Offset: Back Collision " + offset, backOffsets[offset], "Individual offset on colision point, to improve clipping")).SettingChanged += (s, e) =>
                { UpdateCollisionOptions(); };
            (_kokanOffsetForward = Config.Bind("Female Options", "Target Offset: Vagina Vertical", -0.15f, "Vertical offset of the vagina target")).SettingChanged += (s, e) =>
            { UpdateCollisionOptions(); };
            (_kokanOffsetUp = Config.Bind("Female Options", "Target Offset: Vagina Depth", 0.0f, "Depth offset of the vagina target")).SettingChanged += (s, e) =>
            { UpdateCollisionOptions(); };
            (_headOffsetForward = Config.Bind("Female Options", "Target Offset: Mouth Depth", 0.0f, "Depth offset of the mouth target")).SettingChanged += (s, e) =>
            { UpdateCollisionOptions(); };
            (_headOffsetUp = Config.Bind("Female Options", "Target Offset: Mouth Vertical", 0.03f, "Vertical offset of the mouth target")).SettingChanged += (s, e) =>
            { UpdateCollisionOptions(); };
            (_useKokanFix = Config.Bind("Female Options", "Joint Adjustment: Missionary Correction", false, "NOTE: There is an Illusion bug that causes the vagina to appear sunken in certain missionary positions.  It is best to use Advanced Bonemod and adjust your female character's cf_J_Kokan Offset Y to 0.001.  If you don't do that, enabling this option will attempt to fix the problem by guessing where the bone should be")).SettingChanged += (s, e) =>
            { UpdateCollisionOptions(); };
            (_kokanFixPositionY = Config.Bind("Female Options", "Joint Adjustment: Missionary Position Y", -0.075f, "Amount to adjust the Vagina bone position Y for certain Missionary positions to correct its appearance")).SettingChanged += (s, e) =>
            { UpdateCollisionOptions(); };
            (_kokanFixPositionZ = Config.Bind("Female Options", "Joint Adjustment: Missionary Position Z", 0.0625f, "Amount to adjust the Vagina bone position Z for certain Missionary positions to correct its appearance")).SettingChanged += (s, e) =>
            { UpdateCollisionOptions(); };
            (_kokanFixRotationX = Config.Bind("Female Options", "Joint Adjustment: Missionary Rotation X", 10.0f, "Amount to adjust the Vagina bone rotation X for certain Missionary positions to correct its appearance")).SettingChanged += (s, e) =>
            { UpdateCollisionOptions(); };

            harmony = new Harmony("HS2_BetterPenetration");
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
            SceneManager.sceneUnloaded += SceneManager_sceneUnloaded;
        }

        private static void UpdateDanColliders()
        {
            if (!inHScene)
                return;

            for (int index = 0; index < MaleLimit; index++)
                Core.UpdateDanCollider(index, _danColliderRadius[index].Value, _danColliderHeadLength[index].Value, _danColliderVerticalCenter[index].Value);
        }

        private static void UpdateFingerColliders()
        {
            if (!inHScene)
                return;

            for (int index = 0; index < MaleLimit; index++)
                Core.UpdateFingerColliders(index, _fingerColliderRadius[index].Value, _fingerColliderLength[index].Value);
        }

        private static void UpdateDanOptions()
        {
            if (!inHScene)
                return;

            for (int index = 0; index < MaleLimit; index++)
                Core.UpdateDanOptions(index, _danSoftness[index].Value, _telescopeThreshold[index].Value, _forceTelescope[index].Value, _useFingerColliders[index].Value);
        }

        private static void UpdateCollisionOptions()
        {
            if (!inHScene)
                return;

            List<CollisionOptions> collisionOptions = PopulateCollisionOptionsList();
            for (int index = 0; index < MaleLimit; index++)
                Core.UpdateCollisionOptions(index, collisionOptions[index]);
        }

        public static void BeforeCharacterReload()
        { 
            if (!inHScene)
                return;

            loadingCharacter = true;
            Core.SetChangingAnimations(true);
        }

        public static void AfterCharacterReload()
        {
            if (!inHScene || hScene == null)
                return;

            ChaControl[] femaleArray = hScene.GetFemales();
            List<ChaControl> femaleList = new List<ChaControl>();

            foreach (var character in femaleArray)
            {
                if (character == null)
                    continue;
                femaleList.Add(character);
            }

            List<CollisionOptions> collisionOptions = PopulateCollisionOptionsList();
            Core.InitializeCollisionAgents(femaleList, collisionOptions);
            loadingCharacter = false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ChaControl), "LoadCharaFbxDataAsync")]
        public static void ChaControl_LoadCharaFbxDataAsync(ChaControl __instance)
        {
            Core.RemovePCollidersFromCoordinate(__instance);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(HScene), "SetStartVoice")]
        public static void HScene_PostSetStartVoice(HScene __instance)
        {
            List<DanOptions> danOptions = PopulateDanOptionsList();
            List<CollisionOptions> collisionOptions = PopulateCollisionOptionsList();

            hScene = __instance;

            ChaControl[] femaleArray = hScene.GetFemales();
            List<ChaControl> femaleList = new List<ChaControl>();
            foreach (var character in femaleArray)
            {
                if (character == null)
                    continue;
                femaleList.Add(character);
            }

            ChaControl[] maleArray = hScene.GetMales();
            List<ChaControl> maleList = new List<ChaControl>();
            foreach (var character in maleArray)
            {
                if (character == null)
                    continue;
                maleList.Add(character);
            }

            Core.InitializeAgents(maleList, femaleList, danOptions, collisionOptions);
            inHScene = true;
        }

        private static List<DanOptions> PopulateDanOptionsList()
        {
            List<DanOptions> danOptions = new List<DanOptions>();

            for (int maleNum = 0; maleNum < MaleLimit; maleNum++)
            {
                danOptions.Add(new DanOptions(_danColliderVerticalCenter[maleNum].Value, _danColliderRadius[maleNum].Value, _danColliderHeadLength[maleNum].Value,
                    _danSoftness[maleNum].Value, _telescopeThreshold[maleNum].Value, _forceTelescope[maleNum].Value,
                    _fingerColliderRadius[maleNum].Value, _fingerColliderLength[maleNum].Value, _useFingerColliders[maleNum].Value));
            }

            return danOptions;
        }

        private static List<CollisionOptions> PopulateCollisionOptionsList()
        {
            List<CollisionOptions> collisionOptions = new List<CollisionOptions>();

            List<CollidonPointInfo> frontInfo = new List<CollidonPointInfo>();
            for (int info = 0; info < BoneNames.frontCollisionList.Count; info++)
                frontInfo.Add(new CollidonPointInfo(BoneNames.frontCollisionList[info], _frontCollisionOffset[info].Value, frontPointsInward[info]));

            List<CollidonPointInfo> backInfo = new List<CollidonPointInfo>();
            for (int info = 0; info < BoneNames.backCollisionList.Count; info++)
                backInfo.Add(new CollidonPointInfo(BoneNames.backCollisionList[info], _backCollisionOffset[info].Value, backPointsInward[info]));

            for (int femaleNum = 0; femaleNum < FemaleLimit; femaleNum++)
            {
                collisionOptions.Add(new CollisionOptions(_useSelfColliders, _kokanOffsetForward.Value, _kokanOffsetUp.Value, _headOffsetForward.Value, _headOffsetUp.Value, _useKokanFix.Value,
                    _kokanFixPositionZ.Value, _kokanFixPositionY.Value, _kokanFixRotationX.Value, _clippingDepth.Value, frontInfo, backInfo));
            }

            return collisionOptions;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(HScene), "ChangeAnimation")]
        private static void HScene_PreChangeAnimation(HScene.AnimationListInfo _info)
        {
            if (!inHScene || _info == null || _info.fileFemale == null)
                return;

            Core.OnChangeAnimation(_info.fileFemale);
        }
		
        [HarmonyPostfix, HarmonyPatch(typeof(H_Lookat_dan), "setInfo")]
        private static void H_Lookat_dan_PostSetInfo(H_Lookat_dan __instance, System.Text.StringBuilder ___assetName, ChaControl ___male)
        {
            if (!inHScene || loadingCharacter || __instance == null || __instance.strPlayMotion == null)
                return;

            int maleNum = 0;
            if (___male != null && ___male.chaID != 99)
                maleNum = 1;

            twoDans = false;
            if (___assetName != null && ___assetName.Length != 0 && ___assetName.ToString().Contains("m2f"))
                twoDans = true;

            Core.LookAtDanSetup(__instance.transLookAtNull, __instance.strPlayMotion, __instance.bTopStick, maleNum, __instance.numFemale, twoDans);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(H_Lookat_dan), "LateUpdate")]
        public static void H_Lookat_dan_PostLateUpdate(H_Lookat_dan __instance, ChaControl ___male)
        {
            if (!inHScene || loadingCharacter || __instance == null || __instance.strPlayMotion == null || ___male == null)
                return;

            int maleNum = 0;

            if (___male.chaID != 99)
            {
                if (!twoDans)
                    return;
                maleNum = 1;
            }

            Core.LookAtDanUpdate(__instance.transLookAtNull, __instance.strPlayMotion, __instance.bTopStick, hScene.NowChangeAnim, maleNum, __instance.numFemale);
        }

        private static void SceneManager_sceneLoaded(Scene scene, LoadSceneMode lsm)
        {
            if (lsm != LoadSceneMode.Single || patched || scene.name != "HScene")
                return;

            harmony.PatchAll(typeof(HS2_BetterPenetration));
            patched = true;

            Console.WriteLine("HS2_BetterPenetration: Searching for Uncensor Selector");
            Chainloader.PluginInfos.TryGetValue("com.deathweasel.bepinex.uncensorselector", out PluginInfo pluginInfo);
            if (pluginInfo != null && pluginInfo.Instance != null)
            {
                Type nestedType = pluginInfo.Instance.GetType().GetNestedType("UncensorSelectorController", AccessTools.all);
                if (nestedType != null)
                {
                    Console.WriteLine("HS2_BetterPenetration: UncensorSelector found, trying to patch");
                    MethodInfo methodInfo = AccessTools.Method(nestedType, "ReloadCharacterBody", null, null);
                    if (methodInfo != null)
                    {
                        harmony.Patch(methodInfo, new HarmonyMethod(typeof(HS2_BetterPenetration), "BeforeCharacterReload"), new HarmonyMethod(typeof(HS2_BetterPenetration), "AfterCharacterReload"), null, null);
                        Console.WriteLine("HS2_BetterPenetration: UncensorSelector patched correctly");
                    }
                }
            }
        }

        private static void SceneManager_sceneUnloaded(Scene scene)
        {
            if (!patched || scene.name != "HScene")
                return;
                
            Core.OnEndScene();

            harmony.UnpatchAll(nameof(HS2_BetterPenetration));
            patched = false;
            inHScene = false;
            loadingCharacter = false;

            if (hScene == null)
                return;

            foreach (var lookat in hScene.ctrlLookAts)
            {
                if (lookat == null)
                    continue;

                lookat.transLookAtNull = null;
            }

            hScene = null;
        }
    }
}