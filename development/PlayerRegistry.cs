using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace UndeadLegacyPanels
{
	public class KnownPlayer
	{
		public string FileBaseName; // e.g. "EOS_0002570c698a48e28d8e2cc520e5be82" - matches Player/<this>.ttp
		public string DisplayName;
		public string Platform;
		public string UserId;
	}

	/// <summary>
	/// Enumerates known players for the active save, using the same GameIO.GetSaveGameDir()
	/// choke point the stock mods use (so it automatically follows Undead Legacy's save-folder
	/// redirect - see the design doc's H_SaveFolderPatch section). Reads players.xml for display
	/// names, falling back to the raw file-name key for any .ttp file that has no matching entry.
	/// </summary>
	public static class PlayerRegistry
	{
		public static string GetPlayerDir()
		{
			return GameIO.GetPlayerDataDir();
		}

		public static List<KnownPlayer> GetKnownPlayers()
		{
			var byKey = new Dictionary<string, KnownPlayer>(StringComparer.OrdinalIgnoreCase);

			string playersXmlPath = Path.Combine(GameIO.GetSaveGameDir(), "players.xml");
			if (File.Exists(playersXmlPath))
			{
				try
				{
					var doc = new XmlDocument();
					doc.Load(playersXmlPath);
					foreach (XmlNode node in doc.SelectNodes("//player"))
					{
						string platform = node.Attributes?["platform"]?.Value;
						string userId = node.Attributes?["userid"]?.Value;
						string name = node.Attributes?["playername"]?.Value;
						if (string.IsNullOrEmpty(platform) || string.IsNullOrEmpty(userId))
						{
							continue;
						}
						string key = platform + "_" + userId;
						byKey[key] = new KnownPlayer
						{
							FileBaseName = key,
							DisplayName = string.IsNullOrEmpty(name) ? key : name,
							Platform = platform,
							UserId = userId,
						};
					}
				}
				catch (Exception ex)
				{
					Log.Warning("[UndeadLegacyPanels] Failed reading players.xml: " + ex.Message);
				}
			}

			string playerDir = GetPlayerDir();
			if (Directory.Exists(playerDir))
			{
				foreach (string ttpPath in Directory.GetFiles(playerDir, "*.ttp"))
				{
					string baseName = Path.GetFileNameWithoutExtension(ttpPath);
					if (!byKey.ContainsKey(baseName))
					{
						// No players.xml entry (shouldn't normally happen for anyone who has
						// actually logged in) - still surface them under their raw file key
						// rather than silently dropping a player from the panel.
						byKey[baseName] = new KnownPlayer
						{
							FileBaseName = baseName,
							DisplayName = baseName,
							Platform = null,
							UserId = null,
						};
					}
				}
			}

			// Sorted deterministically (not left to incidental Dictionary enumeration order,
			// which .NET happens to usually preserve as insertion order but never guarantees) so
			// every consumer of this list - the Player List panel, the map's player widget, and
			// anything future - shows players in the same order without needing to coordinate
			// with each other (design discussion: "order of names should be consistent between
			// the two panels").
			var result = new List<KnownPlayer>(byKey.Values);
			result.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
			return result;
		}
	}
}
