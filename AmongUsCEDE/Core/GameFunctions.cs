﻿using System;
using System.Collections.Generic;
using System.Text;
using AmongUsCEDE.Core.Extensions;
using AmongUsCEDE.LuaData;
using UnityEngine;
using System.Linq;
using Hazel;
using AmongUsCEDE.Lua;
using MoonSharp.Interpreter;
using BepInEx.IL2CPP;
using AmongUsCEDE.Extensions;

namespace AmongUsCEDE.Core
{


	public enum CustomRPCs
	{
		CheckWithHost = 49,
		SetUserData = 50
	}
	public enum HostRequestType
	{
		UsePrimary,
		Teleport,
		ChangeHat,
		ChangeSkin,
		ChangeColor,
		ChangePet,
		Custom
	}

	public static class GameFunctions
	{
		public static void RpcSetRoles(PlayerControl self, GameData.PlayerInfo[] players, string[] roles, bool intro = true)
		{
			byte[] array = (from p in players
							select p.PlayerId).ToArray<byte>();
			byte[] rolesbytes = new byte[roles.Length];
			for (int i = 0; i < roles.Length; i++)
			{
				Role role = ScriptManager.CurrentGamemode.Roles.Find(a => a.Internal_Name == roles[i]);
				if (role != null)
				{
					rolesbytes[i] = (byte)(ScriptManager.CurrentGamemode.Roles.IndexOf(role) + 1);
				}
				else
				{
					rolesbytes[i] = 0;
				}
			}
			if (AmongUsClient.Instance.AmClient)
			{
				SetRoles(self, array, rolesbytes, intro);
			}
			MessageWriter messageWriter = AmongUsClient.Instance.StartRpc(self.NetId, 3, SendOption.Reliable);
			messageWriter.Write(intro);
			messageWriter.WriteBytesAndSize(array);
			messageWriter.WriteBytesAndSize(rolesbytes);
			messageWriter.EndMessage();
		}

		public static void SendRequestToHost(PlayerControl instance, HostRequestType type, params object[] data)
		{
			if (!AmongUsClient.Instance.AmHost) return;
			switch (type)
			{
				case HostRequestType.UsePrimary:
					GameData.PlayerInfo play = instance.Data;
					GameData.PlayerInfo playyes = GameData.Instance.GetPlayerById((byte)data[0]);
					ScriptManager.RunCurrentGMFunction("OnUsePrimary", false, (PlayerInfoLua)play, (PlayerInfoLua)playyes);
					break;
				case HostRequestType.Custom:
					ScriptManager.RunCurrentGMFunction("OnHostRecieve", false, data);
					break;
				default:
					throw new NotImplementedException("Host Request Type: " + type + " not implemented!");
			}
		}


		public static void SendKillRequestRPC(PlayerControl self, PlayerControl plfo)
		{

			if (AmongUsClient.Instance.AmClient)
			{
				if (AmongUsClient.Instance.AmHost)
				{
					SendRequestToHost(self, HostRequestType.UsePrimary, plfo.Data.PlayerId);
					return;
				}
			}
			MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(self.NetId, (int)CustomRPCs.CheckWithHost, SendOption.Reliable, AmongUsClient.Instance.HostId);
			messageWriter.Write((byte)HostRequestType.UsePrimary);
			messageWriter.Write(plfo.Data.PlayerId);
			//messageWriter.EndMessage();
			AmongUsClient.Instance.FinishRpcImmediately(messageWriter); //send it NOW.
		}

