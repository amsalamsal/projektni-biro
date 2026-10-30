# Inventar 3D modela

Generisano iz biblioteke proširenja (`Assets/inventory.json`, `Assets/library.json`) alatom
`tools/assets/gen_inventory_doc.py` – ne uređivati ručno.

## Sažetak

* Objekata iz specifikacije: **122** u **14** kategorija.
* Modela u biblioteci: **253** (jedan model po tipu elementa; ID modela = ID tipa elementa).
* Veza 2D ↔ 3D: svaki AutoCAD element (blok simbola, polilinija, šrafura) nosi ID tipa u svojim podacima (`SDP_ELEMENT`), paket ga prenosi u SketchUp, a SketchUp gradi model s istim ID-om. Nedostajući model se prijavljuje, nikad ne zamjenjuje drugim.
* Vrsta modela: vlastiti detaljni 216, parametarski s teksturom 37, generički 0.
* Statusi modela: Uvezeno u biblioteku 253.
* Objekata bez modela: **0**.

### Statusi (od početka do kraja)

| Status | Značenje |
|---|---|
| Nije započeto (Not started) | model ne postoji |
| Traži se model (Searching for model) | traži se vanjski model |
| Model pronađen (Model found) | vanjski model pronađen, licenca nije provjerena |
| Model preuzet (Model acquired) | datoteka preuzeta |
| Napravljen vlastiti model (Custom model created) | model napravljen u projektu |
| Licenca provjerena (License verified) | licenca dozvoljava upotrebu i distribuciju |
| Uvezeno u biblioteku (Imported into library) | model, pregled i metapodaci su u biblioteci i prošli su automatske provjere |
| Testirano u SketchUpu (Tested in SketchUp) | umetnut i provjeren u stvarnom SketchUpu |
| Završeno (Complete) | testiran u SketchUpu i odobren |
| Blokirano (Blocked) | greška ili otvoreno pitanje – razlog je naveden |

**Iskreno stanje:** svi modeli su na statusu *Uvezeno u biblioteku*. Nijedan još nije *Testirano u SketchUpu* ni
*Završeno*, jer u razvojnom okruženju nema SketchUpa. Automatske provjere koje su prošle za svaki model:
zatvorena tijela (manifold) na tri nivoa detalja, dimenzije prema parametrima, ispravan COLLADA (metri, Z gore)
koji nezavisni čitač (three.js ColladaLoader) učitava s istim dimenzijama, postojeće teksture i slika pregleda.
Za status *Testirano u SketchUpu* svaki model treba umetnuti kroz *Biblioteka 3D modela*, promijeniti parametar,
rotirati i zamijeniti, i provjeriti izgled; rezultat se upisuje u `IZVJESTAJ_TESTIRANJA.md`.

Svi modeli su vlastiti rad (proceduralni generatori u `core/models/*.rb`, teksture iz `tools/assets/make_textures.py`);
nijedan nije preuzet s interneta, pa licenca nije ograničenje (vidi `REGISTAR_LICENCI.md`).

## Infrastruktura i površine (`Assets/Infrastructure`)

| Objekat iz specifikacije | ID modela = tip elementa | Naziv modela | Vrsta modela | Status |
|---|---|---|---|---|
| Betonsko popločanje | `surface.concrete_paving` | Betonsko popločanje | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |
| Prirodni kamen | `surface.natural_stone` | Popločanje prirodnim kamenom | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |
| Betonske ploče | `surface.concrete_slabs` | Betonske ploče velikog formata | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |
| Gumene sigurnosne podloge | `surface.rubber_safety` | Gumena sigurnosna podloga | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |
| Taktilno popločanje | `surface.tactile_paving` | Taktilno popločanje | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |
| Šljunak i dekorativni agregat | `surface.gravel` | Šljunak / dekorativni agregat | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |
| Asfalt | `surface.asphalt` | Asfaltna površina | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |
| Vodopropusno popločanje | `surface.permeable_paving` | Vodopropusno popločanje | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |
|  | `surface.grass_grid` | Travna rešetka / ojačani travnjak | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |
| Ivičnjaci | `edge.kerb` | Ivičnjak | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `edge.edging` | Rubnjak / rubna traka | vlastiti detaljni model | Uvezeno u biblioteku |
| Prelazi površina | `edge.flush_kerb` | Upušteni ivičnjak / prelaz površina | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `edge.surface_joint` | Dilatacijska / razdjelna fuga | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |
| Materijali ispune i podloge | `surface.sand` | Pijesak / rastresita podloga za igru | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |
|  | `surface.mulch` | Malč od kore | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |

