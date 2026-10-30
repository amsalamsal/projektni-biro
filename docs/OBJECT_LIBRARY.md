# Object library

Generated from `shared/catalog` by `tools/gen_object_library.py` - do not edit by hand.

263 element types, 43 parametric builders. Types marked **review** carry values that must be confirmed against local regulations or a specialist design (they are reported as review items by validation).

## Objekti i reference (2)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `building.footprint` | Tlocrt objekta (referentni volumen) | Objekti | massing | polygon | buildings |
| `building.entrance` | Ulaz u objekat (referenca) | Reference | marker | point | buildings |

## Lokacija (2)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `site.boundary` | Parcelna granica | Granice | outline | polygon | site_boundary |
| `site.existing_feature` | Kontura postojećeg elementa | Postojeće stanje | outline | polygon | existing |

## Parter i popločanje (24)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `surface.concrete_paving` | Betonsko popločanje | Popločanje | slab | polygon | surfaces |
| `surface.concrete_slabs` | Betonske ploče velikog formata | Popločanje | slab | polygon | surfaces |
| `surface.natural_stone` | Popločanje prirodnim kamenom | Popločanje | slab | polygon | surfaces |
| `surface.porcelain_tiles` | Vanjske porculanske / keramičke ploče | Popločanje | slab | polygon | surfaces |
| `surface.asphalt` | Asfaltna površina | Popločanje | slab | polygon | roads |
| `surface.permeable_paving` | Vodopropusno popločanje | Popločanje | slab | polygon | surfaces |
| `surface.grass_grid` | Travna rešetka / ojačani travnjak | Popločanje | slab | polygon | surfaces |
| `surface.gravel` | Šljunak / dekorativni agregat | Rastresiti materijali | slab | polygon | surfaces |
| `surface.rubber_safety` | Gumena sigurnosna podloga | Sigurnosne podloge | slab | polygon | recreation |
| `surface.tactile_paving` | Taktilno popločanje | Popločanje | slab | polygon | surfaces |
| `surface.sand` | Pijesak / rastresita podloga za igru | Rastresiti materijali | slab | polygon | recreation |
| `surface.mulch` | Malč od kore | Rastresiti materijali | slab | polygon | planting |
| `surface.lawn` | Travnjak | Zelene površine | slab | polygon | planting |
| `surface.timber_deck` | Drvena podna obloga / platforma | Podovi i terase | slab | polygon | surfaces |
| `surface.raised_terrace` | Podignuti pod terase (sistem podmetača) | Podovi i terase | slab | polygon | surfaces |
| `surface.sports_court` | Podloga sportskog terena | Sportske podloge | slab | polygon | recreation |
| `edge.kerb` | Ivičnjak | Ivice i prelazi | wall | path | roads |
| `edge.flush_kerb` | Upušteni ivičnjak / prelaz površina | Ivice i prelazi | wall | path | surfaces |
| `edge.edging` | Rubnjak / rubna traka | Ivice i prelazi | wall | path | surfaces |
| `edge.retaining_edge` | Potporna ivica | Ivice i prelazi | wall | path | surfaces |
| `edge.surface_joint` | Dilatacijska / razdjelna fuga | Ivice i prelazi | linear_slab | path | surfaces |
| `level.steps` | Stepenice | Visinske razlike | steps | polygon | surfaces |
| `level.ramp` | Rampa **review** | Visinske razlike | ramp | polygon | surfaces |
| `level.landing` | Podest | Visinske razlike | slab | polygon | surfaces |

