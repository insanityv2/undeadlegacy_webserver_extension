using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace UndeadLegacyPanels
{
	public class ResearchNode
	{
		public string Name;
		public string Area;
		public bool AlwaysUnlocked;
	}

	/// <summary>
	/// Parses Undead Legacy's own Config/Custom/recipes_research.xml once and caches the result.
	/// This is the static, config-driven half of the research-unlock union described in the
	/// design doc §6/§7 - the set of nodes with unlocked="true" is identical for every player and
	/// never appears in any save file (confirmed against ULM_ResearchManager/ULM_Research source),
	/// so it only needs parsing once per server run, not per request.
	/// </summary>
	public static class ResearchTreeConfig
	{
		private static List<ResearchNode> _nodes;
		private static readonly object _lock = new object();

		public static List<ResearchNode> GetAllNodes()
		{
			if (_nodes == null)
			{
				lock (_lock)
				{
					if (_nodes == null)
					{
						_nodes = Parse();
					}
				}
			}
			return _nodes;
		}

		public static HashSet<string> GetAlwaysUnlockedNames()
		{
			return new HashSet<string>(
				GetAllNodes().Where(n => n.AlwaysUnlocked).Select(n => n.Name),
				StringComparer.OrdinalIgnoreCase);
		}

		private static List<ResearchNode> Parse()
		{
			var result = new List<ResearchNode>();
			string path = UndeadLegacyPaths.GetResearchTreeXmlPath();
			try
			{
				if (!System.IO.File.Exists(path))
				{
					Log.Warning("[UndeadLegacyPanels] recipes_research.xml not found at " + path);
					return result;
				}

				XDocument doc = XDocument.Load(path);
				foreach (XElement el in doc.Descendants("research"))
				{
					string name = (string)el.Attribute("name");
					if (string.IsNullOrEmpty(name))
					{
						continue;
					}
					result.Add(new ResearchNode
					{
						Name = name,
						Area = (string)el.Attribute("area") ?? "",
						AlwaysUnlocked = string.Equals((string)el.Attribute("unlocked"), "true", StringComparison.OrdinalIgnoreCase),
					});
				}
			}
			catch (Exception ex)
			{
				Log.Warning("[UndeadLegacyPanels] Failed parsing recipes_research.xml: " + ex.Message);
			}
			return result;
		}
	}
}