## Saobraćaj i mobilnost (`Assets/Traffic`)

| Objekat iz specifikacije | ID modela = tip elementa | Naziv modela | Vrsta modela | Status |
|---|---|---|---|---|
| Stanice za punjenje električnih vozila | `sustainability.ev_charger_pedestal` | Stanica za punjenje električnih vozila (stub) | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `sustainability.ev_charger_wallbox` | Punjač za električna vozila (zidni) | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `road.parking_bay_ev` | Parking mjesto s punjačem za električna vozila | vlastiti detaljni model | Uvezeno u biblioteku |
| Usporivači (ležeći policajci) | `road.speed_hump` | Usporivač brzine (ležeći policajac) | vlastiti detaljni model | Uvezeno u biblioteku |
| Saobraćajni znakovi | `road.traffic_sign` | Saobraćajni znak | vlastiti detaljni model | Uvezeno u biblioteku |
| Stubići | `road.bollard` | Stubić | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `road.removable_bollard` | Pokretni stubić | vlastiti detaljni model | Uvezeno u biblioteku |
| Barijere za vozila | `road.vehicle_barrier` | Rampa za vozila | vlastiti detaljni model | Uvezeno u biblioteku |
| Parking oprema | `road.parking_meter` | Parking automat | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `road.wheel_stop` | Graničnik točka na parkingu | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `road.parking_bay` | Parking mjesto | vlastiti detaljni model | Uvezeno u biblioteku |
| Pješački prelazi | `road.pedestrian_crossing` | Pješački prelaz (zebra) | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |
|  | `road.raised_crossing` | Uzdignuti pješački prelaz / plato | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |
| Parking za osobe s invaliditetom | `road.parking_bay_accessible` | Parking mjesto za osobe s invaliditetom | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `info.accessibility_sign` | Oznaka pristupačnosti | vlastiti detaljni model | Uvezeno u biblioteku |
| Elementi smirivanja saobraćaja | `road.raised_crossing` | Uzdignuti pješački prelaz / plato | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |
|  | `road.speed_hump` | Usporivač brzine (ležeći policajac) | vlastiti detaljni model | Uvezeno u biblioteku |

## Biciklistička infrastruktura (`Assets/Bicycle`)

| Objekat iz specifikacije | ID modela = tip elementa | Naziv modela | Vrsta modela | Status |
|---|---|---|---|---|
| Biciklističke staze | `cycling.bike_path` | Biciklistička staza (osovina) | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |
|  | `cycling.shared_path` | Zajednička pješačko-biciklistička staza (osovina) | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |
|  | `cycling.bike_lane` | Oznaka biciklističke trake | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |
| Stalci za bicikle | `cycling.bike_stand` | Stalak za bicikl (luk) | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `cycling.bike_rack` | Parking za bicikle (niz stalaka) | vlastiti detaljni model | Uvezeno u biblioteku |
| Nadstrešnice za bicikle | `cycling.bike_shelter` | Natkriveni parking za bicikle | vlastiti detaljni model | Uvezeno u biblioteku |
| Bike box | `cycling.bike_box` | Biciklistički zaustavni prostor (bike box) | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |
| Stanice javnih bicikala | `cycling.share_station` | Stanica za iznajmljivanje bicikala | vlastiti detaljni model | Uvezeno u biblioteku |
| Stanice za popravak bicikala | `cycling.repair_station` | Stanica za popravak bicikala | vlastiti detaljni model | Uvezeno u biblioteku |
| Pumpe za bicikle | `cycling.pump` | Javna pumpa za bicikle | vlastiti detaljni model | Uvezeno u biblioteku |
| Servisni stalci za bicikle | `cycling.service_stand` | Servisni stalak za bicikl | vlastiti detaljni model | Uvezeno u biblioteku |

## Vodeni elementi i upravljanje vodom (`Assets/Water`)

