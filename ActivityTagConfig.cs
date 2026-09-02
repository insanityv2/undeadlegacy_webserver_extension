using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace UndeadLegacyPanels
{
	/// <summary>
	/// Builds the "which progression-class names carry a talent affecting this activity" table
	/// described in design doc §7, by parsing XML rather than hand-enumerating nodes. Deliberately
	/// scans only <progression> blocks in progression.xml and recipes_skills.xml - not
	/// item_modifiers.xml (item/gear attachment mods) or buffs.xml (temporary buff-gating
	/// conditions), since both of those are gear/consumable-scoped, not persistent talent
	/// investment, and the whole point of this estimate is to read only the latter (see design
	/// doc §7's EffectManager.GetValue finding: gear and buffs are transient inputs blended at
	/// use-time, not persistent player traits).
	/// </summary>
	public static class ActivityTagConfig
	{
		private static readonly string[] MiningTags = { "miningTool" };
		private static readonly string[] RepairTags = { "repairTool" };
		private static readonly string[] SalvageTags = { "salvageTool", "salvageHarvest" };

		private static Dictionary<string, HashSet<string>> _table; // activity -> progression class names
		private static readonly object _lock = new object();

		public static HashSet<string> GetProgressionClassesForActivity(string activity)
		{
			EnsureBuilt();
			return _table.TryGetValue(activity, out var set) ? set : new HashSet<string>();
		}

		private static void EnsureBuilt()
		{
			if (_table != null)
			{
				return;
			}
			lock (_lock)
			{
				if (_table != null)
				{
					return;
				}
				var table = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
				{
					["mining"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
					["repair"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
					["salvage"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
				};

				foreach (string path in new[] { UndeadLegacyPaths.GetProgressionXmlPath(), UndeadLegacyPaths.GetSkillTreeXmlPath() })
				{
					ScanFile(path, table);
				}

				_table = table;
			}
		}

		private static void ScanFile(string path, Dictionary<string, HashSet<string>> table)
		{
			if (!System.IO.File.Exists(path))
			{
				Log.Warning("[UndeadLegacyPanels] Activity-tag source file not found: " + path);
				return;
			}

			XDocument doc;
			try
			{
				doc = XDocument.Load(path);
			}
			catch (Exception ex)
			{
				Log.Warning("[UndeadLegacyPanels] Failed parsing " + path + ": " + ex.Message);
				return;
			}

			// Scans every confirmed named-progression-class element type (ProgressionElementNames)
			// - a real player report ("I remember blade talents") caught that this scan was
			// missing <perk> entirely (UL's "Action Skill" perks, leveled via in-game kills/use,
			// e.g. actionPerkBlades/actionPerkMining) - a second missed element type after <book>,
			// so this now scans the full confirmed set rather than cherry-picking.
			foreach (XElement progression in doc.Descendants().Where(el => ProgressionElementNames.All.Contains(el.Name.LocalName)))
			{
				string className = (string)progression.Attribute("name");
				if (string.IsNullOrEmpty(className))
				{
					continue;
				}

				// Any passive_effect/requirement anywhere under this progression/book block
				// carrying one of the target tags counts this whole class as activity-relevant.
				var taggedDescendants = progression.Descendants()
					.Where(d => d.Attribute("tags") != null);

				foreach (XElement tagged in taggedDescendants)
				{
					string tagsValue = (string)tagged.Attribute("tags");
					AddIfTagged(table, "mining", className, tagsValue, MiningTags);
					AddIfTagged(table, "repair", className, tagsValue, RepairTags);
					AddIfTagged(table, "salvage", className, tagsValue, SalvageTags);
				}
			}
		}

		private static void AddIfTagged(Dictionary<string, HashSet<string>> table, string activity, string className, string tagsValue, string[] targetTags)
		{
			if (string.IsNullOrEmpty(tagsValue))
			{
				return;
			}
			// tags attributes are comma-separated lists (e.g. "melee,salvageTool") - split rather
			// than substring-match, to avoid false positives against unrelated tag names.
			string[] parts = tagsValue.Split(',');
			foreach (string tag in targetTags)
			{
				if (parts.Any(p => string.Equals(p.Trim(), tag, StringComparison.OrdinalIgnoreCase)))
				{
					table[activity].Add(className);
					return;
				}
			}
		}
	}
}
