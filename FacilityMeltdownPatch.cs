using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using BepInEx.Logging;
using System.Reflection;
using FacilityMeltdown;
using FacilityMeltdown.Effects;
using UnityEngine;
using System.Text.RegularExpressions;
using FacilityMeltdown.Util;
using FacilityMeltdown.Networking;

namespace FacilityMeltdownPatch
{
    [BepInPlugin(GUID, NAME, VER)]
    [BepInDependency(MeltdownPlugin.modGUID)]
    public class FacilityMeltdownPatchPlugin : BaseUnityPlugin
    {
        public const string GUID = "xyz.poogle.facilitymeltdownpatch";
        public const string NAME = "Facility Meltdown Patch";
        public const string VER = "1.0.1";
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
        internal static void InitPatches()
        {
            FacilityMeltdownPatchPlugin.Instance.harmony.PatchAll();
        }

        [HarmonyPatch(typeof(WarningAnnouncerEffect))]
        [HarmonyPatch("Setup")]
        public static class WarningAnnouncerEffect_Setup_Patch
        {
            public static void Postfix(WarningAnnouncerEffect __instance)
            {
                __instance.warningAudioSource.rolloffMode = AudioRolloffMode.Linear;
                __instance.warningAudioSource.maxDistance = 175f;
                __instance.warningAudioSource.minDistance = __instance.warningAudioSource.maxDistance - 1f;
                __instance.warningAudioSource.spatialBlend = 1f;
            }
        }


        [HarmonyPatch]
        public static class WarningAnnouncerEffect_Play_Patch
        {

            public static MethodBase TargetMethod()
            {
                return AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(WarningAnnouncerEffect), "Play"));
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
            {
                FacilityMeltdownPatchPlugin.Instance.LogSrc.LogInfo("Patching Play!");

                FieldInfo warningAudioSource = AccessTools.Field(typeof(WarningAnnouncerEffect), nameof(WarningAnnouncerEffect.warningAudioSource));

                MethodInfo AS_Play = AccessTools.Method(typeof(AudioSource), nameof(AudioSource.Play));
                MethodInfo PlayAtPos = AccessTools.Method(typeof(WarningAnnouncerEffect_Play_Patch), nameof(WarningAnnouncerEffect_Play_Patch.PlayAtPos));

                MethodInfo FindObjectsOfType = AccessTools.Method(typeof(UnityEngine.Object), nameof(UnityEngine.Object.FindObjectsOfType), null, new Type[] { typeof(EntranceTeleport) });
                MethodInfo ToList = AccessTools.Method(typeof(Enumerable), nameof(Enumerable.ToList), null, new Type[] { typeof(EntranceTeleport) });
                var code = new List<CodeInstruction>(instructions);

                int insertionIndex = -1;
                for (int i = 0; i < code.Count - 1; i++) // Count - 1 since we are checking i + 1
                {
                    // [i + 0] OpCode: ldfld    | Operand: UnityEngine.AudioSource warningAudioSource
                    // [i + 1] OpCode: callvirt | Operand: Void Play()
                    if (code[i].opcode == OpCodes.Ldfld && code[i].operand.Equals(warningAudioSource) && code[i + 1].Calls(AS_Play))
                    {
                        insertionIndex = i;
                        break;
                    }
                }

                if (insertionIndex == -1)
                {
                    FacilityMeltdownPatchPlugin.Instance.LogSrc.LogError("Could not find IL to transpile, bailing patch!");
                    return code;
                }
                
                code[insertionIndex] = new CodeInstruction(OpCodes.Nop); // nop the ldfld
                code[insertionIndex + 1] = new CodeInstruction(OpCodes.Nop); // nop the callvirt

                List<CodeInstruction> _il = new List<CodeInstruction>
                {
                    /*
                      OpCode: ldloc.1 | Operand:
                      OpCode: nop     | Operand:
                      OpCode: nop     | Operand:
                    */

                    new CodeInstruction(OpCodes.Call, FindObjectsOfType), // EntranceTeleport[]
                    new CodeInstruction(OpCodes.Call, ToList),            // EntranceTeleport[].ToList<EntranceTeleport>()
                    new CodeInstruction(OpCodes.Call, PlayAtPos)          // PlayAtPos(WarningAnnouncerEffect, List<EntranceTeleport>)
                };

                code.InsertRange(insertionIndex, _il);

                FacilityMeltdownPatchPlugin.Instance.LogSrc.LogInfo("Patched Play!");
                return code;
            }

            public static void PlayAtPos(WarningAnnouncerEffect instance, List<EntranceTeleport> AllEntrances)
            {
                AllEntrances.ForEach(entrance =>
                {
                    if (ReferenceEquals(instance, null) || ReferenceEquals(entrance, null)) {
                        FacilityMeltdownPatchPlugin.Instance.LogSrc.LogError("null check failed!");
                        return;
                    }

                    Vector3 PlayerPos = GameNetworkManager.Instance.localPlayerController.transform.position;

                    switch (GameNetworkManager.Instance.localPlayerController.isInsideFactory)
                    {
                        case true:
                            if(entrance.gotExitPoint)
                            {
                                Vector3 InsideDoorPos = entrance.exitPoint.transform.position;
                                instance.warningAudioSource.transform.position = InsideDoorPos;
                                instance.warningAudioSource.PlayOneShot(instance.warningAudioSource.clip, 1 - Vector3.Distance(InsideDoorPos, PlayerPos) / (instance.warningAudioSource.maxDistance * 1.5f));
                            }
                            else
                            {
                                entrance.FindExitPoint();
                            }
                            break;
                        case false:
                            Vector3 OutsideDoorPos = entrance.entrancePoint.transform.position;
                            instance.warningAudioSource.transform.position = OutsideDoorPos;
                            instance.warningAudioSource.PlayOneShot(instance.warningAudioSource.clip, 1 - Vector3.Distance(OutsideDoorPos, PlayerPos) / instance.warningAudioSource.maxDistance);
                            break;
                    }
                });
            }
        }
    }
}