## Saobraćaj i parking (22)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `road.access_road` | Interna pristupna saobraćajnica (osovina) | Saobraćajnice | linear_slab | path | roads |
| `road.driveway` | Kolni prilaz (osovina) | Saobraćajnice | linear_slab | path | roads |
| `road.service_route` | Servisni put (osovina) | Saobraćajnice | linear_slab | path | roads |
| `road.fire_access` | Vatrogasni pristup (osovina) **review** | Saobraćajnice | linear_slab | path | roads |
| `road.turning_area` | Okretište | Saobraćajnice | slab | polygon | roads |
| `road.swept_path_placeholder` | Oznaka prohodnosti vozila (nije analiza) | Saobraćajnice | zone | polygon | anno_clearance |
| `road.parking_bay` | Parking mjesto | Parking | parking_bay | point | parking |
| `road.parking_bay_accessible` | Parking mjesto za osobe s invaliditetom **review** | Parking | parking_bay | point | parking |
| `road.parking_bay_ev` | Parking mjesto s punjačem za električna vozila | Parking | parking_bay | point | parking |
| `road.loading_bay` | Mjesto za utovar / istovar | Parking | parking_bay | point | parking |
| `road.pedestrian_crossing` | Pješački prelaz (zebra) | Oznake | stripes | polygon | roads_marking |
| `road.hatched_area` | Šrafirana površina oznake | Oznake | stripes | polygon | roads_marking |
| `road.marking_line` | Linija horizontalne signalizacije | Oznake | linear_slab | path | roads_marking |
| `road.arrow` | Saobraćajna strelica | Oznake | arrow_marking | point | roads_marking |
| `road.speed_hump` | Usporivač brzine (ležeći policajac) | Smirivanje saobraćaja | box_equipment | point | roads |
| `road.raised_crossing` | Uzdignuti pješački prelaz / plato | Smirivanje saobraćaja | slab | polygon | roads |
| `road.bollard` | Stubić | Barijere | bollard | point | furniture |
| `road.removable_bollard` | Pokretni stubić | Barijere | bollard | point | furniture |
| `road.vehicle_barrier` | Rampa za vozila | Barijere | gate | point | roads |
| `road.wheel_stop` | Graničnik točka na parkingu | Parking | box_equipment | point | parking |
| `road.parking_meter` | Parking automat | Parking oprema | box_equipment | point | roads |
| `road.traffic_sign` | Saobraćajni znak | Signalizacija | sign_post | point | roads_marking |

## Biciklistička infrastruktura (16)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `cycling.bike_path` | Biciklistička staza (osovina) | Staze i trake | linear_slab | path | cycling |
| `cycling.shared_path` | Zajednička pješačko-biciklistička staza (osovina) | Staze i trake | linear_slab | path | pedestrian |
| `cycling.bike_lane` | Oznaka biciklističke trake | Staze i trake | linear_slab | path | cycling |
| `cycling.separation_line` | Razdjelna linija biciklisti / pješaci | Staze i trake | linear_slab | path | cycling |
| `cycling.bike_box` | Biciklistički zaustavni prostor (bike box) | Staze i trake | slab | polygon | cycling |
| `cycling.bike_stand` | Stalak za bicikl (luk) | Parking | bike_stand | point | cycling |
| `cycling.bike_rack` | Parking za bicikle (niz stalaka) | Parking | bike_stand | point | cycling |
| `cycling.bike_shelter` | Natkriveni parking za bicikle | Parking | shelter | point | structures |
| `cycling.bike_locker` | Ormarić za bicikl | Parking | box_equipment | point | cycling |
| `cycling.share_station` | Stanica za iznajmljivanje bicikala **review** | Instalacije | box_equipment | point | cycling |
| `cycling.repair_station` | Stanica za popravak bicikala | Instalacije | box_equipment | point | cycling |
| `cycling.service_stand` | Servisni stalak za bicikl | Instalacije | box_equipment | point | cycling |
| `cycling.pump` | Javna pumpa za bicikle | Instalacije | cylinder_equipment | point | cycling |
| `cycling.charging_point` | Punjač za električne bicikle | Instalacije | box_equipment | point | cycling |
| `cycling.route_marking` | Oznaka biciklističke rute | Signalizacija | arrow_marking | point | cycling |
| `cycling.direction_sign` | Biciklistički putokaz | Signalizacija | sign_post | point | cycling |

