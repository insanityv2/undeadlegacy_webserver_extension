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
			var markers = new List<Waypoint>();

			foreach (KnownPlayer kp in players)
			{
				PlayerDataFile file = new PlayerDataFile();
				try
				{
					file.Load(playerDir, kp.FileBaseName);
				}
				catch (System.Exception ex)
				{
					Log.Warning("[UndeadLegacyPanels] SharedMarkers load failed for " + kp.FileBaseName + ": " + ex.Message);
					continue;
				}

				if (file.waypoints == null)
				{
					continue;
				}

				foreach (Waypoint wp in file.waypoints.Collection.list)
				{
					if (wp.lastKnownPositionEntityId != -1)
					{
						continue; // auto-tracked (vehicle/drone) - owner-only, not shared
					}

					string ownerKey = wp.ownerId != null ? wp.ownerId.CombinedString : "unknown";
					string dedupeKey = ownerKey + "|" + wp.pos.x + "," + wp.pos.y + "," + wp.pos.z + "|" + (wp.name != null ? wp.name.Text : "");
					if (!seen.Add(dedupeKey))
					{
						continue;
					}

					markers.Add(wp);
				}
			}

			JsonWriter writer;
			PrepareEnvelopedResult(out writer);
			writer.WriteBeginObject();
			writer.WritePropertyName("markers");
			writer.WriteBeginArray();

			bool first = true;
			foreach (Waypoint wp in markers)
			{
				if (!first)
				{
					writer.WriteValueSeparator();
				}
				first = false;

				string name = wp.name != null ? wp.name.Text : "";
				// bUsingLocalizationId means Text is a localization key, not literal display text.
				if (wp.bUsingLocalizationId && !string.IsNullOrEmpty(name))
				{
					name = Localization.Get(name, false);
				}

				writer.WriteBeginObject();
				writer.WritePropertyName("name");
				writer.WriteString(string.IsNullOrEmpty(name) ? "(unnamed marker)" : name);
				writer.WriteValueSeparator();
				writer.WritePropertyName("icon");
				writer.WriteString(wp.icon);
				writer.WriteValueSeparator();
				writer.WritePropertyName("owner");
				string ownerDisplayName = null;
				if (wp.ownerId != null)
				{
					if (!displayNameByOwnerKey.TryGetValue(wp.ownerId.CombinedString, out ownerDisplayName))
					{
						ownerDisplayName = wp.ownerId.ReadablePlatformUserIdentifier;
					}
				}
				writer.WriteString(ownerDisplayName);
				writer.WriteValueSeparator();
				writer.WritePropertyName("position");
				writer.WriteBeginObject();
				writer.WritePropertyName("x");
				writer.WriteInt32(wp.pos.x);
				writer.WriteValueSeparator();
				writer.WritePropertyName("y");
				writer.WriteInt32(wp.pos.y);
				writer.WriteValueSeparator();
				writer.WritePropertyName("z");
				writer.WriteInt32(wp.pos.z);
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