| Objekat iz specifikacije | ID modela = tip elementa | Naziv modela | Vrsta modela | Status |
|---|---|---|---|---|
| Bazeni, uključujući infinity bazen | `water.swimming_pool` | Bazen | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `water.infinity_pool` | Infinity bazen (preljevni rub) | vlastiti detaljni model | Uvezeno u biblioteku |
| Kaskade | `water.cascade_wall` | Kaskada / vodeni zid | vlastiti detaljni model | Uvezeno u biblioteku |
| Fontane | `water.fountain` | Fontana | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `water.water_jet` | Vodeni mlaz (podna mlaznica) | vlastiti detaljni model | Uvezeno u biblioteku |
| Dekorativni vodeni elementi | `water.decorative_pond` | Ukrasno jezerce | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `water.reflecting_pool` | Reflektujući bazen | vlastiti detaljni model | Uvezeno u biblioteku |
| Drenažni kanali | `water.drainage_channel` | Linijski drenažni kanal | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `water.slot_drain` | Prorezni kanal | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `water.overflow_channel` | Preljevni kanal | vlastiti detaljni model | Uvezeno u biblioteku |
| Sistemi zadržavanja kišnice | `water.retention_basin` | Retencijski / detencijski bazen | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `water.infiltration_crates` | Infiltracijski / retencijski blokovi | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `water.rain_garden` | Kišni vrt | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `water.bioswale` | Bio-kanal (zatravljeni jarak) | vlastiti detaljni model | Uvezeno u biblioteku |
| Vodopropusni kolovozni sistemi | `surface.permeable_paving` | Vodopropusno popločanje | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |
|  | `surface.grass_grid` | Travna rešetka / ojačani travnjak | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |
| Elementi prikupljanja kišnice | `water.rainwater_tank` | Podzemni rezervoar kišnice | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `water.rainwater_collector` | Nadzemna posuda za kišnicu | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `water.surface_inlet` | Slivnik / rešetka slivnika | vlastiti detaljni model | Uvezeno u biblioteku |
| Revizioni i pristupni elementi | `water.inspection_chamber` | Reviziono okno odvodnje | vlastiti detaljni model | Uvezeno u biblioteku |

## Zelenilo i sadnja (`Assets/Greenery`)

| Objekat iz specifikacije | ID modela = tip elementa | Naziv modela | Vrsta modela | Status |
|---|---|---|---|---|
| Pojedinačna stabla | `landscape.tree_deciduous` | Listopadno drvo | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `landscape.tree_evergreen` | Zimzeleno drvo | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `landscape.tree_fruit` | Voćka | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `landscape.tree_columnar` | Stubasto drvo | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `landscape.tree_existing` | Postojeće drvo | vlastiti detaljni model | Uvezeno u biblioteku |
| Grmlje | `landscape.shrub` | Grm | vlastiti detaljni model | Uvezeno u biblioteku |
| Žive ograde | `landscape.hedge` | Živa ograda | vlastiti detaljni model | Uvezeno u biblioteku |
| Ukrasne trave | `landscape.ornamental_grasses` | Ukrasne trave | vlastiti detaljni model | Uvezeno u biblioteku |
| Cvjetne gredice | `landscape.flower_bed` | Cvjetna gredica | vlastiti detaljni model | Uvezeno u biblioteku |
| Ružičnjaci | `landscape.rose_garden` | Ružičnjak | vlastiti detaljni model | Uvezeno u biblioteku |
| Voćnjaci | `landscape.orchard_tree` | Voćka u voćnjaku (sadnja u redu) | vlastiti detaljni model | Uvezeno u biblioteku |
| Zeleni krovovi | `landscape.green_roof` | Zeleni krov | vlastiti detaljni model | Uvezeno u biblioteku |
| Krovni vrtovi | `landscape.raised_planter` | Podignuta žardinjera | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `landscape.green_roof` | Zeleni krov | vlastiti detaljni model | Uvezeno u biblioteku |
| Zeleni zidovi | `landscape.green_wall` | Zeleni zid | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `landscape.vertical_garden` | Modul vertikalnog vrta | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `landscape.climber` | Penjačica | vlastiti detaljni model | Uvezeno u biblioteku |
| Grupe sadnje | `landscape.planting_group` | Grupa sadnje (mješoviti grmovi) | vlastiti detaljni model | Uvezeno u biblioteku |
| Sezonsko zelenilo | `landscape.seasonal_planter` | Posuda sa sezonskim cvijećem | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `landscape.perennials` | Trajnice | vlastiti detaljni model | Uvezeno u biblioteku |
| Pokrivači tla | `landscape.ground_cover` | Pokrivači tla | vlastiti detaljni model | Uvezeno u biblioteku |

