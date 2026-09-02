using System.Collections.Generic;
using System.Linq;
using Utf8Json;
using Webserver;
using Webserver.WebAPI;

namespace UndeadLegacyPanels.Api
{
	/// <summary>
	/// GET /api/researchoverlap - design doc §6, extended to recipes and perk books. Per known
	/// player: unlocked recipes, unlocked UL research-tree nodes, and read perk/skill books - each
	/// using the same "union of always-unlocked config + per-player save data" pattern
	/// (ResearchTreeConfig/RecipeConfig) or, for books, the plain Progression-level check
	/// (PerkBookConfig - books have no always-unlocked concept, every series starts at level 0).
	/// Also exposes the three canonical name lists once at the top level, not per-player - the
	/// frontend's search/autocomplete needs the full valid-name universe to tell "no player has
	/// this" apart from "not a real name" (design discussion), and re-sending it per player would
	/// be wasteful. Auto-discovered by the stock WebServer's ApiHandler via reflection - no manual
	/// route registration needed.
	/// </summary>
	public class ResearchOverlap : AbsRestApi
	{
		public ResearchOverlap()
			: base(null)
		{
		}

		protected override void HandleRestGet(RequestContext _context)
		{
			List<KnownPlayer> players = PlayerRegistry.GetKnownPlayers();
			string playerDir = PlayerRegistry.GetPlayerDir();

			HashSet<string> alwaysUnlockedResearch = ResearchTreeConfig.GetAlwaysUnlockedNames();
			List<string> allNodeNames = ResearchTreeConfig.GetAllNodes().Select(n => n.Name).ToList();

			HashSet<string> alwaysUnlockedRecipes = RecipeConfig.GetAlwaysUnlockedNames();
			List<string> allRecipeNames = RecipeConfig.GetAllRecipes().Select(r => r.Name).ToList();

			List<PerkBookSeries> allBookSeries = PerkBookConfig.GetAllSeries();

			JsonWriter writer;
			PrepareEnvelopedResult(out writer);
			writer.WriteBeginObject();

			writer.WritePropertyName("players");
			writer.WriteBeginArray();

			bool firstPlayer = true;
			foreach (KnownPlayer kp in players)
			{
				if (!firstPlayer)
				{
					writer.WriteValueSeparator();
				}
				firstPlayer = false;

				PlayerSaveData save = PlayerSaveReader.Load(playerDir, kp.FileBaseName);

				var unlockedResearch = new List<string>(alwaysUnlockedResearch);
				foreach (string nodeName in allNodeNames)
				{
					if (!alwaysUnlockedResearch.Contains(nodeName) && save.HasCVar(nodeName))
					{
						unlockedResearch.Add(nodeName);
					}
				}

				var unlockedRecipes = new HashSet<string>(alwaysUnlockedRecipes, System.StringComparer.OrdinalIgnoreCase);
				unlockedRecipes.UnionWith(save.UnlockedRecipes);

				var readBooks = new List<string>();
				foreach (PerkBookSeries series in allBookSeries)
				{
					if (PerkBookConfig.HasReadAnyVolume(save, series))
					{
						readBooks.Add(series.Name);
					}
				}

				writer.WriteBeginObject();
				writer.WritePropertyName("name");
				writer.WriteString(kp.DisplayName);
				writer.WriteValueSeparator();
				writer.WritePropertyName("platformId");
				writer.WriteString(kp.FileBaseName);
				writer.WriteValueSeparator();
				writer.WritePropertyName("unlockedRecipes");
				JsonWriteHelpers.WriteStringArray(ref writer, unlockedRecipes);
				writer.WriteValueSeparator();
				writer.WritePropertyName("unlockedResearch");
				JsonWriteHelpers.WriteStringArray(ref writer, unlockedResearch);
				writer.WriteValueSeparator();
				writer.WritePropertyName("readBooks");
				JsonWriteHelpers.WriteStringArray(ref writer, readBooks);
				writer.WriteEndObject();
			}

			writer.WriteEndArray();
			writer.WriteValueSeparator();

			// Canonical name universes for search/autocomplete validation - not per-player.
			writer.WritePropertyName("allResearchNodes");
			JsonWriteHelpers.WriteStringArray(ref writer, allNodeNames);
			writer.WriteValueSeparator();
			writer.WritePropertyName("allRecipes");
			JsonWriteHelpers.WriteStringArray(ref writer, allRecipeNames);
			writer.WriteValueSeparator();
			writer.WritePropertyName("allPerkBooks");
			JsonWriteHelpers.WriteStringArray(ref writer, allBookSeries.Select(s => s.Name));

			writer.WriteEndObject();

			SendEnvelopedResult(_context, ref writer);
		}

		// "Anyone reads" per design doc §2 - baked in as the compiled-in default rather than
		// requiring a manual serveradmin.xml <webmodules> edit the way the stock Map fix needed.
		public override int DefaultPermissionLevel()
		{
			return 1000;
		}
	}
}
