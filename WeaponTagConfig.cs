using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace UndeadLegacyPanels
{
	/// <summary>
	/// Same technique as ActivityTagConfig (mining/repair/salvage), applied to per-weapon-type
	/// tags found in progression.xml / recipes_skills.xml. Confirmed real tag vocabulary via
	/// direct grep of both files - these aren't guessed names:
	///   melee:  axe, club, knife, knuckles, polearm, sledge, blade
	///   ranged: pistol, shotgun, machineGun, submachine, sniperRifle, bow, crossbow,
	///           energyWeapon, explosive
	/// Deliberately excludes item_modifiers.xml/buffs.xml for the same reason as
	/// ActivityTagConfig: those are gear/consumable-scoped, not persistent talent investment.
	/// </summary>
	public static class WeaponTagConfig
	{
		public static readonly string[] MeleeWeaponTags = { "axe", "club", "knife", "knuckles", "polearm", "sledge", "blade" };
		public static readonly string[] RangedWeaponTags = { "pistol", "shotgun", "machineGun", "submachine", "sniperRifle", "bow", "crossbow", "energyWeapon", "explosive" };

		private static Dictionary<string, HashSet<string>> _table; // weaponTag -> progression class names
		private static readonly object _lock = new object();

		public static HashSet<string> GetProgressionClassesForWeaponTag(string weaponTag)
		{
			EnsureBuilt();
			return _table.TryGetValue(weaponTag, out var set) ? set : new HashSet<string>();
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
				var table = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
				foreach (string tag in MeleeWeaponTags.Concat(RangedWeaponTags))
				{
					table[tag] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				}

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
				Log.Warning("[UndeadLegacyPanels] Weapon-tag source file not found: " + path);
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

			// See ProgressionElementNames - a real report ("I remember blade talents") caught this
			// scan missing <perk> entirely (UL's "Action Skill" perks: actionPerkBlades,
			// actionPerkAxes, actionPerkHandguns, etc. - a comprehensive per-weapon-type tree
			// leveled via in-game kills/use, distinct from both <progression> and <book>).
			foreach (XElement progression in doc.Descendants().Where(el => ProgressionElementNames.All.Contains(el.Name.LocalName)))
			{
				string className = (string)progression.Attribute("name");
				if (string.IsNullOrEmpty(className))
				{
					continue;
				}

				foreach (XElement tagged in progression.Descendants().Where(d => d.Attribute("tags") != null))
				{
					string[] parts = ((string)tagged.Attribute("tags")).Split(',');
					foreach (string part in parts)
					{
						string tag = part.Trim();
						if (table.ContainsKey(tag))
						{
							table[tag].Add(className);
						}
					}
				}
			}
		}
	}
}