		public static void SendCustomHostRequestRPC(PlayerControl self, byte id, bool important, List<DynValue> values)
		{

			if (AmongUsClient.Instance.AmClient)
			{
				if (AmongUsClient.Instance.AmHost)
				{
					SendRequestToHost(self, HostRequestType.Custom, id,values);
					return;
				}
			}
			MessageWriter messageWriter = null;
			if (important)
			{
				messageWriter = AmongUsClient.Instance.StartRpcImmediately(self.NetId, (int)CustomRPCs.CheckWithHost, SendOption.Reliable, AmongUsClient.Instance.HostId);
			}
			else
			{
				messageWriter = AmongUsClient.Instance.StartRpc(self.NetId, (int)CustomRPCs.CheckWithHost, SendOption.Reliable);
			}
			messageWriter.Write((byte)HostRequestType.Custom);
			messageWriter.Write(id);
			messageWriter.Write(values);
			if (important)
			{
				AmongUsClient.Instance.FinishRpcImmediately(messageWriter); //send it NOW.
			}
			else
			{
				messageWriter.EndMessage();
			}
		}


		public static void MurderPlayerNoAnimation(PlayerControl target, PlayerControl user, bool GenerateBody = true)
		{
			target.Die(DeathReason.Kill);
			if (GenerateBody)
			{
				DeadBody deadBody = UnityEngine.Object.Instantiate<DeadBody>(PlayerControl.LocalPlayer.KillAnimations[0].bodyPrefab);
				deadBody.enabled = false;
				deadBody.ParentId = target.PlayerId;
				target.SetPlayerMaterialColors(deadBody.bodyRenderer);
				target.SetPlayerMaterialColors(deadBody.bloodSplatter);

				Vector3 vector = target.transform.position + PlayerControl.LocalPlayer.KillAnimations[0].BodyOffset;
				vector.z = vector.y / 1000f;
				deadBody.transform.position = vector;

				deadBody.enabled = true;
			}
			if (target.AmOwner)
			{
				StatsManager instance2 = StatsManager.Instance;
				uint num2 = instance2.TimesMurdered;
				instance2.TimesMurdered = num2 + 1U;
				if (Minigame.Instance)
				{
					try
					{
						Minigame.Instance.Close();
						Minigame.Instance.Close();
					}
					catch
					{
					}
				}
				target.RpcSetScanner(false);
				DestroyableSingleton<HudManager>.Instance.KillOverlay.ShowKillAnimation(user.Data, target.Data);
				DestroyableSingleton<HudManager>.Instance.ShadowQuad.gameObject.SetActive(false);
			}
		}



		public static void SetImportantText(this PlayerControl player, string text)
		{
			if (player.myTasks.Count == 0) return;
			if (player.myTasks[0].TryCast<ImportantTextTask>())
			{
				player.myTasks.RemoveAt(0);
			}
			if (text == "") return; //aka this is just a "remove the important task!!" call
			ImportantTextTask importantTextTask = new GameObject("_ImportantTask").AddComponent<ImportantTextTask>();
			importantTextTask.transform.SetParent(PlayerControl.LocalPlayer.transform, false);
			importantTextTask.Text = text;
			player.myTasks.Insert(0, importantTextTask);
		}

		public static void SetImportantText(this PlayerControl player)
		{
			Role myrole = player.Data.GetRole();
			if (myrole.HasTasks)
			{
				SetImportantText(player,"");
			}
			else
			{
				SetImportantText(player, "<color=#" + myrole.RoleColor.ToHtmlStringRGBA() + ">" + myrole.FakeTaskString + "</color>\r\n<color=#FFFFFFFF>" + "Fake Tasks:" + "</color>");
			}
		}


