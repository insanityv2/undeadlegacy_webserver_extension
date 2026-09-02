using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace UndeadLegacyPanels
{
	public class RecipeInfo
	{
		public string Name;
		public bool AlwaysUnlocked;
	}

	/// <summary>
	/// Parses Undead Legacy's Config/recipes.xml once and caches the result - the canonical list
	/// of every valid recipe name, needed so the search UI can tell "no player has this" apart
	/// from "not a real recipe name" (design doc discussion). Same two-tier unlock pattern as
	/// ResearchTreeConfig: recipes with always_unlocked="true" never appear in any player's
	/// PlayerDataFile.unlockedRecipeList (confirmed by the identical naming convention to
	/// research's unlocked="true"), so the per-player "known recipes" set is the union of this
	/// class's AlwaysUnlocked set and PlayerSaveData.UnlockedRecipes, exactly like research nodes.
	/// </summary>
	public static class RecipeConfig
	{
		private static List<RecipeInfo> _recipes;
		private static readonly object _lock = new object();

		public static List<RecipeInfo> GetAllRecipes()
		{
			if (_recipes == null)
			{
				lock (_lock)
				{
					if (_recipes == null)
					{
						_recipes = Parse();
					}
				}
			}
			return _recipes;
		}

		public static HashSet<string> GetAlwaysUnlockedNames()
		{
			return new HashSet<string>(
				GetAllRecipes().Where(r => r.AlwaysUnlocked).Select(r => r.Name),
				StringComparer.OrdinalIgnoreCase);
		}

		public static HashSet<string> GetAllNames()
		{
			return new HashSet<string>(GetAllRecipes().Select(r => r.Name), StringComparer.OrdinalIgnoreCase);
		}

		private static List<RecipeInfo> Parse()
		{
			var result = new List<RecipeInfo>();
			string path = UndeadLegacyPaths.GetRecipesXmlPath();
			try
			{
				if (!System.IO.File.Exists(path))
				{
					Log.Warning("[UndeadLegacyPanels] recipes.xml not found at " + path);
					return result;
				}

				XDocument doc = XDocument.Load(path);
				foreach (XElement el in doc.Descendants("recipe"))
				{
					string name = (string)el.Attribute("name");
					if (string.IsNullOrEmpty(name))
					{
						continue;
					}
					result.Add(new RecipeInfo
					{
						Name = name,
						AlwaysUnlocked = string.Equals((string)el.Attribute("always_unlocked"), "true", StringComparison.OrdinalIgnoreCase),
					});
				}
			}
			catch (Exception ex)
			{
				Log.Warning("[UndeadLegacyPanels] Failed parsing recipes.xml: " + ex.Message);
			}
			return result;
		}
	}
}
