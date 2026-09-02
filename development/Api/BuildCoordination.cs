using System.Collections.Generic;
using Utf8Json;
using Webserver;
using Webserver.WebAPI;

namespace UndeadLegacyPanels.Api
{
	/// <summary>
	/// GET /api/buildcoordination. Per known player: level, starting class, and three score
	/// breakdowns (resourceSkills, meleeWeapons, rangedWeapons), each a dict of
	/// {itemName: {score, maxPossible}} - deliberately data-only, no "favorite" picked
	/// server-side. The frontend derives "Favored Melee Weapon" etc. by ranking each group and
	/// dropping zero-score entries (design discussion) - keeping that presentation logic out of
	/// the API keeps this response reusable for other future views.
	///
	/// All scores are talent-unlock counts, never live/active stat values or held gear - see
	/// design doc §7's EffectManager.GetValue finding for why that split is deliberate.
	/// Attribute totals and per-player "repair" score were both dropped from the response
	/// entirely per design discussion: attributes weren't useful in practice, and repair has zero
	/// matching talent tags in this config (confirmed, not a bug - see ActivityTagConfig), so a
	/// permanently-empty 0/0 field had no reason to exist in the API at all.
	/// </summary>
	public class BuildCoordination : AbsRestApi
	{
		public BuildCoordination()
			: base(null)
		{
		}

		protected override void HandleRestGet(RequestContext _context)
		{
			List<KnownPlayer> players = PlayerRegistry.GetKnownPlayers();
			string playerDir = PlayerRegistry.GetPlayerDir();
			Dictionary<string, UnityEngine.Vector3?> onlinePlayerPositions = GetOnlinePlayerPositions();

			HashSet<string> miningClasses = ActivityTagConfig.GetProgressionClassesForActivity("mining");
			HashSet<string> salvageClasses = ActivityTagConfig.GetProgressionClassesForActivity("salvage");

			JsonWriter writer;
			PrepareEnvelopedResult(out writer);
			writer.WriteBeginObject();
			writer.WritePropertyName("players");
			writer.WriteBeginArray();

			bool firstPlayer = true;
			foreach (KnownPlayer kp in players)
			{
				if (!firstPlayer)
				{
					writer.WriteValueSeparator();
				}
				firstPlayer = false;

				PlayerSaveData save = PlayerSaveReader.Load(playerDir, kp.FileBaseName);

				string playerClass = null;
				foreach (var kv in SkillTreeConstants.ClassDisplayNames)
				{
					if (save.GetProgressionLevel(kv.Key) > 0)
					{
						playerClass = kv.Value;
						break;
					}
				}

				int lockpickScore = save.GetProgressionLevel(SkillTreeConstants.LockPickBaseProgression)
					+ save.GetProgressionLevel(SkillTreeConstants.LockPickProgression);

				writer.WriteBeginObject();
				writer.WritePropertyName("name");
				writer.WriteString(kp.DisplayName);
				writer.WriteValueSeparator();
				writer.WritePropertyName("platformId");
				writer.WriteString(kp.FileBaseName);
				writer.WriteValueSeparator();
				bool isOnline = onlinePlayerPositions.TryGetValue(kp.FileBaseName, out UnityEngine.Vector3? position);
				writer.WritePropertyName("online");
				writer.WriteBoolean(isOnline);
				writer.WriteValueSeparator();
				writer.WritePropertyName("position");
				if (position.HasValue)
				{
					writer.WriteBeginObject();
					writer.WritePropertyName("x");
					writer.WriteSingle(position.Value.x);
					writer.WriteValueSeparator();
					writer.WritePropertyName("y");
					writer.WriteSingle(position.Value.y);
					writer.WriteValueSeparator();
					writer.WritePropertyName("z");
					writer.WriteSingle(position.Value.z);
					writer.WriteEndObject();
				}
				else
				{
					writer.WriteNull();
				}
				writer.WriteValueSeparator();
				writer.WritePropertyName("level");
				writer.WriteInt32(save.CharacterLevel);
				writer.WriteValueSeparator();
				writer.WritePropertyName("class");
				writer.WriteString(playerClass ?? "not selected");
				writer.WriteValueSeparator();

				writer.WritePropertyName("resourceSkills");
				writer.WriteBeginObject();
				WriteScore(ref writer, "mining", CountUnlocked(save, miningClasses), miningClasses.Count);
				writer.WriteValueSeparator();
				WriteScore(ref writer, "salvage", CountUnlocked(save, salvageClasses), salvageClasses.Count);
				writer.WriteValueSeparator();
				WriteScore(ref writer, "lockpick", lockpickScore, 5);
				writer.WriteEndObject();
				writer.WriteValueSeparator();

				writer.WritePropertyName("meleeWeapons");
				WriteWeaponGroup(ref writer, save, WeaponTagConfig.MeleeWeaponTags);
				writer.WriteValueSeparator();

				writer.WritePropertyName("rangedWeapons");
				WriteWeaponGroup(ref writer, save, WeaponTagConfig.RangedWeaponTags);

				writer.WriteEndObject();
			}

			writer.WriteEndArray();
			writer.WriteEndObject();

			SendEnvelopedResult(_context, ref writer);
		}

