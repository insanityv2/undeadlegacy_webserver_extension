using Webserver;
using Webserver.FileCache;
using Webserver.Permissions;
using Webserver.UrlHandlers;

namespace UndeadLegacyPanels
{
	/// <summary>
	/// Three jobs:
	/// 1. Learn this mod's own on-disk folder via Mod.Path - Assembly.GetExecutingAssembly().Location
	///    threw "Invalid path" when actually tested live, so UndeadLegacyPaths uses the same
	///    Mod.Path the stock mods rely on (see Webserver/ModApi.cs's identical caching pattern).
	/// 2. Serve map tiles under our own route/permission ("web.ulmap", baked to level 1000 -
	///    "anyone reads", no serveradmin.xml edit ever required), instead of touching the stock
	///    "web.map" module the way the original investigation's fix did. This is a deliberate fork
	///    of just the tile-serving path - not the tile *rendering*, which stays entirely owned by
	///    the stock TFP_MapRendering mod; we only read the PNG files it already writes to
	///    &lt;save&gt;/map/. Keeps this mod's redistributable fully self-contained: no dependency
	///    on MapRendering.dll, no required edits to any other mod's config.
	/// 3. Serve Undead Legacy's own map-marker icon sprites (Mods/UndeadLegacy/UIAtlases/UIAtlas/
	///    symbol_map_*.png - confirmed real loose PNG files, not baked into an inaccessible Unity
	///    asset bundle) under our own route too, so shared-marker widgets/pins can show the real
	///    in-game icon a player actually picked, not a generic pin. Same self-contained pattern -
	///    serves the whole UIAtlas folder generically rather than filtering to just symbol_map_*,
	///    since it's all public, non-sensitive icon artwork and other UI sprites there may be
	///    useful for future panels too.
	/// 4. Serve Undead Legacy's separate UISkills atlas (Mods/UndeadLegacy/UIAtlases/UISkills/
	///    symbol_class_*.png) - the 5 gold class-badge icons used at the skill tree's root nodes
	///    (recipes_skills.xml rootStrength/rootFortitude/etc., atlas="UISkills"), distinct from
	///    the plain attribute icon masks in UIAtlas. Confirmed visually against a screenshot of
	///    the in-game skill tree that these, not the attribute icons, are what a player recognizes
	///    as "their class icon".
	/// </summary>
	public class ModApi : IModApi
	{
		private const string MapModuleName = "web.ulmap";
		private const string MapUrlPath = "/ulmap/";
		private const string IconsModuleName = "web.ulmapicons";
		private const string IconsUrlPath = "/ulmapicons/";
		private const string SkillIconsModuleName = "web.ulskillicons";
		private const string SkillIconsUrlPath = "/ulskillicons/";

		public void InitMod(Mod _modInstance)
		{
			UndeadLegacyPaths.SetOwnModPath(_modInstance.Path);
			Web.ServerInitialized += OnWebServerInitialized;
		}

		private void OnWebServerInitialized(Web _web)
		{
			// StaticHandler's base AbsHandler ctor registers its module at level 0 (admin-only)
			// by default - override immediately after to "anyone reads" per design doc §2.
			_web.RegisterPathHandler(MapUrlPath, new StaticHandler(GameIO.GetSaveGameDir() + "/map", new DirectAccess(), false, MapModuleName));
			AdminWebModules.Instance.AddKnownModule(new AdminWebModules.WebModule(MapModuleName, 1000, _isDefault: true));

			_web.RegisterPathHandler(IconsUrlPath, new StaticHandler(UndeadLegacyPaths.GetUndeadLegacyMapIconsDir(), new DirectAccess(), false, IconsModuleName));
			AdminWebModules.Instance.AddKnownModule(new AdminWebModules.WebModule(IconsModuleName, 1000, _isDefault: true));

			_web.RegisterPathHandler(SkillIconsUrlPath, new StaticHandler(UndeadLegacyPaths.GetUndeadLegacySkillIconsDir(), new DirectAccess(), false, SkillIconsModuleName));
			AdminWebModules.Instance.AddKnownModule(new AdminWebModules.WebModule(SkillIconsModuleName, 1000, _isDefault: true));
		}
	}
}