## Voda i odvodnja (22)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `water.swimming_pool` | Bazen | Bazeni | pool | polygon | water |
| `water.infinity_pool` | Infinity bazen (preljevni rub) | Bazeni | pool | polygon | water |
| `water.decorative_pond` | Ukrasno jezerce | Jezerca i vodeni elementi | pool | polygon | water |
| `water.reflecting_pool` | Reflektujući bazen | Jezerca i vodeni elementi | pool | polygon | water |
| `water.fountain` | Fontana | Jezerca i vodeni elementi | fountain | point | water |
| `water.water_jet` | Vodeni mlaz (podna mlaznica) | Jezerca i vodeni elementi | cylinder_equipment | point | water |
| `water.cascade_wall` | Kaskada / vodeni zid | Jezerca i vodeni elementi | wall | path | water |
| `water.equipment_zone` | Zona opreme bazena | Bazeni | zone | polygon | water |
| `water.drainage_channel` | Linijski drenažni kanal | Površinska odvodnja | channel | path | drainage |
| `water.slot_drain` | Prorezni kanal | Površinska odvodnja | channel | path | drainage |
| `water.overflow_channel` | Preljevni kanal | Površinska odvodnja | channel | path | drainage |
| `water.surface_inlet` | Slivnik / rešetka slivnika | Površinska odvodnja | chamber | point | drainage |
| `water.inspection_chamber` | Reviziono okno odvodnje | Podzemna odvodnja | chamber | point | drainage |
| `water.drain_pipe` | Cijev oborinske odvodnje | Podzemna odvodnja | pipe | path | drainage |
| `water.rainwater_tank` | Podzemni rezervoar kišnice | Kišnica | tank | point | drainage |
| `water.rainwater_collector` | Nadzemna posuda za kišnicu | Kišnica | tank | point | drainage |
| `water.infiltration_crates` | Infiltracijski / retencijski blokovi | Kišnica | tank | point | drainage |
| `water.retention_basin` | Retencijski / detencijski bazen | Kišnica | basin | polygon | drainage |
| `water.rain_garden` | Kišni vrt | Kišnica | basin | polygon | drainage |
| `water.bioswale` | Bio-kanal (zatravljeni jarak) | Kišnica | basin | polygon | drainage |
| `water.irrigation_line` | Linija navodnjavanja | Navodnjavanje i snabdijevanje | pipe | path | drainage |
| `water.supply_point` | Točilo / izljevno mjesto vode | Navodnjavanje i snabdijevanje | box_equipment | point | drainage |

## Zelenilo (29)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `landscape.tree_deciduous` | Listopadno drvo **review** | Drveće | tree | point | planting_trees |
| `landscape.tree_evergreen` | Zimzeleno drvo **review** | Drveće | tree | point | planting_trees |
| `landscape.tree_fruit` | Voćka **review** | Drveće | tree | point | planting_trees |
| `landscape.tree_columnar` | Stubasto drvo **review** | Drveće | tree | point | planting_trees |
| `landscape.tree_existing` | Postojeće drvo | Drveće | tree | point | existing |
| `landscape.orchard_tree` | Voćka u voćnjaku (sadnja u redu) **review** | Drveće | tree | point | planting_trees |
| `landscape.shrub` | Grm | Grmlje i žive ograde | shrub | point | planting |
| `landscape.planting_group` | Grupa sadnje (mješoviti grmovi) | Grmlje i žive ograde | shrub | point | planting |
| `landscape.hedge` | Živa ograda | Grmlje i žive ograde | wall | path | planting |
| `landscape.flower_bed` | Cvjetna gredica | Gredice | planting_area | polygon | planting |
| `landscape.rose_garden` | Ružičnjak | Gredice | planting_area | polygon | planting |
| `landscape.ornamental_grasses` | Ukrasne trave | Gredice | planting_area | polygon | planting |
| `landscape.perennials` | Trajnice | Gredice | planting_area | polygon | planting |
| `landscape.ground_cover` | Pokrivači tla | Gredice | planting_area | polygon | planting |
| `landscape.planting_bed` | Gredica za sadnju (samo zemlja) | Gredice | planting_area | polygon | planting |
| `landscape.meadow` | Prirodna livada | Travnjaci i livade | planting_area | polygon | planting |
| `landscape.lawn_area` | Površina travnjaka | Travnjaci i livade | planting_area | polygon | planting |
| `landscape.climber` | Penjačica | Vertikalno ozelenjavanje | box_equipment | point | planting |
| `landscape.green_roof` | Zeleni krov | Vertikalno ozelenjavanje | slab | polygon | planting |
| `landscape.green_wall` | Zeleni zid | Vertikalno ozelenjavanje | wall | path | planting |
| `landscape.vertical_garden` | Modul vertikalnog vrta | Vertikalno ozelenjavanje | box_equipment | point | planting |
| `landscape.raised_planter` | Podignuta žardinjera | Žardinjere | planter | polygon | planting |
| `landscape.planting_container` | Velika posuda za sadnju | Žardinjere | cylinder_equipment | point | planting |
| `landscape.seasonal_planter` | Posuda sa sezonskim cvijećem | Žardinjere | cylinder_equipment | point | planting |
| `landscape.tree_grate` | Rešetka oko stabla | Zaštita stabala | box_equipment | point | planting_trees |
| `landscape.tree_pit` | Sadna jama stabla (volumen tla) | Zaštita stabala | box_equipment | point | planting_trees |
| `landscape.root_protection_zone` | Zona zaštite korijena **review** | Zaštita stabala | circle_zone | point | anno_clearance |
| `landscape.tree_protection_barrier` | Zaštitna ograda stabla | Zaštita stabala | fence | path | construction |
| `landscape.irrigation_point` | Priključak za navodnjavanje / ventilska kutija | Navodnjavanje | cylinder_equipment | point | planting |

