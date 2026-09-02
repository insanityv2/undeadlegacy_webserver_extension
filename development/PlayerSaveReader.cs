using System;
using System.Collections.Generic;
using System.IO;

namespace UndeadLegacyPanels
{
	/// <summary>
	/// Reads the pieces of a player's .ttp save file this mod needs, without touching any live
	/// game/world object. Uses the real PlayerDataFile.Load() for the full sequential binary
	/// parse (version-tolerant, handles every preceding field correctly), then does its own
	/// small, self-contained parse of the two byte blobs PlayerDataFile leaves undecoded
	/// (progressionData / buffData), since decoding those fully (Progression.Read / EntityBuffs.Read)
	/// would require a live EntityAlive instance we don't have and don't want to fabricate.
	/// </summary>
	/// <summary>
	/// One manually player-placed map marker, extracted from PlayerDataFile.waypoints into plain
	/// data so cached PlayerSaveData never holds live game objects (Waypoint references a
	/// PlatformUserIdentifierAbs and localization state we don't want to retain or share across
	/// threads).
	/// </summary>
	public class MarkerData
	{
		public string RawName; // literal text, or a localization key if NameIsLocalizationId
		public bool NameIsLocalizationId;
		public string Icon;
		public string OwnerKey; // ownerId.CombinedString, or null for the save's own markers
		public int X;
		public int Y;
		public int Z;
	}

	public class PlayerSaveData
	{
		public List<string> UnlockedRecipes = new List<string>();

		/// Manually placed map markers only - auto-tracked entity waypoints
		/// (vehicles/drones, lastKnownPositionEntityId != -1) are excluded at extraction,
		/// matching Waypoint.CanBeViewedBy's owner-only rule for those.
		public List<MarkerData> Markers = new List<MarkerData>();

		public int CharacterLevel;

		/// Progression-class name -> level (covers attributes, classes, books, UL skill-tree nodes).
		public Dictionary<string, int> ProgressionLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		/// CVar name -> value (covers UL research-node unlock flags, among other things).
		public Dictionary<string, float> BuffCVars = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

		public int GetProgressionLevel(string name)
		{
			return ProgressionLevels.TryGetValue(name, out int level) ? level : 0;
		}

		public bool HasCVar(string name)
		{
			return BuffCVars.TryGetValue(name, out float value) && value > 0f;
		}
	}

	public static class PlayerSaveReader
	{
		public static PlayerSaveData Load(string playerDir, string fileBaseName)
		{
			PlayerDataFile file = new PlayerDataFile();
			try
			{
				file.Load(playerDir, fileBaseName);
			}
			catch (Exception ex)
			{
				Log.Warning("[UndeadLegacyPanels] Failed loading player file '" + fileBaseName + "': " + ex.Message);
				return new PlayerSaveData();
			}

			PlayerSaveData result = new PlayerSaveData();
			if (file.unlockedRecipeList != null)
			{
				result.UnlockedRecipes.AddRange(file.unlockedRecipeList);
			}

			// waypoints.Read() runs inline inside PlayerDataFile.Read() (same as questJournal),
			// so the collection is already fully populated - extract to plain data here.
			if (file.waypoints != null)
			{
				foreach (Waypoint wp in file.waypoints.Collection.list)
				{
					if (wp.lastKnownPositionEntityId != -1)
					{
						continue; // auto-tracked (vehicle/drone) - owner-only, not shared
					}
					result.Markers.Add(new MarkerData
					{
						RawName = wp.name != null ? wp.name.Text : "",
						NameIsLocalizationId = wp.bUsingLocalizationId,
						Icon = wp.icon,
						OwnerKey = wp.ownerId != null ? wp.ownerId.CombinedString : null,
						X = wp.pos.x,
						Y = wp.pos.y,
						Z = wp.pos.z,
					});
				}
			}

			try
			{
				result.ProgressionLevels = ParseProgressionBlob(file.progressionData, out int characterLevel);
				result.CharacterLevel = characterLevel;
			}
			catch (Exception ex)
			{
				Log.Warning("[UndeadLegacyPanels] Failed parsing progressionData for '" + fileBaseName + "': " + ex.Message);
			}

			try
			{
				result.BuffCVars = ParseBuffCVars(file.buffData);
			}
			catch (Exception ex)
			{
				Log.Warning("[UndeadLegacyPanels] Failed parsing buffData for '" + fileBaseName + "': " + ex.Message);
			}

			return result;
		}