## Komunalna infrastruktura (`Assets/Municipal`)

| Objekat iz specifikacije | ID modela = tip elementa | Naziv modela | Vrsta modela | Status |
|---|---|---|---|---|
| Podzemni kontejneri 3 m³ i 5 m³ | `utility.waste_container_ug_3m3` | Podzemni kontejner za otpad 3 m³ | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `utility.waste_container_ug_5m3` | Podzemni kontejner za otpad 5 m³ | vlastiti detaljni model | Uvezeno u biblioteku |
| Reciklažna ostrva | `utility.recycling_island` | Reciklažno ostrvo | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `utility.waste_container_semi` | Polupodzemni kontejner za otpad | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `utility.waste_container_above` | Nadzemni kontejner za otpad | vlastiti detaljni model | Uvezeno u biblioteku |
| Podzemne cisterne | `utility.cistern` | Podzemna cisterna | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `utility.water_tank` | Nadzemni rezervoar vode | vlastiti detaljni model | Uvezeno u biblioteku |
| Septičke jame | `utility.septic_tank` | Septička jama | vlastiti detaljni model | Uvezeno u biblioteku |
| Kablovski kanali | `utility.trench` | Kanal za instalacije (gabarit) | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `utility.cable_duct` | Kablovska cijev | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |
| Šahtovi | `utility.manhole` | Šaht | vlastiti detaljni model | Uvezeno u biblioteku |
| Poklopci komunalnih okana | `utility.electrical_chamber` | Elektro reviziono okno | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `utility.telecom_chamber` | Telekomunikacijsko okno | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `utility.water_meter_chamber` | Vodomjerno okno | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `utility.valve_chamber` | Ventilsko okno | vlastiti detaljni model | Uvezeno u biblioteku |
| Podzemna komunalna infrastruktura | `utility.service_corridor` | Podzemni koridor instalacija | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `utility.water_pipe` | Vodovodna cijev | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |
|  | `utility.service_connection` | Priključak instalacije | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |
|  | `utility.pump_chamber` | Pumpno okno | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `utility.equipment_enclosure` | Kućište opreme | vlastiti detaljni model | Uvezeno u biblioteku |
| Revizione komore | `water.inspection_chamber` | Reviziono okno odvodnje | vlastiti detaljni model | Uvezeno u biblioteku |

## Rasvjeta (`Assets/Lighting`)

| Objekat iz specifikacije | ID modela = tip elementa | Naziv modela | Vrsta modela | Status |
|---|---|---|---|---|
| Solarne svjetiljke | `lighting.solar_light` | Solarna svjetiljka | vlastiti detaljni model | Uvezeno u biblioteku |
| Pametne svjetiljke sa senzorom pokreta | `lighting.smart_light` | Pametna svjetiljka (sa senzorom pokreta) | vlastiti detaljni model | Uvezeno u biblioteku |
| Rasvjetni stubići | `lighting.bollard_light` | Stubić-svjetiljka | vlastiti detaljni model | Uvezeno u biblioteku |
| Svjetiljke za staze | `lighting.path_light` | Svjetiljka za staze | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `lighting.pedestrian_light` | Pješačka svjetiljka (na vrhu stuba) | vlastiti detaljni model | Uvezeno u biblioteku |
| Podvodna rasvjeta | `lighting.underwater_light` | Podvodna svjetiljka | vlastiti detaljni model | Uvezeno u biblioteku |
| Fasadna rasvjeta | `lighting.facade_light` | Fasadna rasvjeta | vlastiti detaljni model | Uvezeno u biblioteku |
| Dekorativna rasvjeta | `lighting.garden_light` | Dekorativna vrtna svjetiljka | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `lighting.led_strip` | LED traka | parametarska površina/trasa s teksturom | Uvezeno u biblioteku |
|  | `lighting.pergola_light` | Svjetiljka pergole | vlastiti detaljni model | Uvezeno u biblioteku |
| Rasvjeta površina | `lighting.area_floodlight` | Reflektorska rasvjeta površine | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `lighting.street_light` | Ulična svjetiljka | vlastiti detaljni model | Uvezeno u biblioteku |
| Zidne svjetiljke | `lighting.wall_light` | Zidna svjetiljka | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `lighting.step_light` | Svjetiljka za stepenice | vlastiti detaljni model | Uvezeno u biblioteku |

