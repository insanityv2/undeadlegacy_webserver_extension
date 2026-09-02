using System.Collections.Generic;
using System.IO;

namespace UndeadLegacyPanels
{
	/// <summary>
	/// Mtime+size-keyed cache over PlayerSaveReader.Load. Every API endpoint previously
	/// re-parsed every known player's .ttp on every request - the Player List panel alone
	/// triggers two endpoints per poll and SharedMarkers a third, so one browser tab cost three
	/// full passes over every save file every five minutes, growing linearly with both player
	/// count and open dashboards. A .ttp only changes when the game writes it (autosave floor
	/// ~1 minute), so mtime+length is a sound freshness key.
	///
	/// The lock intentionally covers the load: concurrent requests for the same stale entry
	/// parse once and share the result, instead of stampeding. Entries are only ever replaced
	/// (players are never removed from a save), so the cache is bounded by the known-player set.
	/// </summary>
	public static class PlayerSaveCache
	{
		private class Entry
		{
			public long MtimeTicks;
			public long Length;
			public PlayerSaveData Data;
		}

		private static readonly Dictionary<string, Entry> _cache = new Dictionary<string, Entry>(System.StringComparer.OrdinalIgnoreCase);
		private static readonly object _lock = new object();

		public static PlayerSaveData Get(string playerDir, string fileBaseName)
		{
			var fileInfo = new FileInfo(Path.Combine(playerDir, fileBaseName + ".ttp"));
			if (!fileInfo.Exists)
			{
				// Nothing to key freshness on - fall through to the reader (which logs and
				// returns an empty result) without polluting the cache.
				return PlayerSaveReader.Load(playerDir, fileBaseName);
			}

			lock (_lock)
			{
				if (_cache.TryGetValue(fileBaseName, out Entry entry)
					&& entry.MtimeTicks == fileInfo.LastWriteTimeUtc.Ticks
					&& entry.Length == fileInfo.Length)
				{
					return entry.Data;
				}

				PlayerSaveData data = PlayerSaveReader.Load(playerDir, fileBaseName);
				_cache[fileBaseName] = new Entry
				{
					MtimeTicks = fileInfo.LastWriteTimeUtc.Ticks,
					Length = fileInfo.Length,
					Data = data,
				};
				return data;
			}
		}
	}
}