## Komunalna infrastruktura (20)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `utility.waste_container_ug_3m3` | Podzemni kontejner za otpad 3 m³ **review** | Otpad | underground_container | point | waste |
| `utility.waste_container_ug_5m3` | Podzemni kontejner za otpad 5 m³ **review** | Otpad | underground_container | point | waste |
| `utility.waste_container_semi` | Polupodzemni kontejner za otpad **review** | Otpad | underground_container | point | waste |
| `utility.waste_container_above` | Nadzemni kontejner za otpad | Otpad | box_equipment | point | waste |
| `utility.recycling_island` | Reciklažno ostrvo | Otpad | zone | polygon | waste |
| `utility.cistern` | Podzemna cisterna | Rezervoari | tank | point | utilities |
| `utility.water_tank` | Nadzemni rezervoar vode | Rezervoari | tank | point | utilities |
| `utility.septic_tank` | Septička jama | Rezervoari | tank | point | utilities |
| `utility.trench` | Kanal za instalacije (gabarit) | Trase | linear_slab | path | utilities |
| `utility.service_corridor` | Podzemni koridor instalacija | Trase | linear_slab | path | utilities |
| `utility.cable_duct` | Kablovska cijev | Trase | pipe | path | utilities |
| `utility.water_pipe` | Vodovodna cijev | Trase | pipe | path | utilities |
| `utility.service_connection` | Priključak instalacije | Trase | pipe | path | utilities |
| `utility.electrical_chamber` | Elektro reviziono okno | Okna | chamber | point | utilities |
| `utility.telecom_chamber` | Telekomunikacijsko okno | Okna | chamber | point | utilities |
| `utility.water_meter_chamber` | Vodomjerno okno | Okna | chamber | point | utilities |
| `utility.valve_chamber` | Ventilsko okno | Okna | chamber | point | utilities |
| `utility.manhole` | Šaht | Okna | chamber | point | utilities |
| `utility.pump_chamber` | Pumpno okno | Okna | chamber | point | utilities |
| `utility.equipment_enclosure` | Kućište opreme **review** | Oprema | box_equipment | point | utilities |

## Rasvjeta (19)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `lighting.street_light` | Ulična svjetiljka | Stubovi | pole_mounted | point | lighting |
| `lighting.pedestrian_light` | Pješačka svjetiljka (na vrhu stuba) | Stubovi | pole_mounted | point | lighting |
| `lighting.solar_light` | Solarna svjetiljka | Stubovi | pole_mounted | point | lighting |
| `lighting.smart_light` | Pametna svjetiljka (sa senzorom pokreta) | Stubovi | pole_mounted | point | lighting |
| `lighting.area_floodlight` | Reflektorska rasvjeta površine | Stubovi | pole_mounted | point | lighting |
| `lighting.bollard_light` | Stubić-svjetiljka | Niska rasvjeta | bollard | point | lighting |
| `lighting.path_light` | Svjetiljka za staze | Niska rasvjeta | bollard | point | lighting |
| `lighting.recessed_ground_light` | Ugradna podna svjetiljka | Niska rasvjeta | cylinder_equipment | point | lighting |
| `lighting.underwater_light` | Podvodna svjetiljka | Niska rasvjeta | cylinder_equipment | point | lighting |
| `lighting.step_light` | Svjetiljka za stepenice | Zidna montaža | box_equipment | point | lighting |
| `lighting.wall_light` | Zidna svjetiljka | Zidna montaža | box_equipment | point | lighting |
| `lighting.facade_light` | Fasadna rasvjeta | Zidna montaža | box_equipment | point | lighting |
| `lighting.garden_light` | Dekorativna vrtna svjetiljka | Dekorativno | cylinder_equipment | point | lighting |
| `lighting.led_strip` | LED traka | Dekorativno | linear_slab | path | lighting |
| `lighting.pergola_light` | Svjetiljka pergole | Dekorativno | box_equipment | point | lighting |
| `lighting.feature_light` | Akcentna rasvjeta (usmjerena naviše) | Dekorativno | cylinder_equipment | point | lighting |
| `lighting.control_cabinet` | Ormar upravljanja rasvjetom **review** | Upravljanje | box_equipment | point | lighting |
| `lighting.circuit_route` | Trasa strujnog kruga rasvjete | Upravljanje | pipe | path | lighting_circuits |
| `lighting.coverage_zone` | Ilustrativni domet rasvjete (nije fotometrija) | Upravljanje | circle_zone | point | anno_clearance |