		public static void SetRoles(PlayerControl self, byte[] players, byte[] roles, bool showintro)
		{
			bool involves_me = players.Contains(self.Data.PlayerId);
			if (showintro)
			{
				StatsManager.Instance.LastGameStarted = Il2CppSystem.DateTime.UtcNow;
			}
			for (int i = 0; i < roles.Length; i++)
			{
				if (roles[i] == 0) continue;
				GameData.PlayerInfo playerById = GameData.Instance.GetPlayerById(players[i]);
				if (playerById != null)
				{
					playerById.GetExtension().Role = roles[i];
				}
			}
			GameData.PlayerInfo data = PlayerControl.LocalPlayer.Data;
			Role myrole = data.GetRole();
			DestroyableSingleton<HudManager>.Instance.MapButton.gameObject.SetActive(true);
			DestroyableSingleton<HudManager>.Instance.ReportButton.gameObject.SetActive(myrole.CanDo(RoleSpecials.Report,data));
			DestroyableSingleton<HudManager>.Instance.UseButton.gameObject.SetActive(true);
			if (showintro)
			{
				PlayerControl.LocalPlayer.RemainingEmergencies = PlayerControl.GameOptions.NumEmergencyMeetings;
			}
			if (involves_me)
			{
				if (myrole.CanDo(RoleSpecials.Primary, data))
				{
					DestroyableSingleton<HudManager>.Instance.KillButton.gameObject.SetActive(true);
					if (showintro)
					{
						PlayerControl.LocalPlayer.SetKillTimer(10f);
					}
					else
					{
						PlayerControl.LocalPlayer.SetKillTimer(PlayerControl.GameOptions.KillCooldown);
					}
				}
			}
			HudManager.Instance.SetHudActive(true);

			if (!showintro)
			{
				PlayerControl.LocalPlayer.SetImportantText();
			}

			for (int j = 0; j < players.Length; j++)
			{
				GameData.PlayerInfo playerById2 = GameData.Instance.GetPlayerById(players[j]);
				if (playerById2 != null)
				{
					if (playerById2.GetRole().CanBeSeen(playerById2, PlayerControl.LocalPlayer.Data))
					{
						playerById2.Object.nameText.color = playerById2.GetRole().RoleTextColor;
					}
				}
			}
			if (!DestroyableSingleton<TutorialManager>.InstanceExists && showintro)
			{
				List<PlayerControl> yourTeam;
				//get all players, convert them back to a regular, sane list. im going to create a conversion function i stg.
				Il2CppSystem.Collections.Generic.List<GameData.PlayerInfo> playerlist = GameData.Instance.AllPlayers;
				List<GameData.PlayerInfo> listfixed = new List<GameData.PlayerInfo>();
				for (int i = 0; i < playerlist.Count; i++)
				{
					listfixed.Add(playerlist[i]);
				}
				byte layer = PlayerControl.LocalPlayer.Data.GetRole().Layer;
				yourTeam = (from pcd in listfixed
							where !pcd.Disconnected
							where ((pcd.GetRole().Layer == layer) || layer == 255) && (layer != 0)
							select pcd.Object).OrderBy(delegate (PlayerControl pc)
							{
								if (!(pc == PlayerControl.LocalPlayer))
								{
									return 1;
								}
								return 0;
							}).ToList<PlayerControl>();

				if (layer == 0)
				{
					yourTeam.Add(PlayerControl.LocalPlayer);
				}

				self.StopAllCoroutines();


				Il2CppSystem.Collections.Generic.List<PlayerControl> yourteamreal = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
				for (int i = 0; i < yourTeam.Count; i++)
				{
					yourteamreal.Add(yourTeam[i]);
				}
				
				DestroyableSingleton<HudManager>.Instance.StartCoroutine(DestroyableSingleton<HudManager>.Instance.CoShowIntro(yourteamreal));

			}
		}


		public static List<GameData.PlayerInfo> GetAllPlayersOnLayer(byte layer)
		{
			List<GameData.PlayerInfo> PlayersToReturn = new List<GameData.PlayerInfo>();
			for (int i = 0; i < GameData.Instance.AllPlayers.Count; i++)
			{
				if (GameData.Instance.AllPlayers[i].GetRole().Layer == layer)
				{
					PlayersToReturn.Add(GameData.Instance.AllPlayers[i]);
				}
			}
			return PlayersToReturn;
		}

		public static List<GameData.PlayerInfo> GetAllPlayersOnTeam(byte team)
		{
			List<GameData.PlayerInfo> PlayersToReturn = new List<GameData.PlayerInfo>();
			for (int i = 0; i < GameData.Instance.AllPlayers.Count; i++)
			{
				if (GameData.Instance.AllPlayers[i].GetRole().Team == team)
				{
					PlayersToReturn.Add(GameData.Instance.AllPlayers[i]);
				}
			}
			return PlayersToReturn;
		}

