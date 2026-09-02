namespace UndeadLegacyPanels
{
	/// <summary>
	/// Every XML element name in Undead Legacy's progression.xml/recipes_skills.xml that defines
	/// its own named, individually-tracked progression class (i.e. has a `name` attribute that
	/// shows up as a key in Progression.ProgressionValues). Confirmed by enumerating every
	/// top-level element name actually used in progression.xml, rather than assuming: "progression"
	/// (attributes/UL skill-tree classes), "book" (per-volume magazine entries - book_group itself
	/// is just a UI container, not a real per-player level, see PerkBookConfig), "perk" (UL's
	/// "Action Skill" system - levels via in-game kills/use, e.g. actionPerkBlades/actionPerkMining,
	/// a completely separate mechanism found only after a real report of missing "blade" talent
	/// data), and "skill"/"attribute"/"book_group" included defensively even though current
	/// evidence says they're graph-node/container wrappers rather than tag-bearing classes
	/// themselves - cheap to include, and this is the second time a missed element type silently
	/// produced a wrong (too-low) count instead of an error, so err on the side of scanning more.
	/// </summary>
	public static class ProgressionElementNames
	{
		public static readonly string[] All = { "progression", "book", "perk", "skill", "attribute", "book_group" };
	}
}
