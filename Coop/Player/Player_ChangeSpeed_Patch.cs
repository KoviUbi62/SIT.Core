﻿using EFT.InventoryLogic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SIT.Coop.Core.Web;
using SIT.Tarkov.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SIT.Core.Coop.Player
{
    public class Player_ChangeSpeed_Patch : ModuleReplicationPatch
    {
        public override Type InstanceType => typeof(EFT.Player);
        public override string MethodName => "ChangeSpeed";
        public override bool DisablePatch => true;

        protected override MethodBase GetTargetMethod()
        {
            var method = PatchConstants.GetMethodForType(InstanceType, MethodName);
            //Logger.LogInfo($"Player_ChangeSpeed_Patch:{InstanceType.Name}:{method.Name}");

            return method;
        }

        public static Dictionary<string, bool> CallLocally
            = new Dictionary<string, bool>();

        private static List<long> ProcessedCalls
            = new List<long>();

        private static Dictionary<string, float> LastSpeedDelta
           = new Dictionary<string, float>();

        [PatchPrefix]
        public static bool PrePatch(EFT.Player __instance)
        {
            var result = false;
            if (CallLocally.TryGetValue(__instance.Profile.AccountId, out var expecting) && expecting)
                result = true;

            return result;
        }

        [PatchPostfix]
        public static void PostPatch(
           EFT.Player __instance,
           float speedDelta
            )
        {
            var player = __instance;

            if (LastSpeedDelta.ContainsKey(player.Profile.AccountId))
            {
                if (Math.Round((double)LastSpeedDelta[player.Profile.AccountId], 2) == Math.Round((double)speedDelta, 2))
                    return;
            }


            if (CallLocally.TryGetValue(player.Profile.AccountId, out var expecting) && expecting)
            {
                CallLocally.Remove(player.Profile.AccountId);
                return;
            }

            Dictionary<string, object> dictionary = new Dictionary<string, object>();
            dictionary.Add("t", DateTime.Now.Ticks);
            dictionary.Add("d", speedDelta.ToString());
            dictionary.Add("m", "ChangeSpeed");
            ServerCommunication.PostLocalPlayerData(player, dictionary);

            if (!LastSpeedDelta.ContainsKey(player.Profile.AccountId))
                LastSpeedDelta.Add(player.Profile.AccountId, speedDelta);

            LastSpeedDelta[player.Profile.AccountId] = speedDelta;
        }


        public override void Replicated(EFT.Player player, Dictionary<string, object> dict)
        {
            var timestamp = long.Parse(dict["t"].ToString());

            if (!ProcessedCalls.Contains(timestamp))
                ProcessedCalls.Add(timestamp);
            else
            {
                ProcessedCalls.RemoveAll(x => x <= DateTime.Now.AddHours(-1).Ticks);
                return;
            }
            
            try
            {
                var speedDelta = float.Parse(dict["d"].ToString());
                CallLocally.Add(player.Profile.AccountId, true);
                player.ChangeSpeed(speedDelta);
            }
            catch (Exception e)
            {
                Logger.LogInfo(e);
            }
        }
    }
}