		public static int FindAmountWithRole(string role)
		{
			int amount = 0;
			for (int i = 0; i < GameData.Instance.AllPlayers.Count; i++)
			{
				if (GameData.Instance.AllPlayers[i].GetRole().Internal_Name == role)
				{
					amount++;
				}
			}
			return amount;
		}

		public static void WriteVictory(List<GameData.PlayerInfo> players, string sound, VictoryTypes type)
		{
			if (AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Ended) return;
			MessageWriter messageWriter = AmongUsClient.Instance.StartEndGame();
			messageWriter.Write((byte)type);
			messageWriter.Write((byte)players.Count);
			for (int i = 0; i < players.Count; i++)
			{
				messageWriter.Write(players[i].PlayerId);
			}
			messageWriter.Write(sound);
			AmongUsClient.Instance.FinishEndGame(messageWriter);
		}

		public static void CommenceVictory(AmongUsClient self, byte[] players, string song, VictoryTypes type)
		{
			CETempData.VictoryType = type;
			CETempData.End_Song = song;
			StatsManager.Instance.BanPoints -= 1.5f;
			StatsManager.Instance.LastGameStarted = Il2CppSystem.DateTime.MinValue;
			self.DisconnectHandlers.Clear();
			if (Minigame.Instance)
			{
				try
				{
					Minigame.Instance.Close();
					Minigame.Instance.Close();
				}
				catch
				{
					Debug.LogError("AmongUsClient::OnGameEnd Exception: 1");
				}
			}
			TempData.EndReason = GameOverReason.HumansByVote;
			TempData.showAd = false;
			TempData.winners = new Il2CppSystem.Collections.Generic.List<WinningPlayerData>();
			CETempData.AdditionalWinningInfos = new List<AdditionalWinningInfo>();
			for (int i = 0; i < GameData.Instance.PlayerCount; i++)
			{
				GameData.PlayerInfo playerInfo = GameData.Instance.AllPlayers[i];
				if (players.Contains(playerInfo.PlayerId))
				{
					CETempData.AdditionalWinningInfos.Add(new AdditionalWinningInfo(playerInfo));
					TempData.winners.Add(new WinningPlayerData(playerInfo));
				}
			}
			self.StartCoroutine(self.CoEndGame());
		}

		public static void WriteMurderButBetter(PlayerControl self, PlayerControl user, PlayerControl target, bool DoAnimation)
		{
			if (!AmongUsClient.Instance.AmHost) return;
			if (AmongUsClient.Instance.AmClient)
			{
				if (DoAnimation)
				{
					if (PlayerControl.LocalPlayer == user)
					{
						if (Minigame.Instance)
						{
							try
							{
								Minigame.Instance.Close();
								Minigame.Instance.Close();
							}
							catch
							{
							}
						}
					}
					user.MurderPlayer(target);
				}
				else
				{
					MurderPlayerNoAnimation(target, user);
				}
			}
			MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(self.NetId, 12, SendOption.Reliable, -1);
			InnerNet.MessageExtensions.WriteNetObject(messageWriter, user);
			InnerNet.MessageExtensions.WriteNetObject(messageWriter, target);
			messageWriter.Write(DoAnimation);
			AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
		}


	}

	public static class SetFunctions
	{
		public static void RPCSetUserData(PlayerControl self, PlayerControl player, int data, int toset)
		{
			if (AmongUsClient.Instance.AmClient)
			{
				player.Data.GetExtension().Userdata[data] = toset;
			}
			MessageWriter messageWriter = AmongUsClient.Instance.StartRpc(self.NetId, (int)CustomRPCs.SetUserData, SendOption.Reliable);
			messageWriter.Write(player.Data.PlayerId);
			messageWriter.WritePacked(data);
			messageWriter.WritePacked(toset);
			messageWriter.EndMessage();
		}
	}

}
