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

			var seen = new HashSet<string>();
			var markers = new List<(MarkerData marker, string ownerKey)>();

			foreach (KnownPlayer kp in players)
			{
				// Cached, and pre-filtered to manually-placed markers - see
				// PlayerSaveReader's extraction and PlayerSaveCache.
				PlayerSaveData save = PlayerSaveCache.Get(playerDir, kp.FileBaseName);

				foreach (MarkerData marker in save.Markers)
				{
					// A player's own markers carry no ownerId in their own save (confirmed live:
					// every marker on a real server came back owner-less) - ownerId is only set on
					// copies received via a waypoint invite. So a null OwnerKey means "owned by the
					// player whose save this is": attribute it to the save it came from instead of
					// falling into an "unknown" bucket that collapses the per-owner coloring.
					string ownerKey = marker.OwnerKey ?? kp.FileBaseName;
					string dedupeKey = ownerKey + "|" + marker.X + "," + marker.Y + "," + marker.Z + "|" + marker.RawName;
					if (!seen.Add(dedupeKey))
					{
						continue;
					}

					markers.Add((marker, ownerKey));
				}
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
				if (!displayNameByOwnerKey.TryGetValue(ownerKey, out string ownerDisplayName))
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