		/// <summary>
		/// Mirrors Progression.Write()'s exact layout (Progression.cs:387, ProgressionValue.Write()):
		/// byte version(3), ushort Level, int ExpToNextLevel, ushort SkillPoints, int count,
		/// then count x [byte(1), string name, byte level, int costForNextLevel], then int ExpDeficit.
		/// Only the per-entry (name, level) pairs are needed here.
		/// </summary>
		private static Dictionary<string, int> ParseProgressionBlob(MemoryStream blob, out int characterLevel)
		{
			var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			characterLevel = 0;
			if (blob == null || blob.Length == 0)
			{
				return result;
			}

			blob.Position = 0;
			using (var br = new BinaryReader(blob, System.Text.Encoding.UTF8, leaveOpen: true))
			{
				br.ReadByte(); // version
				characterLevel = br.ReadUInt16(); // Level
				br.ReadInt32(); // ExpToNextLevel
				br.ReadUInt16(); // SkillPoints
				int count = br.ReadInt32();
				for (int i = 0; i < count; i++)
				{
					br.ReadByte(); // entry version (1)
					string name = br.ReadString();
					byte level = br.ReadByte();
					br.ReadInt32(); // costForNextLevel
					result[name] = level;
				}
				// int ExpDeficit follows; not needed.
			}
			return result;
		}

		/// <summary>
		/// Mirrors EntityBuffs.Write()'s exact layout (EntityBuffs.cs:643, Version = 3):
		/// byte Version, ushort activeBuffsCount, count x BuffValue.Write(), then
		/// ushort cvarCount, then cvarCount x (string name, float value).
		/// Active buffs are skipped byte-for-byte (BuffValue.Read, EntityBuffs.Version=3 means
		/// every buff entry includes the trailing Vector3i instigatorPos) rather than decoded,
		/// since only the CVar section is needed here.
		/// </summary>
		private static Dictionary<string, float> ParseBuffCVars(MemoryStream blob)
		{
			var result = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
			if (blob == null || blob.Length == 0)
			{
				return result;
			}

			blob.Position = 0;
			using (var br = new BinaryReader(blob, System.Text.Encoding.UTF8, leaveOpen: true))
			{
				byte version = br.ReadByte();
				ushort activeBuffsCount = br.ReadUInt16();
				for (int i = 0; i < activeBuffsCount; i++)
				{
					SkipBuffValue(br, version);
				}

				if (version < 2)
				{
					// Pre-string-keyed CVar format (int id -> float); not expected on a live
					// UL server (EntityBuffs.Version is 3), left unhandled deliberately.
					return result;
				}

				ushort cvarCount = br.ReadUInt16();
				for (int i = 0; i < cvarCount; i++)
				{
					string name = br.ReadString();
					float value = br.ReadSingle();
					result[name] = value;
				}
			}
			return result;
		}

		private static void SkipBuffValue(BinaryReader br, int entityBuffsVersion)
		{
			// BuffValue.Read/Write (Assembly-CSharp): string buffName, byte stackEffectMultiplier,
			// uint durationTicks, int instigatorId, byte buffFlags, ushort updateTicks,
			// and (version >= 3) a Vector3i instigatorPos (3 x int32).
			br.ReadString(); // buffName
			br.ReadByte(); // stackEffectMultiplier
			br.ReadUInt32(); // durationTicks
			br.ReadInt32(); // instigatorId
			br.ReadByte(); // buffFlags
			br.ReadUInt16(); // updateTicks
			if (entityBuffsVersion >= 3)
			{
				br.ReadInt32();
				br.ReadInt32();
				br.ReadInt32(); // instigatorPos (Vector3i)
			}
		}
	}
}
