using System.Collections.Generic;

namespace UndeadLegacyPanels
{
	/// <summary>
	/// Fixed progression-class names confirmed against Mods/UndeadLegacy/Config/Custom/recipes_skills.xml
	/// (design doc §7). These are structural to Undead Legacy's skill tree and don't need runtime
	/// XML parsing - they're the five base attributes, the five mutually-exclusive starting-class
	/// root nodes, and the two lockpick progression classes.
	/// </summary>
	public static class SkillTreeConstants
	{
		public static readonly string[] Attributes = { "Strength", "Fortitude", "Perception", "Dexterity", "Intellect" };

		// Progression class name -> display name, from each class's own description text
		// (recipes_skills.xml progression `desc` key opens with "[decea3]N A M E[-]").
		public static readonly Dictionary<string, string> ClassDisplayNames = new Dictionary<string, string>
		{
			{ "skillTreeStrengthClass", "Assault" },
			{ "skillTreeFortitudeClass", "Enforcer" },
			{ "skillTreeDexterityClass", "Recon" },
			{ "skillTreePerceptionClass", "Scout" },
			{ "skillTreeIntellectClass", "Specialist" },
		};

		public const string LockPickBaseProgression = "skillTreeLockPickingBase"; // max_level 1
		public const string LockPickProgression = "skillTreeLockPicking"; // max_level 4
	}
}
