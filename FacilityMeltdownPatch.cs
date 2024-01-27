using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using System.Reflection.Emit;
using BepInEx.Logging;
using System.Reflection;
using FacilityMeltdown;

namespace FacilityMeltdownPatch
{
    [BepInPlugin(GUID, NAME, VER)]
    [BepInDependency(MeltdownPlugin.modGUID)]
    public class FacilityMeltdownPatchPlugin : BaseUnityPlugin
    {
        public const string GUID = "xyz.poogle.facilitymeltdownpatch";
        public const string NAME = "Facility Meltdown Patch";
        public const string VER = "1.0.0";
        public readonly Harmony harmony = new Harmony(GUID);

        public ManualLogSource LogSrc;

        public static FacilityMeltdownPatchPlugin Instance;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            LogSrc = BepInEx.Logging.Logger.CreateLogSource(GUID);


            Patches.InitPatches();

            LogSrc.LogInfo("Initialized!");

        }
    }

    internal class Patches
    {
        public static int CurrentTime {
            get {
                return (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            }
        }

        internal static void InitPatches()
        {
            FacilityMeltdownPatchPlugin.Instance.harmony.PatchAll();
        }
    }
}