## Ograde i zidovi (16)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `wall.metal_fence` | Metalna ograda od šipki | Ograde | fence | path | fences |
| `wall.mesh_fence` | Ograda od zavarene mreže | Ograde | fence | path | fences |
| `wall.glass_railing` | Staklena ograda | Ograde i rukohvati | fence | path | fences |
| `wall.timber_fence` | Drvena ograda | Ograde | fence | path | fences |
| `wall.decorative_fence` | Dekorativna ograda | Ograde | fence | path | fences |
| `wall.safety_railing` | Zaštitna ograda **review** | Ograde i rukohvati | fence | path | fences |
| `wall.gabion` | Gabionski zid | Zidovi | wall | path | walls |
| `wall.concrete` | Betonski zid | Zidovi | wall | path | walls |
| `wall.stone` | Kameni zid | Zidovi | wall | path | walls |
| `wall.retaining` | Potporni zid **review** | Potporne konstrukcije | wall | path | walls |
| `wall.planter_wall` | Zid žardinjere | Zidovi | wall | path | walls |
| `wall.pedestrian_gate` | Pješačka kapija | Kapije | gate | point | fences |
| `wall.vehicle_gate` | Kolna kapija (dvokrilna) | Kapije | gate | point | fences |
| `wall.sliding_gate` | Automatska klizna kapija | Kapije | gate | point | fences |
| `wall.swing_gate` | Krilna kapija | Kapije | gate | point | fences |
| `wall.access_barrier` | Rampa kontrole pristupa | Kapije | gate | point | fences |

## Nadstrešnice i zasjena (12)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `structure.pergola` | Pergola | Pergole | pergola | polygon | structures |
| `structure.bioclimatic_pergola` | Bioklimatska pergola (pomične lamele) | Pergole | pergola | polygon | structures |
| `structure.retractable_shade` | Pergola s pomičnom tendom | Pergole | pergola | polygon | structures |
| `structure.tensile_membrane` | Membranska nadstrešnica (pojednostavljeno) | Nadstrešnice | sail | polygon | structures |
| `structure.carport` | Nadstrešnica za automobile | Nadstrešnice | pergola | polygon | structures |
| `structure.solar_canopy` | Solarna nadstrešnica | Nadstrešnice | pergola | polygon | structures |
| `structure.bus_shelter` | Nadstrešnica tipa stajališta | Nadstrešnice za zaklon | shelter | point | structures |
| `structure.gazebo` | Sjenica | Paviljoni | shelter | point | structures |
| `structure.pavilion` | Vanjski paviljon | Paviljoni | shelter | point | structures |
| `structure.terrace_roof` | Krov terase (staklo) | Nadstrešnice | pergola | polygon | structures |
| `structure.entrance_canopy` | Laka ulazna nadstrešnica | Nadstrešnice | box_equipment | point | structures |
| `structure.seating_shelter` | Nadstrešnica za sjedenje | Nadstrešnice za zaklon | shelter | point | structures |