		private static void WriteWeaponGroup(ref JsonWriter writer, PlayerSaveData save, string[] weaponTags)
		{
			writer.WriteBeginObject();
			bool first = true;
			foreach (string tag in weaponTags)
			{
				if (!first)
				{
					writer.WriteValueSeparator();
				}
				first = false;
				HashSet<string> classes = WeaponTagConfig.GetProgressionClassesForWeaponTag(tag);
				WriteScore(ref writer, tag, CountUnlocked(save, classes), classes.Count);
			}
			writer.WriteEndObject();
		}

		/// <summary>
		/// Online status: bug found and fixed live: save-file keys (.ttp file names,
		/// KnownPlayer.FileBaseName) are built from the crossplatform/EOS identity (confirmed
		/// from real connection logs: "PltfmId='Steam_76561197971824535', CrossId='EOS_0002570c...'"),
		/// but this originally compared against ClientInfo.PlatformId (the native/Steam identity)
		/// - a guaranteed mismatch. Checks both PlatformId and CrossplatformId now, so a match on
		/// either identity succeeds - covers players without a linked crossplatform ID too.
		///
		/// Position: read directly from the live entity (GameManager.Instance.World.Players.dict),
		/// same self-contained approach as online status - not from the stock /api/player, whose
		/// row-level access check restricts non-admins to seeing only themselves. A player key can
		/// be present (online) with a null position if the entity lookup fails for some reason
		/// (e.g. mid-connect) - callers must check for that rather than assume every online entry
		/// has a resolvable position.
		/// </summary>
		private static Dictionary<string, UnityEngine.Vector3?> GetOnlinePlayerPositions()
		{
			var result = new Dictionary<string, UnityEngine.Vector3?>(System.StringComparer.OrdinalIgnoreCase);
			var clients = SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.List;
			for (int i = 0; i < clients.Count; i++)
			{
				ClientInfo client = clients[i];

				UnityEngine.Vector3? position = null;
				if (GameManager.Instance != null && GameManager.Instance.World != null
					&& GameManager.Instance.World.Players.dict.TryGetValue(client.entityId, out EntityPlayer entity)
					&& entity != null)
				{
					position = entity.GetPosition();
				}

				PlatformUserIdentifierAbs platformId = client.PlatformId;
				if (platformId != null)
				{
					result[platformId.CombinedString] = position;
				}
				PlatformUserIdentifierAbs crossplatformId = client.CrossplatformId;
				if (crossplatformId != null)
				{
					result[crossplatformId.CombinedString] = position;
				}
			}
			return result;
		}

		private static int CountUnlocked(PlayerSaveData save, HashSet<string> progressionClasses)
		{
			int count = 0;
			foreach (string className in progressionClasses)
			{
				if (save.GetProgressionLevel(className) > 0)
				{
					count++;
				}
			}
			return count;
		}

		private static void WriteScore(ref JsonWriter writer, string propertyName, int score, int maxPossible)
		{
			writer.WritePropertyName(propertyName);
			writer.WriteBeginObject();
			writer.WritePropertyName("score");
			writer.WriteInt32(score);
			writer.WriteValueSeparator();
			writer.WritePropertyName("maxPossible");
			writer.WriteInt32(maxPossible);
			writer.WriteEndObject();
		}

		// "Anyone reads" per design doc §2.
		public override int DefaultPermissionLevel()
		{
			return 1000;
		}
	}
}
