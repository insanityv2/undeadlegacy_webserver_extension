// Undead Legacy Dashboard Panels - WebMod bundle.
//
// Loaded by the stock dashboard as a plain <script> tag (see Webserver/WebMod.cs and
// lib/useExternalScripts.js in the recovered dashboard source - design doc §4), NOT as an ES
// module. Must register itself on window under the exact ModInfo.xml <Name> ("UndeadLegacyPanels").
// React/HTTP/etc. arrive as props on each route component rather than via import, since the host
// page supplies its own single React instance (see hydrateModComponent in lib/mods.js) - do not
// import React here.
(function () {
	function useJsonEndpoint(React, HTTP, url) {
		var dataState = React.useState(null);
		var data = dataState[0];
		var setData = dataState[1];

		var errorState = React.useState(null);
		var error = errorState[0];
		var setError = errorState[1];

		React.useEffect(function () {
			var cancelled = false;
			HTTP.get(url)
				.then(function (result) {
					if (!cancelled) {
						setData(result);
					}
				})
				.catch(function (e) {
					if (!cancelled) {
						setError(e && e.message ? e.message : String(e));
					}
				});
			return function () {
				cancelled = true;
			};
			// eslint-disable-next-line react-hooks/exhaustive-deps
		}, [url]);

		return { data: data, error: error };
	}

	// Presentation-only display names for the raw tag/key names the backend returns - kept on
	// the frontend deliberately, since the backend stays generic/data-only (design comment in
	// Api/BuildCoordination.cs) and these are a small, finite, hand-known set (unlike
	// recipe/research/book names, which are open-ended and come from the canonical lists instead).
	var DISPLAY_NAMES = {
		mining: 'Mining',
		salvage: 'Salvage',
		lockpick: 'Lockpick',
		axe: 'Axe',
		club: 'Club',
		knife: 'Knife',
		knuckles: 'Knuckles',
		polearm: 'Polearm',
		sledge: 'Sledgehammer',
		blade: 'Blade',
		pistol: 'Pistol',
		shotgun: 'Shotgun',
		machineGun: 'Machine Gun',
		submachine: 'SMG',
		sniperRifle: 'Sniper Rifle',
		bow: 'Bow',
		crossbow: 'Crossbow',
		energyWeapon: 'Energy Weapon',
		explosive: 'Explosives',
	};

	// Starting-class badge icons - the actual gold class badges from UL's own skill tree root
	// nodes (Config/Custom/recipes_skills.xml rootStrength/rootFortitude/rootDexterity/
	// rootPerception/rootIntellect entries, atlas="UISkills"), confirmed against a screenshot of
	// the real in-game skill tree - NOT the plain attribute icon masks (muscle/heart/eye/brain in
	// UIAtlas) tried first, which are a different, uncolored asset the game tints at runtime.
	// Class name -> {Strength: Assault, Fortitude: Enforcer, Dexterity: Recon, Perception: Scout,
	// Intellect: Specialist} per SkillTreeConstants.cs.
	//
	// Important gotcha found while verifying: the game's own internal sprite filenames don't
	// match the display class names - "symbol_class_scout" is Dexterity/Recon's badge, and
	// "symbol_class_ranger" is Perception/Scout's badge (renamed at some point in UL's dev
	// history without renaming the art files). Mapped below by progression key via
	// SkillTreeConstants, not by guessing from filename similarity.
	var CLASS_ICON_MAP = {
		Assault: { icon: 'symbol_class_warrior' },
		Enforcer: { icon: 'symbol_class_tank' },
		Recon: { icon: 'symbol_class_scout' },
		Scout: { icon: 'symbol_class_ranger' },
		Specialist: { icon: 'symbol_class_specialist' },
	};

	// Renders the class as a small badge icon instead of a text label (design ask: more compact,
	// and use the real in-game class badge now that we're already serving game sprites for map
	// markers). The class name is still available as a native title-attribute tooltip on hover
	// rather than a permanently-visible column label.
	function renderClassIcon(React, className) {
		var info = CLASS_ICON_MAP[className];
		if (!info) {
			return React.createElement(
				'span',
				{ className: 'ul-class-icon-empty', title: className || 'No class selected' },
				'—'
			);
		}
		return React.createElement(
			'span',
			{ className: 'ul-class-icon', title: className },
			React.createElement('img', { src: '/ulskillicons/' + info.icon + '.png', alt: className })
		);
	}

	// Ranks a {name: {score, maxPossible}} group, drops zero-score entries (never show a "0
	// wins"), and returns the top two by score descending. Ties beyond the top two are resolved
	// by object key iteration order - not accommodated further in the UI per design discussion
	// ("shouldn't crash anything but we aren't accommodating it").
	function pickFavored(group) {
		var entries = Object.keys(group || {})
			.map(function (key) {
				return { name: key, score: group[key].score };
			})
			.filter(function (entry) {
				return entry.score > 0;
			})
			.sort(function (a, b) {
				return b.score - a.score;
			});
		return entries.slice(0, 2);
	}

	function favoredCell(React, group) {
		var favored = pickFavored(group);
		if (favored.length === 0) {
			return React.createElement('td', { className: 'ul-favored-empty' }, '—');
		}
		return React.createElement(
			'td',
			null,
			favored.map(function (entry, index) {
				var label = DISPLAY_NAMES[entry.name] || entry.name;
				return React.createElement(
					'div',
					{ key: entry.name, className: index === 0 ? 'ul-favored-first' : 'ul-favored-second' },
					label
				);
			})
		);
	}

	var CATEGORY_LABELS = { research: 'Research Node', recipe: 'Recipe', book: 'Perk Book' };

	// Flattens the three canonical name lists (design doc discussion: exposed once, not per
	// player, specifically so the search box can tell "no player has this" apart from "not a
	// real name") into one array for the datalist and exact-match validation.
	function buildSearchIndex(overlapData) {
		var index = [];
		(overlapData.allResearchNodes || []).forEach(function (name) {
			index.push({ name: name, category: 'research' });
		});
		(overlapData.allRecipes || []).forEach(function (name) {
			index.push({ name: name, category: 'recipe' });
		});
		(overlapData.allPerkBooks || []).forEach(function (name) {
			index.push({ name: name, category: 'book' });
		});
		return index;
	}

	function findExactMatch(searchIndex, text) {
		var normalized = text.trim().toLowerCase();
		if (!normalized) {
			return null;
		}
		for (var i = 0; i < searchIndex.length; i++) {
			if (searchIndex[i].name.toLowerCase() === normalized) {
				return searchIndex[i];
			}
		}
		return null;
	}

	function playerMatchesChip(playerOverlap, chip) {
		if (!chip || !playerOverlap) {
			return false;
		}
		var list =
			chip.category === 'research'
				? playerOverlap.unlockedResearch
				: chip.category === 'recipe'
				? playerOverlap.unlockedRecipes
				: playerOverlap.readBooks;
		return !!list && list.indexOf(chip.name) !== -1;
	}

	// Merged "Player List" panel (was two separate panels - Research/Recipe Overlap and Build
	// Coordination - consolidated per design discussion: the search/highlight mechanism made a
	// standalone research-node browsing table unnecessary, and both panels show the same player
	// roster anyway). Visual language, as specified:
	//   - excluded from the list entirely = player is offline (never shown, not even greyed)
	//   - greyed out = player IS online, but doesn't match the currently active search
	//   - normal/highlighted = matches the active search, or no search is active
	function PlayerListPanel(props) {
		var React = props.React;
		var HTTP = props.HTTP;

		var dataState = React.useState(null); // { players: [{coordination, overlap}], searchIndex }
		var data = dataState[0];
		var setData = dataState[1];

		var errorState = React.useState(null);
		var error = errorState[0];
		var setError = errorState[1];

		var searchTextState = React.useState('');
		var searchText = searchTextState[0];
		var setSearchText = searchTextState[1];

		var chipState = React.useState(null); // {name, category} | null
		var chip = chipState[0];
		var setChip = chipState[1];

		var validationErrorState = React.useState(null);
		var validationError = validationErrorState[0];
		var setValidationError = validationErrorState[1];

		React.useEffect(function () {
			var cancelled = false;

			// Refetches both endpoints and replaces `data` in place - deliberately doesn't touch
			// `chip`/`searchText`, which are separate state, so an active search survives a
			// background refresh instead of getting cleared out from under the user.
			function fetchData() {
				Promise.all([HTTP.get('/api/buildcoordination'), HTTP.get('/api/researchoverlap')])
					.then(function (results) {
						if (cancelled) {
							return;
						}
						var coordination = results[0];
						var overlap = results[1];

						var overlapByPlatformId = {};
						overlap.players.forEach(function (p) {
							overlapByPlatformId[p.platformId] = p;
						});

						var merged = coordination.players
							.filter(function (p) {
								return p.online;
							})
							.map(function (p) {
								return {
									coordination: p,
									overlap: overlapByPlatformId[p.platformId] || {
										unlockedRecipes: [],
										unlockedResearch: [],
										readBooks: [],
									},
								};
							});

						setData({
							players: merged,
							searchIndex: buildSearchIndex(overlap),
						});
					})
					.catch(function (e) {
						if (!cancelled) {
							setError(e && e.message ? e.message : String(e));
						}
					});
			}

			fetchData();
			// 5 minutes: sits comfortably above the game's own ~1 minute autosave floor (design
			// discussion) - refreshing faster couldn't show anything newer, just adds load.
			var intervalId = setInterval(fetchData, 5 * 60 * 1000);

			return function () {
				cancelled = true;
				clearInterval(intervalId);
			};
			// eslint-disable-next-line react-hooks/exhaustive-deps
		}, []);

		function commitSearch() {
			if (!data) {
				return;
			}
			var match = findExactMatch(data.searchIndex, searchText);
			if (match) {
				setChip(match);
				setSearchText('');
				setValidationError(null);
			} else {
				setValidationError(
					'No research node, recipe, or perk book named "' + searchText.trim() + '". Pick a suggestion from the list.'
				);
			}
		}

		function clearChip() {
			setChip(null);
			setValidationError(null);
		}

		if (error) {
			return React.createElement('div', null, 'Failed to load: ' + error);
		}
		if (!data) {
			return React.createElement('div', null, 'Loading...');
		}

		var searchBar = chip
			? React.createElement(
					'div',
					{ className: 'ul-search-chip' },
					React.createElement('span', null, CATEGORY_LABELS[chip.category] + ': ' + chip.name),
					React.createElement('button', { onClick: clearChip, className: 'ul-chip-clear', type: 'button' }, '×')
			  )
			: React.createElement(
					'div',
					null,
					React.createElement('input', {
						type: 'text',
						list: 'ul-search-datalist',
						value: searchText,
						placeholder: 'Search a research node, recipe, or perk book...',
						className: 'ul-search-input',
						onChange: function (e) {
							setSearchText(e.target.value);
							setValidationError(null);
						},
						onKeyDown: function (e) {
							if (e.key === 'Enter') {
								commitSearch();
							}
						},
					}),
					React.createElement(
						'datalist',
						{ id: 'ul-search-datalist' },
						data.searchIndex.map(function (entry) {
							return React.createElement('option', { key: entry.category + ':' + entry.name, value: entry.name });
						})
					),
					validationError ? React.createElement('div', { className: 'ul-search-error' }, validationError) : null
			  );

		var rows = data.players.map(function (row) {
			var p = row.coordination;
			var matches = chip ? playerMatchesChip(row.overlap, chip) : true;
			var rowClass = chip ? (matches ? 'ul-row-match' : 'ul-row-nomatch') : '';
			return React.createElement(
				'tr',
				{ key: p.platformId, className: rowClass },
				React.createElement('td', null, p.name),
				React.createElement('td', null, p.level),
				React.createElement('td', { className: 'ul-class-cell' }, renderClassIcon(React, p.class)),
				favoredCell(React, p.meleeWeapons),
				favoredCell(React, p.rangedWeapons),
				favoredCell(React, p.resourceSkills)
			);
		});

		var noneOnlineNotice =
			data.players.length === 0 ? React.createElement('p', null, 'No players are currently online.') : null;

		var allGreyed =
			chip &&
			data.players.length > 0 &&
			data.players.every(function (row) {
				return !playerMatchesChip(row.overlap, chip);
			});
		var noMatchNotice = allGreyed
			? React.createElement('p', { className: 'ul-no-match-notice' }, 'No online player currently has this.')
			: null;

		return React.createElement(
			'div',
			null,
			React.createElement('h1', null, 'Player List'),
			React.createElement(
				'p',
				null,
				"Shows currently online players only. Search a research node, recipe, or perk book to highlight " +
					"who has it — greyed rows don't match the active search."
			),
			searchBar,
			noMatchNotice,
			noneOnlineNotice,
			data.players.length > 0
				? React.createElement(
						'table',
						null,
						React.createElement(
							'thead',
							null,
							React.createElement(
								'tr',
								null,
								React.createElement('th', null, 'Player'),
								React.createElement('th', null, 'Level'),
								React.createElement('th', { title: 'Class', className: 'ul-class-cell' }, ''),
								React.createElement('th', null, 'Favored Melee Weapon'),
								React.createElement('th', null, 'Favored Ranged Weapon'),
								React.createElement('th', null, 'Favored Resource Skill')
							)
						),
						React.createElement('tbody', null, rows)
				  )
				: null
		);
	}

	// Forked map viewer: reads tiles from our own /ulmap/ route (ModApi.cs) rather than the
	// stock /map/ route, so this panel never depends on the "web.map" module's permission level.
	// Tile-coordinate scheme (Y-flip, tileSize, zoom bounds) matches the stock
	// components/map/tileLayer.js exactly (recovered from the dashboard's own source map) -
	// only the base tile URL differs. Uses vanilla Leaflet (loaded from CDN, not react-leaflet -
	// that binding isn't available to us since only React itself arrives via props), managed
	// directly against a DOM ref rather than through React's virtual DOM, which is the normal way
	// to embed non-React libraries like Leaflet in a React app.
	var leafletLoadPromise = null;
	function loadLeaflet() {
		if (leafletLoadPromise) {
			return leafletLoadPromise;
		}
		leafletLoadPromise = new Promise(function (resolve, reject) {
			if (window.L) {
				resolve(window.L);
				return;
			}
			var link = document.createElement('link');
			link.rel = 'stylesheet';
			link.href = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.css';
			document.head.appendChild(link);

			var script = document.createElement('script');
			script.src = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.js';
			script.async = true;
			script.onload = function () {
				resolve(window.L);
			};
			script.onerror = function (e) {
				reject(e);
			};
			document.body.appendChild(script);
		});
		return leafletLoadPromise;
	}

	// Ported exactly from the stock pages/Map.js (recovered dashboard source) - NOT the same as
	// plain L.CRS.Simple. World-unit coordinates (player/quest/waypoint positions) only land on
	// the correct pixel if this exact projection is used: it pre-scales by 1/2^maxZoom so that,
	// combined with Leaflet's own zoom-based scale (2^zoom), 1 world unit = 1 pixel at native
	// max zoom, with predictable scaling at every other zoom level too. Plain CRS.Simple's
	// identity projection has no such scaling, so tiles still pan/zoom fine (Leaflet indexes
	// those by integer tile coordinates, independent of CRS), but any marker placed at a real
	// world coordinate would land in the wrong spot without this. Coordinate convention (matches
	// the stock PlayerLayer marker placement exactly): lat = world X, lng = world Z.
	function makeSDTDCRS(L, maxZoom) {
		var SDTD_Projection = {
			project: function (latlng) {
				return L.point(latlng.lat / Math.pow(2, maxZoom), latlng.lng / Math.pow(2, maxZoom));
			},
			unproject: function (point) {
				return L.latLng(point.x * Math.pow(2, maxZoom), point.y * Math.pow(2, maxZoom));
			},
		};
		return L.extend({}, L.CRS.Simple, {
			projection: SDTD_Projection,
			transformation: new L.Transformation(1, 0, -1, 0),
			scale: function (zoom) {
				return Math.pow(2, zoom);
			},
		});
	}

	function ULMapPanel(props) {
		var React = props.React;
		var HTTP = props.HTTP;
		var containerRef = React.useRef(null);
		var mapRef = React.useRef(null);

		var errorState = React.useState(null);
		var error = errorState[0];
		var setError = errorState[1];

		// Player widget: a simplified view (name/level/class only) of the same data the Player
		// List panel shows, fetched independently from the same /api/buildcoordination endpoint -
		// the two panels don't reference each other (design discussion), but PlayerRegistry sorts
		// deterministically now, so both naturally show players in the same order without needing
		// to coordinate. Clicking a name centers the map on that player's live position.
		var widgetPlayersState = React.useState([]);
		var widgetPlayers = widgetPlayersState[0];
		var setWidgetPlayers = widgetPlayersState[1];

		React.useEffect(function () {
			var cancelled = false;

			function fetchWidgetPlayers() {
				HTTP.get('/api/buildcoordination')
					.then(function (result) {
						if (!cancelled) {
							setWidgetPlayers(result.players.filter(function (p) { return p.online; }));
						}
					})
					.catch(function () {
						// Widget is secondary to the map itself - fail quietly rather than
						// surfacing a second error UI alongside the map's own.
					});
			}

			fetchWidgetPlayers();
			var intervalId = setInterval(fetchWidgetPlayers, 5 * 60 * 1000);
			return function () {
				cancelled = true;
				clearInterval(intervalId);
			};
			// eslint-disable-next-line react-hooks/exhaustive-deps
		}, []);

		function centerOnPlayer(p) {
			if (mapRef.current && p.position) {
				mapRef.current.panTo([p.position.x, p.position.z]);
			}
		}

		// Shared markers widget: same pattern as the player widget, fetched independently from
		// /api/sharedmarkers (player-placed pins visible to everyone by the game's own rule -
		// see SharedMarkers.cs). Rendered both as a clickable list here and as actual pins on the
		// map (markersLayerRef), kept in sync by a separate effect below once both the map
		// instance and the marker data are available (they load independently/asynchronously).
		var widgetMarkersState = React.useState([]);
		var widgetMarkers = widgetMarkersState[0];
		var setWidgetMarkers = widgetMarkersState[1];
		var markersLayerRef = React.useRef(null);
		var leafletRef = React.useRef(null);

		React.useEffect(function () {
			var cancelled = false;

			function fetchWidgetMarkers() {
				HTTP.get('/api/sharedmarkers')
					.then(function (result) {
						if (!cancelled) {
							setWidgetMarkers(result.markers);
						}
					})
					.catch(function () {
						// Secondary to the map itself - fail quietly, same reasoning as players.
					});
			}

			fetchWidgetMarkers();
			var intervalId = setInterval(fetchWidgetMarkers, 5 * 60 * 1000);
			return function () {
				cancelled = true;
				clearInterval(intervalId);
			};
			// eslint-disable-next-line react-hooks/exhaustive-deps
		}, []);

		function centerOnMarker(m) {
			if (mapRef.current) {
				mapRef.current.panTo([m.position.x, m.position.z]);
			}
		}

		// Server is capped at 10 players (design constraint), so a fixed 10-color palette can give
		// every owner a stable, distinct color with no collisions. Assigned by sorted owner name so
		// the mapping stays stable across polls regardless of marker order. Two players who pick the
		// same icon and name for a marker (design concern) are still told apart by both this color
		// and the owner name shown in the list/popup.
		var OWNER_COLOR_PALETTE = [
			'#e6194b', '#3cb44b', '#4363d8', '#f58231', '#911eb4',
			'#42d4f4', '#f032e6', '#bfef45', '#fabed4', '#469990',
		];
		function buildOwnerColorMap(markers) {
			var owners = [];
			markers.forEach(function (m) {
				if (m.owner && owners.indexOf(m.owner) === -1) {
					owners.push(m.owner);
				}
			});
			owners.sort();
			var map = {};
			owners.forEach(function (owner, i) {
				map[owner] = OWNER_COLOR_PALETTE[i % OWNER_COLOR_PALETTE.length];
			});
			return map;
		}

		// Free-text filter for the markers widget (design ask: "jump straight to say 'Crafting base
		// 3' from a long list"). Unlike the Player List panel's search, marker names/owners aren't
		// drawn from a fixed canonical vocabulary - they're whatever the player typed in-game - so
		// this is a live substring filter rather than an exact-match autocomplete. Matches on either
		// name or owner so searching a player's name also works.
		function markerMatchesSearch(m, searchText) {
			if (!searchText) {
				return true;
			}
			var needle = searchText.toLowerCase();
			var name = (m.name || '').toLowerCase();
			var owner = (m.owner || '').toLowerCase();
			return name.indexOf(needle) !== -1 || owner.indexOf(needle) !== -1;
		}

		// mapReady exists so the marker sync below re-fires the moment the map finishes
		// initializing, not just when marker data changes - markers commonly finish loading
		// before the map does (Leaflet CDN load + config fetch both take longer than one JSON
		// call), and without this the sync effect would silently no-op once and then wait for
		// the next 5-minute poll before markers actually appeared.
		var mapReadyState = React.useState(false);
		var mapReady = mapReadyState[0];
		var setMapReady = mapReadyState[1];

		// Syncs widgetMarkers into actual map pins once both the map and Leaflet are ready.
		React.useEffect(function () {
			if (!mapReady || !mapRef.current || !leafletRef.current) {
				return;
			}
			var L = leafletRef.current;
			if (!markersLayerRef.current) {
				markersLayerRef.current = L.layerGroup().addTo(mapRef.current);
			}
			markersLayerRef.current.clearLayers();
			var ownerColors = buildOwnerColorMap(widgetMarkers);
			widgetMarkers.forEach(function (m) {
				var color = m.owner && ownerColors[m.owner] ? ownerColors[m.owner] : '#888888';
				// A DivIcon combining the real in-game sprite (/ulmapicons/<icon>.png, see
				// ModApi.cs) with a colored ring lets the pin show both the exact icon the player
				// picked in-game AND an at-a-glance owner color, without needing any image
				// manipulation of the sprite itself.
				var icon = L.divIcon({
					className: 'ul-map-marker-icon',
					html: '<div class="ul-map-marker-ring" style="border-color: ' + color + ';">' +
						'<img src="/ulmapicons/' + m.icon + '.png" alt="" /></div>',
					iconSize: [34, 34],
					iconAnchor: [17, 17],
					popupAnchor: [0, -17],
				});
				L.marker([m.position.x, m.position.z], { icon: icon })
					.addTo(markersLayerRef.current)
					.bindPopup(m.name + '<br/>Placed by ' + (m.owner || 'unknown'));
			});
		}, [widgetMarkers, mapReady]);

		// Collapsible widgets (design ask): each panel can be hidden independently, collapsing
		// both gives a map-only view without needing a separate dedicated toggle for that.
		var playersCollapsedState = React.useState(false);
		var playersCollapsed = playersCollapsedState[0];
		var setPlayersCollapsed = playersCollapsedState[1];
		var markersCollapsedState = React.useState(false);
		var markersCollapsed = markersCollapsedState[0];
		var setMarkersCollapsed = markersCollapsedState[1];
		var markerSearchTextState = React.useState('');
		var markerSearchText = markerSearchTextState[0];
		var setMarkerSearchText = markerSearchTextState[1];

		React.useEffect(function () {
			var cancelled = false;

			Promise.all([loadLeaflet(), HTTP.get('/api/map/config')])
				.then(function (results) {
					if (cancelled || !containerRef.current) {
						return;
					}
					var L = results[0];
					leafletRef.current = L;
					var mapConfig = results[1];

					if (!mapConfig.enabled) {
						setError('Map rendering is disabled on this server (EnableMapRendering=false in serverconfig.xml).');
						return;
					}

					var tileSize = mapConfig.mapBlockSize;
					var maxZoom = mapConfig.maxZoom;
					var minZoom = Math.max(0, maxZoom - 5);
					var mapSize = mapConfig.mapSize || { x: 6144, z: 6144 };

					var map = L.map(containerRef.current, {
						crs: makeSDTDCRS(L, maxZoom),
						minZoom: minZoom,
						maxZoom: maxZoom + 1,
						maxBounds: [
							[-mapSize.x / 2, -mapSize.z / 2],
							[mapSize.x / 2, mapSize.z / 2],
						],
						maxBoundsViscosity: 1.0,
					});
					mapRef.current = map;

					var tileLayer = L.tileLayer('/ulmap/{z}/{x}/{y}.png?t=' + Date.now(), {
						maxZoom: maxZoom + 1,
						minZoom: minZoom,
						maxNativeZoom: maxZoom,
						minNativeZoom: 0,
						tileSize: tileSize,
					});
					// Matches the stock components/map/tileLayer.js transform exactly.
					tileLayer.getTileUrl = function (coords) {
						coords.y = -coords.y - 1;
						return L.TileLayer.prototype.getTileUrl.call(tileLayer, coords);
					};
					tileLayer.addTo(map);

					map.setView([0, 0], Math.max(0, maxZoom - 3));
					setMapReady(true);
				})
				.catch(function (e) {
					if (!cancelled) {
						setError(e && e.message ? e.message : String(e));
					}
				});

			return function () {
				cancelled = true;
				if (mapRef.current) {
					mapRef.current.remove();
					mapRef.current = null;
				}
				markersLayerRef.current = null;
				setMapReady(false);
			};
			// eslint-disable-next-line react-hooks/exhaustive-deps
		}, []);

		// Shared header + collapse-toggle chrome for both widgets, so collapsing either (or
		// both, for a map-only view) is just a title-bar click - no separate dedicated toggle.
		function renderWidgetPanel(title, collapsed, onToggle, content) {
			return React.createElement(
				'div',
				{ className: 'ul-map-widget' },
				React.createElement(
					'div',
					{ className: 'ul-map-widget-header', onClick: onToggle },
					React.createElement('h2', null, title),
					React.createElement('span', { className: 'ul-map-widget-toggle' }, collapsed ? '▸' : '▾')
				),
				collapsed ? null : content
			);
		}

		var playerRows = widgetPlayers.map(function (p) {
			var clickable = !!p.position;
			return React.createElement(
				'li',
				{
					key: p.platformId,
					className: clickable ? 'ul-map-widget-row' : 'ul-map-widget-row ul-map-widget-row-disabled',
					onClick: clickable ? function () { centerOnPlayer(p); } : undefined,
					title: clickable ? 'Center map on ' + p.name : p.name + ' (position unavailable)',
				},
				renderClassIcon(React, p.class),
				' ' + p.name + ' (Lvl ' + p.level + ')'
			);
		});

		var markerOwnerColors = buildOwnerColorMap(widgetMarkers);
		var filteredWidgetMarkers = widgetMarkers.filter(function (m) {
			return markerMatchesSearch(m, markerSearchText);
		});
		var markerRows = filteredWidgetMarkers.map(function (m, index) {
			var color = m.owner && markerOwnerColors[m.owner] ? markerOwnerColors[m.owner] : '#888888';
			return React.createElement(
				'li',
				{
					key: index,
					className: 'ul-map-widget-row',
					onClick: function () { centerOnMarker(m); },
					title: 'Center map on ' + m.name,
				},
				React.createElement('span', {
					className: 'ul-map-marker-swatch',
					style: { background: color },
				}),
				m.name + ' — ' + (m.owner || 'unknown')
			);
		});

		return React.createElement(
			'div',
			null,
			React.createElement('h1', null, 'UL Map'),
			error ? React.createElement('div', null, 'Failed to load: ' + error) : null,
			React.createElement(
				'div',
				{ style: { display: 'flex', gap: '1rem' } },
				React.createElement('div', {
					ref: containerRef,
					style: { height: '80vh', flex: '1 1 auto', background: '#111' },
				}),
				React.createElement(
					'div',
					{ className: 'ul-map-widget-column' },
					renderWidgetPanel(
						'Online Players',
						playersCollapsed,
						function () { setPlayersCollapsed(!playersCollapsed); },
						widgetPlayers.length === 0
							? React.createElement('p', null, 'No players currently online.')
							: React.createElement('ul', { className: 'ul-map-widget-list' }, playerRows)
					),
					renderWidgetPanel(
						'Shared Markers',
						markersCollapsed,
						function () { setMarkersCollapsed(!markersCollapsed); },
						widgetMarkers.length === 0
							? React.createElement('p', null, 'No shared markers placed yet.')
							: React.createElement(
									React.Fragment,
									null,
									React.createElement('input', {
										type: 'text',
										value: markerSearchText,
										placeholder: 'Filter markers by name or owner...',
										className: 'ul-map-widget-search',
										onChange: function (e) { setMarkerSearchText(e.target.value); },
									}),
									filteredWidgetMarkers.length === 0
										? React.createElement('p', null, 'No markers match "' + markerSearchText + '".')
										: React.createElement('ul', { className: 'ul-map-widget-list' }, markerRows)
							  )
					)
				)
			)
		);
	}

	// Route keys double as both the URL path segment (mods/<modname>/<key>, built verbatim with
	// no encoding by the host's lib/mods.js) and the sidebar link label (Sidebar's
	// SidebarNavLink title={route.name}) - every built-in route (Console, Settings, Map, Mods)
	// is a single plain word, and a multi-word key with spaces/"&" breaks route matching once the
	// browser URL-encodes it (confirmed live: caused a 404). Keep keys URL-safe; the friendlier
	// name lives in each panel's own <h1> instead. "ULMap" deliberately does not collide with the
	// stock "Map" route/module.
	window.UndeadLegacyPanels = {
		routes: {
			PlayerList: PlayerListPanel,
			ULMap: ULMapPanel,
		},
	};
})();