## Urbani mobilijar i rekreacija (26)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `furniture.bench` | Klupa s naslonom | Sjedenje | bench | point | furniture |
| `furniture.bench_backless` | Klupa bez naslona | Sjedenje | bench | point | furniture |
| `furniture.accessible_bench` | Pristupačna klupa (s rukohvatima) | Sjedenje | bench | point | furniture |
| `furniture.chair` | Vanjska stolica | Sjedenje | bench | point | furniture |
| `furniture.lounger` | Ležaljka | Sjedenje | bench | point | furniture |
| `furniture.picnic_table` | Stol za piknik | Stolovi | table | point | furniture |
| `furniture.dining_table` | Vanjski trpezarijski stol | Stolovi | table | point | furniture |
| `furniture.waste_bin` | Korpa za otpatke | Korpe i kante | cylinder_equipment | point | furniture |
| `furniture.ashtray` | Pepeljara | Korpe i kante | cylinder_equipment | point | furniture |
| `furniture.planter` | Žardinjera (mobilijar) | Žardinjere | box_equipment | point | furniture |
| `furniture.drinking_fountain` | Česma za piće | Sadržaji | box_equipment | point | furniture |
| `furniture.outdoor_shower` | Vanjski tuš | Sadržaji | pole_mounted | point | furniture |
| `furniture.pet_waste_station` | Stanica za otpad kućnih ljubimaca | Sadržaji | box_equipment | point | furniture |
| `recreation.fitness_station` | Vanjska fitness sprava **review** | Fitness | box_equipment | point | recreation |
| `recreation.playground_unit` | Dječija igračka sprava **review** | Dječija igrališta | box_equipment | point | recreation |
| `recreation.slide` | Tobogan **review** | Dječija igrališta | box_equipment | point | recreation |
| `recreation.swing` | Ljuljačka **review** | Dječija igrališta | swing | point | recreation |
| `recreation.climbing_structure` | Penjalica **review** | Dječija igrališta | box_equipment | point | recreation |
| `recreation.trampoline` | Ukopani trampolin **review** | Dječija igrališta | cylinder_equipment | point | recreation |
| `recreation.zip_line` | Zip-line (žičara) **review** | Dječija igrališta | cable_line | path | recreation |
| `recreation.table_tennis` | Vanjski stol za stoni tenis | Sport | table | point | recreation |
| `recreation.basketball_hoop` | Košarkaški koš | Sport | sign_post | point | recreation |
| `recreation.chess_table` | Vanjski šahovski sto | Sport | table | point | recreation |
| `recreation.football_goal` | Gol za mali fudbal | Sport | box_equipment | point | recreation |
| `recreation.platform` | Rekreativna platforma | Sport | slab | polygon | recreation |
| `recreation.safety_zone` | Sigurnosna zona igrališta **review** | Dječija igrališta | zone | polygon | anno_clearance |

## Eko tehnologije (13)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `sustainability.solar_array` | Solarna elektrana na tlu | Solarna energija | solar_array | point | sustainability |
| `sustainability.inverter` | Invertor **review** | Solarna energija | box_equipment | point | sustainability |
| `sustainability.battery_cabinet` | Ormar baterijskog spremnika **review** | Skladištenje | box_equipment | point | sustainability |
| `sustainability.ev_charger_pedestal` | Stanica za punjenje električnih vozila (stub) | Punjenje električnih vozila | box_equipment | point | parking |
| `sustainability.ev_charger_wallbox` | Punjač za električna vozila (zidni) | Punjenje električnih vozila | box_equipment | point | parking |
| `sustainability.heat_pump` | Toplotna pumpa (vanjska jedinica) **review** | Tehničke jedinice | box_equipment | point | sustainability |
| `sustainability.outdoor_unit` | Vanjska tehnička jedinica **review** | Tehničke jedinice | box_equipment | point | sustainability |
| `sustainability.wind_turbine` | Vjetroturbina s vertikalnom osom | Vjetar | vawt | point | sustainability |
| `sustainability.rainwater_harvesting` | Rezervoar za prikupljanje kišnice | Voda | tank | point | sustainability |
| `sustainability.greywater_unit` | Uređaj za prečišćavanje sive vode **review** | Voda | box_equipment | point | sustainability |
| `sustainability.compost_station` | Kompostište | Otpad | box_equipment | point | sustainability |
| `sustainability.energy_cabinet` | Ormar za praćenje energije | Tehničke jedinice | box_equipment | point | sustainability |
| `sustainability.suds_crate` | Retencijski modul održive odvodnje | Voda | tank | point | drainage |