## Ograde i zidovi (`Assets/Fences_Walls`)

| Objekat iz specifikacije | ID modela = tip elementa | Naziv modela | Vrsta modela | Status |
|---|---|---|---|---|
| Gabionski zidovi | `wall.gabion` | Gabionski zid | vlastiti detaljni model | Uvezeno u biblioteku |
| Staklene ograde | `wall.glass_railing` | Staklena ograda | vlastiti detaljni model | Uvezeno u biblioteku |
| Metalne ograde | `wall.metal_fence` | Metalna ograda od šipki | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `wall.mesh_fence` | Ograda od zavarene mreže | vlastiti detaljni model | Uvezeno u biblioteku |
| Drvene ograde | `wall.timber_fence` | Drvena ograda | vlastiti detaljni model | Uvezeno u biblioteku |
| Dekorativne ograde | `wall.decorative_fence` | Dekorativna ograda | vlastiti detaljni model | Uvezeno u biblioteku |
| Potporni zidovi | `wall.retaining` | Potporni zid | vlastiti detaljni model | Uvezeno u biblioteku |
| Vrtni zidovi | `wall.stone` | Kameni zid | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `wall.concrete` | Betonski zid | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `wall.planter_wall` | Zid žardinjere | vlastiti detaljni model | Uvezeno u biblioteku |
| Automatske kapije | `wall.sliding_gate` | Automatska klizna kapija | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `wall.access_barrier` | Rampa kontrole pristupa | vlastiti detaljni model | Uvezeno u biblioteku |
| Pješačke kapije | `wall.pedestrian_gate` | Pješačka kapija | vlastiti detaljni model | Uvezeno u biblioteku |
| Sistemi ograđivanja | `wall.mesh_fence` | Ograda od zavarene mreže | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `wall.vehicle_gate` | Kolna kapija (dvokrilna) | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `wall.swing_gate` | Krilna kapija | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `wall.safety_railing` | Zaštitna ograda | vlastiti detaljni model | Uvezeno u biblioteku |

## Nadstrešnice i zasjena (`Assets/Canopies`)

| Objekat iz specifikacije | ID modela = tip elementa | Naziv modela | Vrsta modela | Status |
|---|---|---|---|---|
| Bioklimatske pergole | `structure.bioclimatic_pergola` | Bioklimatska pergola (pomične lamele) | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `structure.pergola` | Pergola | vlastiti detaljni model | Uvezeno u biblioteku |
| Solarne nadstrešnice | `structure.solar_canopy` | Solarna nadstrešnica | vlastiti detaljni model | Uvezeno u biblioteku |
| Solarni krovovi | `structure.solar_canopy` | Solarna nadstrešnica | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `sustainability.solar_array` | Solarna elektrana na tlu | vlastiti detaljni model | Uvezeno u biblioteku |
| Membranske konstrukcije | `structure.tensile_membrane` | Membranska nadstrešnica (pojednostavljeno) | vlastiti detaljni model | Uvezeno u biblioteku |
| Vanjska skloništa | `structure.seating_shelter` | Nadstrešnica za sjedenje | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `structure.bus_shelter` | Nadstrešnica tipa stajališta | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `structure.gazebo` | Sjenica | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `structure.pavilion` | Vanjski paviljon | vlastiti detaljni model | Uvezeno u biblioteku |
| Nadstrešnice za bicikle | `cycling.bike_shelter` | Natkriveni parking za bicikle | vlastiti detaljni model | Uvezeno u biblioteku |
| Nadstrešnice za parking | `structure.carport` | Nadstrešnica za automobile | vlastiti detaljni model | Uvezeno u biblioteku |
| Jedra za sjenu | `climate.shade_sail` | Jedro za sjenu | vlastiti detaljni model | Uvezeno u biblioteku |

