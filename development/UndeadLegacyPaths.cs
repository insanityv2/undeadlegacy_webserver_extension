using System;
using System.IO;

namespace UndeadLegacyPanels
{
	/// <summary>
	/// Locates the Undead Legacy mod's own folder as a sibling of this mod's folder under Mods/,
	/// rather than assuming any particular absolute install path. This mod is explicitly
	/// Undead-Legacy-specific (see design doc §1) so hardcoding the "UndeadLegacy" folder name is
	/// intentional, not a genericness compromise.
	///
	/// Own-mod path comes from IModApi.InitMod's Mod.Path (see ModApi.cs) - NOT from
	/// Assembly.GetExecutingAssembly().Location, which was confirmed live to throw ("Invalid
	/// path") in this mod-loading environment.
	/// </summary>
	public static class UndeadLegacyPaths
	{
		private static string _ownModPath;
		private static string _modsRoot;
		private static string _ulConfigDir;

		public static void SetOwnModPath(string ownModPath)
		{
			_ownModPath = ownModPath;
		}

		public static string GetModsRoot()
		{
			if (_modsRoot == null)
			{
				if (string.IsNullOrEmpty(_ownModPath))
				{
					throw new InvalidOperationException(
						"UndeadLegacyPaths used before ModApi.InitMod ran - own mod path is unknown.");
				}
				_modsRoot = Path.GetDirectoryName(_ownModPath.TrimEnd('\\', '/'));
			}
			return _modsRoot;
		}

		public static string GetUndeadLegacyConfigDir()
		{
			if (_ulConfigDir == null)
			{
				_ulConfigDir = Path.Combine(GetModsRoot(), "UndeadLegacy", "Config");
			}
			return _ulConfigDir;
		}

		public static string GetResearchTreeXmlPath()
		{
			return Path.Combine(GetUndeadLegacyConfigDir(), "Custom", "recipes_research.xml");
		}

		public static string GetSkillTreeXmlPath()
		{
			return Path.Combine(GetUndeadLegacyConfigDir(), "Custom", "recipes_skills.xml");
		}

		public static string GetProgressionXmlPath()
		{
			return Path.Combine(GetUndeadLegacyConfigDir(), "progression.xml");
		}

		public static string GetRecipesXmlPath()
		{
			return Path.Combine(GetUndeadLegacyConfigDir(), "recipes.xml");
		}

		public static string GetUndeadLegacyMapIconsDir()
		{
			return Path.Combine(GetModsRoot(), "UndeadLegacy", "UIAtlases", "UIAtlas");
		}

		// Separate atlas folder (sibling of UIAtlas) holding the 5 gold class-badge sprites used
		// at the skill tree's root nodes (recipes_skills.xml's rootStrength/rootFortitude/etc.
		// "icon"/"alt_icon" attributes point here, atlas="UISkills") - distinct from the plain
		// white/gray attribute icon masks in UIAtlas.
		public static string GetUndeadLegacySkillIconsDir()
		{
			return Path.Combine(GetModsRoot(), "UndeadLegacy", "UIAtlases", "UISkills");
		}
	}
}