## Vanjski klimatski sistemi (12)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `climate.misting_line` | Linija sistema za raspršivanje vode | Hlađenje | pipe | path | climate |
| `climate.infrared_heater` | Infracrveni grijač (montažni) **review** | Grijanje | box_equipment | point | climate |
| `climate.patio_heater` | Samostojeći grijač terase **review** | Grijanje | cylinder_equipment | point | climate |
| `climate.misting_post` | Stub za raspršivanje vode (misting) | Hlađenje | cylinder_equipment | point | climate |
| `climate.heated_zone` | Grijana zona terase | Grijanje | zone | polygon | climate |
| `climate.cooling_unit` | Vanjska rashladna jedinica **review** | Hlađenje | box_equipment | point | climate |
| `climate.evaporative_cooler` | Evaporativni rashladni uređaj **review** | Hlađenje | box_equipment | point | climate |
| `climate.shade_sail` | Jedro za sjenu | Zasjena | sail | polygon | climate |
| `climate.awning` | Pomična tenda | Zasjena | box_equipment | point | climate |
| `climate.wind_screen` | Zaštita od vjetra | Zasjena | fence | path | climate |
| `climate.ventilation_unit` | Vanjska ventilacijska oprema **review** | Ventilacija | box_equipment | point | climate |
| `climate.comfort_zone` | Zona opreme toplotnog komfora | Zone | zone | polygon | climate |

## Dekorativni elementi (11)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `decorative.sculpture` | Skulptura (zamjenski ili vlastiti model) | Umjetnost | box_equipment | point | decorative |
| `decorative.public_art` | Javna umjetnička instalacija | Umjetnost | box_equipment | point | decorative |
| `decorative.decorative_wall` | Dekorativni zid | Zidovi i paravani | wall | path | decorative |
| `decorative.mural` | Mural (oslikana površina) | Umjetnost | wall | path | decorative |
| `decorative.feature_planter` | Akcentna žardinjera | Žardinjere | planter | polygon | decorative |
| `decorative.boulder` | Dekorativni kamen / gromada | Kamen | boulder | point | decorative |
| `decorative.focal_point` | Fokusna tačka pejzaža | Umjetnost | cylinder_equipment | point | decorative |
| `decorative.light_installation` | Dekorativna svjetlosna instalacija | Svjetlosna umjetnost | box_equipment | point | decorative |
| `decorative.entrance_monument` | Ulazno obilježje | Obilježja | box_equipment | point | decorative |
| `decorative.screen` | Dekorativni paravan | Zidovi i paravani | fence | path | decorative |
| `decorative.signage_structure` | Nosiva konstrukcija natpisa | Obilježja | sign_post | point | decorative |

## Informativni i sigurnosni sistemi (17)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `info.site_board` | Informativna tabla lokacije | Informacije | sign_post | point | information |
| `info.site_map` | Plan lokacije (stalak s kartom) | Informacije | sign_post | point | information |
| `info.qr_panel` | Informativni panel s QR kodom | Informacije | sign_post | point | information |
| `info.directional_sign` | Putokaz | Orijentacija | sign_post | point | information |
| `info.wayfinding_sign` | Orijentacijski znak (monolit) | Orijentacija | box_equipment | point | information |
| `info.building_sign` | Oznaka objekta | Orijentacija | box_equipment | point | information |
| `info.accessibility_sign` | Oznaka pristupačnosti | Orijentacija | sign_post | point | information |
| `info.fire_info_point` | Informativno mjesto zaštite od požara | Sigurnost | sign_post | point | information |
| `info.digital_kiosk` | Digitalni kiosk | Informacije | box_equipment | point | information |
| `info.cctv_camera` | Video nadzorna kamera (na stubu) | Sigurnosni sistemi | pole_mounted | point | information |
| `info.security_cabinet` | Ormar sigurnosne opreme | Sigurnosni sistemi | box_equipment | point | information |
| `info.access_reader` | Čitač kontrole pristupa (stub) | Sigurnosni sistemi | box_equipment | point | information |
| `info.intercom` | Interfon (stub) | Komunikacije | box_equipment | point | information |
| `info.speaker` | Vanjski zvučnik / ozvučenje (stub) | Komunikacije | pole_mounted | point | information |
| `info.wifi_ap` | Wi-Fi pristupna tačka (stub) | Komunikacije | pole_mounted | point | information |
| `info.emergency_call_point` | Mjesto za hitni poziv | Sigurnost | box_equipment | point | information |
| `info.cable_route` | Trasa komunikacijskog kabla | Komunikacije | pipe | path | information |

## Builders

