using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace UndeadLegacyPanels
{
	public class PerkBookSeries
	{
		public string Name; // book_group name, e.g. "skillArtOfMining" - what the search UI matches against
		public List<string> VolumeProgressionNames = new List<string>(); // each <book parent="Name"> entry's own name
	}

	/// <summary>
	/// Parses the canonical list of perk/skill book series from Undead Legacy's own
	/// Config/progression.xml. Confirmed live and NOT assumed: a book_group's own progression
	/// level is NOT a "has this player read anything" signal - tested against a fresh level-1
	/// character with zero real progress and ALL 19 book_group entries showed level &gt; 0
	/// identically, while attributes/class/research (parsed the same way) correctly showed 0.
	/// This means book_group's level is some kind of UI-container default (e.g. a MinLevel
	/// backfill from Progression.SetupData()), not tracked reading progress - the same "looks
	/// like the right field, isn't" trap as ulmPlayer.dat's SkillTree dict (design doc §8.3).
	///
	/// The real per-player signal is the individual &lt;book name="..." parent="skillX"&gt;
	/// volume entries (design doc §7's activity-tag scan already had to learn this same lesson for
	/// mining bonuses) - a player has "read" a series if ANY of its volumes has level &gt; 0.
	/// </summary>
	public static class PerkBookConfig
	{
		private static List<PerkBookSeries> _series;
		private static readonly object _lock = new object();

		public static List<PerkBookSeries> GetAllSeries()
		{
			if (_series == null)
			{
				lock (_lock)
				{
					if (_series == null)
					{
						_series = Parse();
					}
				}
			}
			return _series;
		}

		public static HashSet<string> GetAllNames()
		{
			return new HashSet<string>(GetAllSeries().Select(s => s.Name), StringComparer.OrdinalIgnoreCase);
		}

		public static bool HasReadAnyVolume(PlayerSaveData save, PerkBookSeries series)
		{
			foreach (string volumeName in series.VolumeProgressionNames)
			{
				if (save.GetProgressionLevel(volumeName) > 0)
				{
					return true;
				}
			}
			return false;
		}

		private static List<PerkBookSeries> Parse()
		{
			var result = new List<PerkBookSeries>();
			string path = UndeadLegacyPaths.GetProgressionXmlPath();
			try
			{
				if (!System.IO.File.Exists(path))
				{
					Log.Warning("[UndeadLegacyPanels] progression.xml not found at " + path);
					return result;
				}

				XDocument doc = XDocument.Load(path);
				var seriesByName = new Dictionary<string, PerkBookSeries>(StringComparer.OrdinalIgnoreCase);

				foreach (XElement el in doc.Descendants("book_group"))
				{
					string name = (string)el.Attribute("name");
					if (string.IsNullOrEmpty(name) || seriesByName.ContainsKey(name))
					{
						continue;
					}
					var series = new PerkBookSeries { Name = name };
					seriesByName[name] = series;
					result.Add(series);
				}

				foreach (XElement el in doc.Descendants("book"))
				{
					string volumeName = (string)el.Attribute("name");
					string parentName = (string)el.Attribute("parent");
					if (string.IsNullOrEmpty(volumeName) || string.IsNullOrEmpty(parentName))
					{
						continue;
					}
					if (seriesByName.TryGetValue(parentName, out PerkBookSeries series))
					{
						series.VolumeProgressionNames.Add(volumeName);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Warning("[UndeadLegacyPanels] Failed parsing progression.xml for book_group/book entries: " + ex.Message);
			}
			return result;
		}
	}
}
