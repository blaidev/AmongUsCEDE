// AmongUsCEDE.Core.Patches.VictoryPatch
using AmongUsCEDE;
using HarmonyLib;

[HarmonyPatch(typeof(ShipStatus))]
[HarmonyPatch("CheckEndCriteria")]
class VictoryPatch
{
	private static bool Prefix(ShipStatus __instance)
	{
		if (!GameData.Instance)
		{
			return false;
		}
		bool IsSabotageEnd = false;
		ScriptManager.RunCurrentGMFunction("CheckEndCriteria", false, GameData.Instance.TotalTasks <= GameData.Instance.CompletedTasks, IsSabotageEnd);
		return false;
	}
}


[HarmonyPatch(typeof(ShipStatus))]
[HarmonyPatch("IsGameOverDueToDeath")]
class PreventHangsAndStuffDueToDeathGameOver
{
	private static bool Prefix(bool __result)
	{
		__result = false;
		return false;
	}
}