| Builder | Geometry | Parameters |
|---|---|---|
| arrow_marking | point | length, width, shaftWidth, thickness, material |
| basin | polygon | depth, sideSlope, waterDepth, floorMaterial, waterMaterial |
| bench | point | length, depth, seatHeight, backrest, backHeight, armrests, material, frameMaterial |
| bike_stand | point | count, spacing, width, height, tubeDiameter, bikesPerStand, material |
| bollard | point | height, diameter, lit, removable, power (W), fixtureCode, material |
| boulder | point | length, width, height, embedment, material |
| box_equipment | point | width, depth, height, baseOffset, serviceClearance, power (kW), material |
| cable_line | path | startHeight, endHeight, postDiameter, cableDiameter, safetyClearance, material |
| chamber | point | shape, innerSize, wallThickness, depth, coverShape, coverSize, coverLevelOffset, loadClass, service, material, coverMaterial |
| channel | path | width, depth, gradient (%), flowDirection, outletTo, grateMaterial, bodyMaterial |
| circle_zone | point | radius, height, baseOffset, zoneType, material |
| cylinder_equipment | point | diameter, height, baseOffset, serviceClearance, power (W), material |
| fence | path | height, postSpacing, postSize, panelThickness, groundClearance, panelStyle, postMaterial, panelMaterial, foundationDepth |
| fountain | point | basinDiameter, basinHeight, rimWidth, jetHeight, jetDiameter, basinMaterial, waterMaterial |
| gate | point | width, height, gateType, openingSide, postSize, leafThickness, automatic, material |
| linear_slab | path | width, thickness, topOffset, material, requiredClearWidth, surfaceCode |
| marker | point | size, reference, material |
| massing | polygon | height, baseOffset, storeys, material |
| outline | polygon | lineHeight |
| parking_bay | point | width, length, lineWidth, accessible, ev, bayNumber, markingMaterial |
| pergola | polygon | height, columnSize, columnSpacing, beamDepth, beamWidth, rafterSpacing, rafterDepth, rafterWidth, louverAngle, cover, roofSlope, material, coverMaterial |
| pipe | path | diameter, axisOffset, service, material |
| planter | polygon | height, wallThickness, soilDepthBelowTop, plantHeight, material, soilMaterial, plantMaterial |
| planting_area | polygon | soilThickness, topOffset, plantHeight, plantSpacing, plantCode, soilMaterial, plantMaterial |
| pole_mounted | point | poleHeight, poleDiameter, armLength, headLength, headWidth, headHeight, solarPanel, power (W), fixtureCode, coverageRadius, poleMaterial, headMaterial |
| pool | polygon | depth, waterLevelOffset, wallThickness, floorThickness, finishMaterial, waterMaterial |
| ramp | polygon | rise, thickness, maxSlope (%), material |
| sail | polygon | highHeight, lowHeight, mastDiameter, material, mastMaterial |
| shelter | point | width, depth, height, roofType, roofSlope, roofThickness, postSize, backWall, sideWalls, frameMaterial, wallMaterial, roofMaterial |
| shrub | point | height, diameter, foliageMaterial |
| sign_post | point | postCount, postHeight, postDiameter, plateWidth, plateHeight, plateBottom, plateShape, plateThickness, viewingDirection, postMaterial, plateMaterial |
| slab | polygon | thickness, topOffset, material, surfaceCode, buildup, jointPattern |
| solar_array | point | rows, modulesPerRow, moduleWidth, moduleLength, tilt, rowSpacing, lowEdgeHeight, modulePower (W), material, frameMaterial |
| steps | polygon | totalRise, riserHeight, material |
| stripes | polygon | stripeWidth, gapWidth, thickness, material |
| swing | point | width, height, frameSpread, seats, seatHeight, frameDiameter, safetyClearance, material |
| table | point | length, width, height, benches, benchWidth, material, frameMaterial |
| tank | point | shape, length, width, height, installation, coverDepth, accessShaft, shaftDiameter, nominalVolume (m3), material |
| tree | point | height, canopyDiameter, trunkDiameter, clearTrunkHeight, form, matureHeight, matureCanopyDiameter, status, foliageMaterial |
| underground_container | point | nominalVolume (m3), installation, wasteFraction, bodyLength, bodyWidth, bodyDepth, aboveGroundHeight, columnDiameter, coverLevelOffset, excavationMargin, showExcavation, liftingClearance, dimensionsSource, material, columnMaterial |
| vawt | point | mastHeight, mastDiameter, rotorDiameter, rotorHeight, ratedPower (kW), material, rotorMaterial |
| wall | path | height, thickness, baseOffset, alignment, footingWidth, footingDepth, material |
| zone | polygon | height, baseOffset, zoneType, material |
