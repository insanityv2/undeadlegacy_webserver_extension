using System.Collections.Generic;
using Utf8Json;
using Webserver;
using Webserver.WebAPI;

namespace UndeadLegacyPanels.Api
{
	/// <summary>
	/// GET /api/sharedmarkers. Aggregates every manually player-placed map marker
	/// (lastKnownPositionEntityId == -1) across every known player's save file - this excludes
	/// only auto-tracked entity waypoints (vehicles/drones, lastKnownPositionEntityId != -1),
	/// which really are owner-only per Waypoint.CanBeViewedBy (Assembly-CSharp).
	///
	/// Investigated live whether "private/allies/everyone" (a real in-game choice when creating a
	/// custom waypoint) is knowable from the creator's own save data - it is not. Sharing is an
	/// invite-based network action (GameManager.WaypointInviteClient sends the waypoint to each
	/// target player's own pending WaypointInvites list; it only joins their Waypoints.Collection
	/// if they accept), not a persistent field on the Waypoint object. The creator's own copy is
	/// byte-identical whether a marker was kept private or shared to everyone. Per design
	/// discussion, this endpoint deliberately shows all manually-placed markers rather than
	/// attempting to filter on a distinction that doesn't exist in the readable data.
	///
	/// waypoints.Read() is called inline inside PlayerDataFile.Read() (same as questJournal, see
	/// design doc §5.3/§7 finding) - PlayerDataFile.Load() already fully populates
	/// file.waypoints.Collection.list, no extra binary parsing needed.
	///
	/// A marker may exist identically in more than one player's own save (e.g. an accepted
	/// invite copies it into the accepting player's own collection too) - deduplicated by
	/// (ownerId, position, name) so the same marker isn't listed multiple times.
	/// </summary>
	public class SharedMarkers : AbsRestApi
	{
		public SharedMarkers()
			: base(null)
		{
		}

		protected override void HandleRestGet(RequestContext _context)
		{
			List<KnownPlayer> players = PlayerRegistry.GetKnownPlayers();
			string playerDir = PlayerRegistry.GetPlayerDir();

			// wp.ownerId.CombinedString is the same "Platform_UserId" key as KnownPlayer.FileBaseName
			// (both derive from PlatformUserIdentifierAbs) - resolve to the player's real in-game
			// name instead of exposing the raw platform id, so two players who happen to pick the
			// same icon/name for a marker can still be told apart (design ask).
			var displayNameByOwnerKey = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
			foreach (KnownPlayer kp in players)
			{
				displayNameByOwnerKey[kp.FileBaseName] = kp.DisplayName;
			}

			// Identical markers (same name, icon, and position) commonly exist in several saves
			// at once: the game auto-adds e.g. a trader's location marker to every player who has
			// discovered it, and an accepted waypoint invite copies the marker into the accepting
			// player's save. Group by (name, icon, position) across all saves, then attribute each
			// group once:
			//   - a copy carrying an explicit ownerId (only invite-copies do) names the true owner;
			//   - a group found in exactly one save belongs to that save's player (a player's own
			//     markers carry no ownerId - confirmed live, see below);
			//   - a group in multiple saves with no explicit owner anywhere is game-generated
			//     (trader discovery) - no meaningful owner, listed once with owner null.
			var groupOrder = new List<string>();
			var groups = new Dictionary<string, (MarkerData marker, string explicitOwnerKey, List<string> sourcePlayers)>();

			foreach (KnownPlayer kp in players)
			{
				// Cached, and pre-filtered to manually-placed markers - see
				// PlayerSaveReader's extraction and PlayerSaveCache.
				PlayerSaveData save = PlayerSaveCache.Get(playerDir, kp.FileBaseName);

				foreach (MarkerData marker in save.Markers)
				{
					string groupKey = marker.RawName + "|" + marker.Icon + "|" + marker.X + "," + marker.Y + "," + marker.Z;
					if (!groups.TryGetValue(groupKey, out var group))
					{
						group = (marker, null, new List<string>());
						groupOrder.Add(groupKey);
					}
					if (marker.OwnerKey != null)
					{
						// An explicit ownerId (invite-copy) is authoritative for the whole group.
						group.explicitOwnerKey = marker.OwnerKey;
					}
					else if (!group.sourcePlayers.Contains(kp.FileBaseName))
					{
						group.sourcePlayers.Add(kp.FileBaseName);
					}
					groups[groupKey] = group;
				}
			}

			var markers = new List<(MarkerData marker, string ownerKey)>();
			foreach (string groupKey in groupOrder)
			{
				var group = groups[groupKey];
				string ownerKey = group.explicitOwnerKey
					?? (group.sourcePlayers.Count == 1 ? group.sourcePlayers[0] : null);
				markers.Add((group.marker, ownerKey));
			}

			JsonWriter writer;
			PrepareEnvelopedResult(out writer);
			writer.WriteBeginObject();
			writer.WritePropertyName("markers");
			writer.WriteBeginArray();

			bool first = true;
			foreach ((MarkerData marker, string ownerKey) in markers)
			{
				if (!first)
				{
					writer.WriteValueSeparator();
				}
				first = false;

				string name = marker.RawName;
				// NameIsLocalizationId means RawName is a localization key, not literal display
				// text. Resolved here at response time, not at extraction, so cached entries
				// never bake in localization state.
				if (marker.NameIsLocalizationId && !string.IsNullOrEmpty(name))
				{
					name = Localization.Get(name, false);
				}

				writer.WriteBeginObject();
				writer.WritePropertyName("name");
				writer.WriteString(string.IsNullOrEmpty(name) ? "(unnamed marker)" : name);
				writer.WriteValueSeparator();
				writer.WritePropertyName("icon");
				writer.WriteString(marker.Icon);
				writer.WriteValueSeparator();
				writer.WritePropertyName("owner");
				// ownerKey null = game-generated marker present in several saves - owner is null in
				// the response (the frontend already renders that as its neutral/unowned style).
				string ownerDisplayName = null;
				if (ownerKey != null && !displayNameByOwnerKey.TryGetValue(ownerKey, out ownerDisplayName))
				{
					// A marker copied from a player we have no save/players.xml entry for -
					// show the raw key rather than nothing.
					ownerDisplayName = ownerKey;
				}
				writer.WriteString(ownerDisplayName);
				writer.WriteValueSeparator();
				writer.WritePropertyName("position");
				writer.WriteBeginObject();
				writer.WritePropertyName("x");
				writer.WriteInt32(marker.X);
				writer.WriteValueSeparator();
				writer.WritePropertyName("y");
				writer.WriteInt32(marker.Y);
				writer.WriteValueSeparator();
				writer.WritePropertyName("z");
				writer.WriteInt32(marker.Z);
				writer.WriteEndObject();
				writer.WriteEndObject();
			}

			writer.WriteEndArray();
			writer.WriteEndObject();
			SendEnvelopedResult(_context, ref writer);
		}

		// "Anyone reads" per design doc §2.
		public override int DefaultPermissionLevel()
		{
			return 1000;
		}
	}
}