## Mobilijar i rekreacija (`Assets/Furniture_Recreation`)

| Objekat iz specifikacije | ID modela = tip elementa | Naziv modela | Vrsta modela | Status |
|---|---|---|---|---|
| Klupe | `furniture.bench` | Klupa s naslonom | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `furniture.bench_backless` | Klupa bez naslona | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `furniture.accessible_bench` | Pristupačna klupa (s rukohvatima) | vlastiti detaljni model | Uvezeno u biblioteku |
| Stolovi | `furniture.dining_table` | Vanjski trpezarijski stol | vlastiti detaljni model | Uvezeno u biblioteku |
| Korpe za otpatke | `furniture.waste_bin` | Korpa za otpatke | vlastiti detaljni model | Uvezeno u biblioteku |
| Vanjske fitness sprave | `recreation.fitness_station` | Vanjska fitness sprava | vlastiti detaljni model | Uvezeno u biblioteku |
| Igrališta | `recreation.playground_unit` | Dječija igračka sprava | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `recreation.swing` | Ljuljačka | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `recreation.climbing_structure` | Penjalica | vlastiti detaljni model | Uvezeno u biblioteku |
| Tobogani | `recreation.slide` | Tobogan | vlastiti detaljni model | Uvezeno u biblioteku |
| Trampolini | `recreation.trampoline` | Ukopani trampolin | vlastiti detaljni model | Uvezeno u biblioteku |
| Zip-line | `recreation.zip_line` | Zip-line (žičara) | vlastiti detaljni model | Uvezeno u biblioteku |
| Piknik mobilijar | `furniture.picnic_table` | Stol za piknik | vlastiti detaljni model | Uvezeno u biblioteku |
| Vanjsko sjedenje | `furniture.chair` | Vanjska stolica | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `furniture.lounger` | Ležaljka | vlastiti detaljni model | Uvezeno u biblioteku |
| Sportska i rekreativna oprema | `recreation.basketball_hoop` | Košarkaški koš | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `recreation.table_tennis` | Vanjski stol za stoni tenis | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `recreation.chess_table` | Vanjski šahovski sto | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `recreation.football_goal` | Gol za mali fudbal | vlastiti detaljni model | Uvezeno u biblioteku |

## Eko tehnologije (`Assets/Eco_Technologies`)

| Objekat iz specifikacije | ID modela = tip elementa | Naziv modela | Vrsta modela | Status |
|---|---|---|---|---|
| Solarni paneli | `sustainability.solar_array` | Solarna elektrana na tlu | vlastiti detaljni model | Uvezeno u biblioteku |
| Toplotne pumpe | `sustainability.heat_pump` | Toplotna pumpa (vanjska jedinica) | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `sustainability.outdoor_unit` | Vanjska tehnička jedinica | vlastiti detaljni model | Uvezeno u biblioteku |
| Vjetroturbine s vertikalnom osom | `sustainability.wind_turbine` | Vjetroturbina s vertikalnom osom | vlastiti detaljni model | Uvezeno u biblioteku |
| Kućišta energetske opreme | `sustainability.energy_cabinet` | Ormar za praćenje energije | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `sustainability.battery_cabinet` | Ormar baterijskog spremnika | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `sustainability.inverter` | Invertor | vlastiti detaljni model | Uvezeno u biblioteku |
| Održiva infrastruktura lokacije | `sustainability.rainwater_harvesting` | Rezervoar za prikupljanje kišnice | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `sustainability.greywater_unit` | Uređaj za prečišćavanje sive vode | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `sustainability.compost_station` | Kompostište | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `sustainability.suds_crate` | Retencijski modul održive odvodnje | vlastiti detaljni model | Uvezeno u biblioteku |

## Vanjski klimatski sistemi (`Assets/Outdoor_Climate`)

