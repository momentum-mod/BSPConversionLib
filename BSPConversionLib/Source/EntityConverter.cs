﻿using LibBSP;
using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BSPConversionLib
{
	public class EntityConverter
	{
		private Entities q3Entities;
		private Entities sourceEntities;
		private Dictionary<string, Shader> shaderDict;
		private int minDamageToConvertTrigger;
		
		private Dictionary<string, List<Entity>> entityDict = new Dictionary<string, List<Entity>>();
		private List<Entity> removeEntities = new List<Entity>(); // Entities to remove after conversion (ex: remove weapons after converting a trigger_multiple that references target_give). TODO: It might be better to convert entities by priority, such as trigger_multiples first so that target_give weapons can be ignored after
		private int currentCheckpointIndex = 2;

		private const string MOMENTUM_START_ENTITY = "_momentum_player_start_";

		public EntityConverter(Entities q3Entities, Entities sourceEntities, Dictionary<string, Shader> shaderDict, int minDamageToConvertTrigger)
		{
			this.q3Entities = q3Entities;
			this.sourceEntities = sourceEntities;
			this.shaderDict = shaderDict;
			this.minDamageToConvertTrigger = minDamageToConvertTrigger;

			foreach (var entity in q3Entities)
			{
				if (!entityDict.ContainsKey(entity.Name))
					entityDict.Add(entity.Name, new List<Entity>() { entity });
				else
					entityDict[entity.Name].Add(entity);
			}
		}

		public void Convert()
		{
			foreach (var entity in q3Entities)
			{
				var ignoreEntity = false;

				switch (entity.ClassName)
				{
					case "worldspawn":
						ConvertWorldspawn(entity);
						break;
					case "info_player_start":
						ConvertPlayerStart(entity);
						break;
					case "info_player_deathmatch":
						ConvertPlayerStart(entity);
						break;
					case "trigger_hurt":
						ConvertTriggerHurt(entity);
						break;
					case "trigger_multiple":
						ConvertTriggerMultiple(entity);
						break;
					case "trigger_push":
						ConvertTriggerPush(entity);
						break;
					case "trigger_teleport":
						ConvertTriggerTeleport(entity);
						break;
					case "misc_teleporter_dest":
						entity.ClassName = "info_teleport_destination";
						break;
					case "target_position":
						entity.ClassName = "info_target";
						break;
					// Ignore these entities since they have no use in Source engine
					case "target_startTimer":
					case "target_stopTimer":
					case "target_checkpoint":
					case "target_give":
						ignoreEntity = true;
						break;
					default:
						{
							if (entity.ClassName.StartsWith("weapon_"))
								ConvertWeapon(entity);
							else if (entity.ClassName.StartsWith("ammo_"))
								ConvertAmmo(entity);
							else if (entity.ClassName.StartsWith("item_"))
								ConvertItem(entity);
							
							break;
						}
				}

				if (!ignoreEntity)
					sourceEntities.Add(entity);
			}

			foreach (var entity in removeEntities)
				sourceEntities.Remove(entity);
		}

		private void ConvertWorldspawn(Entity worldspawn)
		{
			foreach (var shader in shaderDict.Values)
			{
				if (shader.skyParms != null)
				{
					var skyName = shader.skyParms.outerBox;
					if (!string.IsNullOrEmpty(skyName))
						worldspawn["skyname"] = skyName;
				}
			}
		}

		private void ConvertPlayerStart(Entity playerStart)
		{
			playerStart.ClassName = "info_player_start";
			playerStart.Name = MOMENTUM_START_ENTITY;

			if (TryGetTargetEntities(playerStart, out var targetEnts))
			{
				foreach (var targetEnt in targetEnts)
				{
					switch (targetEnt.ClassName)
					{
						case "target_give":
							ConvertPlayerStartTargetGive(playerStart, targetEnt);
							break;
					}
				}
			}
		}

		private void ConvertPlayerStartTargetGive(Entity playerStart, Entity targetGive)
		{
			if (TryGetTargetEntities(targetGive, out var targetEnts))
			{
				foreach (var targetEnt in targetEnts)
				{
					if (targetEnt.ClassName.StartsWith("weapon_"))
					{
						var weaponName = GetMomentumWeaponName(targetEnt.ClassName);
						var weapon = CreateTargetGiveWeapon(weaponName, playerStart.Origin, targetEnt["count"]);
						sourceEntities.Add(weapon);
					}
					else if (targetEnt.ClassName.StartsWith("ammo_"))
					{
						var ammoName = GetMomentumAmmoName(targetEnt.ClassName);
						var ammo = CreateTargetGiveAmmo(ammoName, playerStart.Origin, targetEnt["count"]);
						sourceEntities.Add(ammo);
					}
					else if (targetEnt.ClassName.StartsWith("item_"))
					{
						var itemName = GetMomentumItemName(targetEnt.ClassName);
						var item = CreateTargetGiveItem(itemName, playerStart.Origin, targetEnt["count"]);
						sourceEntities.Add(item);
					}

					removeEntities.Add(targetEnt);
				}
			}
		}

		private Entity CreateTargetGiveWeapon(string weaponName, Vector3 origin, string count)
		{
			var weapon = new Entity();

			weapon.ClassName = "momentum_weapon_spawner";
			weapon.Origin = origin;
			weapon["weaponname"] = weaponName;
			weapon["pickupammo"] = count;
			weapon["resettime"] = "-1"; // Only use once
			weapon["rendermode"] = "10";
			
			return weapon;
		}

		private Entity CreateTargetGiveAmmo(string ammoName, Vector3 origin, string count)
		{
			var ammo = new Entity();

			ammo.ClassName = "momentum_pickup_ammo";
			ammo.Origin = origin;
			ammo["ammoname"] = ammoName;
			ammo["pickupammo"] = count;
			ammo["resettime"] = "-1"; // Only use once
			ammo["rendermode"] = "10";

			return ammo;
		}

		private Entity CreateTargetGiveItem(string itemName, Vector3 origin, string count)
		{
			var item = new Entity();

			item.ClassName = itemName;
			item.Origin = origin;
			item["resettime"] = "-1"; // Only use once
			item["rendermode"] = "10";

			if (itemName == "momentum_powerup_haste")
				item["hastetime"] = count;
			else if (itemName == "momentum_powerup_damage_boost")
				item["damageboosttime"] = count;

			return item;
		}

		private void ConvertTriggerHurt(Entity trigger)
		{
			if (int.TryParse(trigger["dmg"], out var damage))
			{
				if (damage >= minDamageToConvertTrigger)
				{
					trigger.ClassName = "trigger_teleport";
					trigger["target"] = MOMENTUM_START_ENTITY;
					trigger["spawnflags"] = "1";
					trigger["mode"] = "1";
				}
			}
		}

		private void ConvertTriggerMultiple(Entity trigger)
		{
			if (!TryGetTargetEntities(trigger, out var targetEnts))
				return;

			foreach (var targetEnt in targetEnts)
			{
				switch (targetEnt.ClassName)
				{
					case "target_stopTimer":
						ConvertTimerTrigger(trigger, "trigger_momentum_timer_stop", 0);
						break;
					case "target_checkpoint":
						ConvertTimerTrigger(trigger, "trigger_momentum_timer_checkpoint", currentCheckpointIndex);
						currentCheckpointIndex++;
						break;
					case "target_give":
						ConvertGiveTrigger(trigger, targetEnt);
						break;
					case "target_teleporter":
						ConvertTeleportTrigger(trigger, targetEnt);
						break;
				}
			}

			trigger["spawnflags"] = "1";
			trigger.Remove("target");
		}

		private static void ConvertTimerTrigger(Entity trigger, string className, int zoneNumber)
		{
			trigger.ClassName = className;
			//trigger["track_number"] = "0";
			trigger["zone_number"] = zoneNumber.ToString();
		}

		// TODO: Convert target_give for player spawn entities
		private void ConvertGiveTrigger(Entity trigger, Entity targetGive)
		{
			if (!TryGetTargetEntities(targetGive, out var targetEnts))
				return;
			
			// TODO: Support more entities (ammo, health, armor, etc.)
			foreach (var targetEnt in targetEnts)
			{
				switch (targetEnt.ClassName)
				{
					case "item_haste":
						GiveHasteOnStartTouch(trigger, targetEnt);
						break;
					case "item_enviro": // TODO: Not supported yet
						break;
					case "item_flight": // TODO: Not supported yet
						break;
					case "item_quad":
						GiveQuadOnStartTouch(trigger, targetEnt);
						break;
					default:
						if (targetEnt.ClassName.StartsWith("weapon_"))
							GiveWeaponOnStartTouch(trigger, targetEnt);
						else if (targetEnt.ClassName.StartsWith("ammo_"))
							GiveAmmoOnStartTouch(trigger, targetEnt);
						break;
				}

				removeEntities.Add(targetEnt);
			}
		}

		private void GiveHasteOnStartTouch(Entity trigger, Entity hasteEnt)
		{
			var connection = new Entity.EntityConnection()
			{
				name = "OnStartTouch",
				target = "!activator",
				action = "SetHaste",
				param = hasteEnt["count"],
				delay = 0f,
				fireOnce = -1
			};
			trigger.connections.Add(connection);
		}

		private void GiveQuadOnStartTouch(Entity trigger, Entity quadEnt)
		{
			var connection = new Entity.EntityConnection()
			{
				name = "OnStartTouch",
				target = "!activator",
				action = "SetDamageBoost",
				param = quadEnt["count"],
				delay = 0f,
				fireOnce = -1
			};
			trigger.connections.Add(connection);
		}

		private void GiveWeaponOnStartTouch(Entity trigger, Entity target)
		{
			var weaponIndex = GetWeaponIndex(target.ClassName);
			if (weaponIndex == -1)
				return;
			
			// TODO: Support weapon count
			var connection = new Entity.EntityConnection()
			{
				name = "OnStartTouch",
				target = "!activator",
				action = "GiveDFWeapon",
				param = weaponIndex.ToString(),
				delay = 0f,
				fireOnce = -1
			};
			trigger.connections.Add(connection);
		}

		private int GetWeaponIndex(string weaponName)
		{
			switch (weaponName)
			{
				case "weapon_machinegun":
					return 2;
				case "weapon_gauntlet":
					return 3;
				case "weapon_grenadelauncher":
					return 4;
				case "weapon_rocketlauncher":
					return 5;
				case "weapon_plasmagun":
					return 8;
				case "weapon_bfg":
					return 9;
				default:
					return -1;
			}
		}

		private void GiveAmmoOnStartTouch(Entity trigger, Entity ammoEnt)
		{
			var ammoOutput = GetAmmoOutput(ammoEnt.ClassName);
			if (string.IsNullOrEmpty(ammoOutput))
				return;

			var connection = new Entity.EntityConnection()
			{
				name = "OnStartTouch",
				target = "!activator",
				action = ammoOutput,
				param = ammoEnt["count"],
				delay = 0f,
				fireOnce = -1
			};
			trigger.connections.Add(connection);
		}

		private string GetAmmoOutput(string ammoName)
		{
			switch (ammoName)
			{
				case "ammo_bfg":
					return "AddBfgRockets";
				case "ammo_bullets": // Machine gun
					return "AddBullets";
				case "ammo_cells": // Plasma gun
					return "AddPlasma";
				case "ammo_grenades":
					return "AddGrenades";
				case "ammo_lightning":
					return "AddCells";
				case "ammo_rockets":
					return "AddRockets";
				case "ammo_shells": // Shotgun
					return "AddShells";
				case "ammo_slugs": // Railgun
					return "AddRails";
				default:
					return string.Empty;
			}
		}

		private void ConvertTeleportTrigger(Entity trigger, Entity targetTele)
		{
			if (!TryGetTargetEntities(targetTele, out var targetEnts))
				return;

			// Set player position to target_teleporter's destination
			var targetEnt = targetEnts.First();
			var telePos = targetEnt["origin"];
			
			var connection = new Entity.EntityConnection()
			{
				name = "OnStartTouch",
				target = "!activator",
				action = "SetLocalOrigin",
				param = telePos,
				delay = 0f,
				fireOnce = -1
			};
			trigger.connections.Add(connection);
		}

		private void ConvertTriggerPush(Entity trigger)
		{
			if (!TryGetTargetEntities(trigger, out var targetEnts))
				return;

			trigger.ClassName = "trigger_catapult";
			trigger["launchtarget"] = targetEnts.First().Name;
			trigger["spawnflags"] = "1";
			trigger["playerspeed"] = "450";

			trigger.Remove("target");
		}

		private void ConvertTriggerTeleport(Entity trigger)
		{
			trigger["spawnflags"] = "1";
			trigger["mode"] = "5";
			trigger["setspeed"] = "400";

			if (TryGetTargetEntities(trigger, out var targetEnts))
			{
				foreach (var ent in targetEnts)
					ent.ClassName = "info_teleport_destination";
			}
		}

		private void ConvertWeapon(Entity weaponEnt)
		{
			weaponEnt["weaponname"] = GetMomentumWeaponName(weaponEnt.ClassName);
			weaponEnt["pickupammo"] = weaponEnt["count"];
			weaponEnt.ClassName = "momentum_weapon_spawner";
		}

		private string GetMomentumWeaponName(string q3WeaponName)
		{
			switch (q3WeaponName)
			{
				case "weapon_machinegun":
					return "weapon_momentum_machinegun";
				case "weapon_gauntlet":
					return "weapon_knife";
				case "weapon_grenadelauncher":
					return "weapon_momentum_df_grenadelauncher";
				case "weapon_rocketlauncher":
					return "weapon_momentum_df_rocketlauncher";
				case "weapon_plasmagun":
					return "weapon_momentum_df_plasmagun";
				case "weapon_bfg":
					return "weapon_momentum_df_bfg";
				case "item_haste":
					return "momentum_powerup_haste";
				case "item_quad":
					return "momentum_powerup_damage_boost";
				default:
					return string.Empty;
			}
		}

		private void ConvertAmmo(Entity ammoEnt)
		{
			ammoEnt["ammoname"] = GetMomentumAmmoName(ammoEnt.ClassName);
			ammoEnt["pickupammo"] = ammoEnt["count"];
			ammoEnt.ClassName = "momentum_pickup_ammo";
		}

		private string GetMomentumAmmoName(string q3AmmoName)
		{
			switch (q3AmmoName)
			{
				case "ammo_bfg":
					return "bfg_rockets";
				case "ammo_bullets": // Machine gun
					return "bullets";
				case "ammo_cells": // Plasma gun
					return "plasma";
				case "ammo_grenades":
					return "grenades";
				case "ammo_lightning":
					return "cells";
				case "ammo_rockets":
					return "rockets";
				case "ammo_shells": // Shotgun
					return "shells";
				case "ammo_slugs": // Railgun
					return "rails";
				default:
					return string.Empty;
			}
		}

		private void ConvertItem(Entity itemEnt)
		{
			itemEnt["resettime"] = itemEnt["wait"];
			if (itemEnt.ClassName == "item_haste")
				itemEnt["hastetime"] = itemEnt["count"];
			else if (itemEnt.ClassName == "item_quad")
				itemEnt["damageboosttime"] = itemEnt["count"];

			itemEnt.ClassName = GetMomentumItemName(itemEnt.ClassName);
		}

		private string GetMomentumItemName(string q3ItemName)
		{
			switch (q3ItemName)
			{
				case "item_haste":
					return "momentum_powerup_haste";
				case "item_quad":
					return "momentum_powerup_damage_boost";
				default:
					return string.Empty;
			}
		}

		private bool TryGetTargetEntities(Entity sourceEntity, out List<Entity> targetEntities)
		{
			if (sourceEntity.TryGetValue("target", out var target))
			{
				if (entityDict.ContainsKey(target))
				{
					targetEntities = entityDict[target];
					return true;
				}
			}

			targetEntities = new List<Entity>();
			return false;
		}
	}
}