| Objekat iz specifikacije | ID modela = tip elementa | Naziv modela | Vrsta modela | Status |
|---|---|---|---|---|
| Sistemi za raspršivanje vode | `climate.misting_post` | Stub za raspršivanje vode (misting) | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `climate.misting_line` | Linija sistema za raspršivanje vode | vlastiti detaljni model | Uvezeno u biblioteku |
| Grijane terase | `climate.heated_zone` | Grijana zona terase | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `climate.patio_heater` | Samostojeći grijač terase | vlastiti detaljni model | Uvezeno u biblioteku |
| Infracrveni grijači | `climate.infrared_heater` | Infracrveni grijač (montažni) | vlastiti detaljni model | Uvezeno u biblioteku |
| Vanjski rashladni elementi | `climate.evaporative_cooler` | Evaporativni rashladni uređaj | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `climate.cooling_unit` | Vanjska rashladna jedinica | vlastiti detaljni model | Uvezeno u biblioteku |
| Vanjska klimatska oprema | `climate.ventilation_unit` | Vanjska ventilacijska oprema | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `climate.awning` | Pomična tenda | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `climate.wind_screen` | Zaštita od vjetra | vlastiti detaljni model | Uvezeno u biblioteku |

## Dekorativni elementi (`Assets/Decorative`)

| Objekat iz specifikacije | ID modela = tip elementa | Naziv modela | Vrsta modela | Status |
|---|---|---|---|---|
| Skulpture | `decorative.sculpture` | Skulptura (zamjenski ili vlastiti model) | vlastiti detaljni model | Uvezeno u biblioteku |
| Zeleni zidovi | `landscape.green_wall` | Zeleni zid | vlastiti detaljni model | Uvezeno u biblioteku |
| Murali | `decorative.mural` | Mural (oslikana površina) | vlastiti detaljni model | Uvezeno u biblioteku |
| Dekorativni paravani | `decorative.screen` | Dekorativni paravan | vlastiti detaljni model | Uvezeno u biblioteku |
| Akcentna rasvjeta | `lighting.feature_light` | Akcentna rasvjeta (usmjerena naviše) | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `decorative.light_installation` | Dekorativna svjetlosna instalacija | vlastiti detaljni model | Uvezeno u biblioteku |
| Umjetnički elementi pejzaža | `decorative.public_art` | Javna umjetnička instalacija | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `decorative.boulder` | Dekorativni kamen / gromada | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `decorative.focal_point` | Fokusna tačka pejzaža | vlastiti detaljni model | Uvezeno u biblioteku |
| Dekorativne konstrukcije | `decorative.entrance_monument` | Ulazno obilježje | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `decorative.decorative_wall` | Dekorativni zid | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `decorative.signage_structure` | Nosiva konstrukcija natpisa | vlastiti detaljni model | Uvezeno u biblioteku |
|  | `decorative.feature_planter` | Akcentna žardinjera | vlastiti detaljni model | Uvezeno u biblioteku |

## Informativni i sigurnosni sistemi (`Assets/Information_Security`)

| Objekat iz specifikacije | ID modela = tip elementa | Naziv modela | Vrsta modela | Status |
|---|---|---|---|---|
| QR informativni paneli | `info.qr_panel` | Informativni panel s QR kodom | vlastiti detaljni model | Uvezeno u biblioteku |
| Informativni kiosci | `info.digital_kiosk` | Digitalni kiosk | vlastiti detaljni model | Uvezeno u biblioteku |
| CCTV kamere | `info.cctv_camera` | Video nadzorna kamera (na stubu) | vlastiti detaljni model | Uvezeno u biblioteku |
| Vanjski audio sistemi | `info.speaker` | Vanjski zvučnik / ozvučenje (stub) | vlastiti detaljni model | Uvezeno u biblioteku |
| Znakovi za orijentaciju | `info.wayfinding_sign` | Orijentacijski znak (monolit) | vlastiti detaljni model | Uvezeno u biblioteku |
| Putokazi | `info.directional_sign` | Putokaz | vlastiti detaljni model | Uvezeno u biblioteku |
| Planovi lokacije | `info.site_map` | Plan lokacije (stalak s kartom) | vlastiti detaljni model | Uvezeno u biblioteku |
| Informativne table | `info.site_board` | Informativna tabla lokacije | vlastiti detaljni model | Uvezeno u biblioteku |
