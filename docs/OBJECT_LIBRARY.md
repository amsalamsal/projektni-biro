# Object library

Generated from `shared/catalog` by `tools/gen_object_library.py` - do not edit by hand.

254 element types, 43 parametric builders. Types marked **review** carry values that must be confirmed against local regulations or a specialist design (they are reported as review items by validation).

## Buildings & References (2)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `building.footprint` | Building footprint (reference massing) | Buildings | massing | polygon | buildings |
| `building.entrance` | Building entrance reference | References | marker | point | buildings |

## Site (2)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `site.boundary` | Site boundary | Boundaries | outline | polygon | site_boundary |
| `site.existing_feature` | Existing feature outline | Existing conditions | outline | polygon | existing |

## Surfaces (23)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `surface.concrete_paving` | Concrete paving | Paving | slab | polygon | surfaces |
| `surface.natural_stone` | Natural stone paving | Paving | slab | polygon | surfaces |
| `surface.porcelain_tiles` | Outdoor porcelain / ceramic tiles | Paving | slab | polygon | surfaces |
| `surface.asphalt` | Asphalt surface | Paving | slab | polygon | roads |
| `surface.permeable_paving` | Permeable paving | Paving | slab | polygon | surfaces |
| `surface.grass_grid` | Grass grid / reinforced turf | Paving | slab | polygon | surfaces |
| `surface.gravel` | Gravel / decorative aggregate | Loose fill | slab | polygon | surfaces |
| `surface.rubber_safety` | Rubber safety surface | Safety surfaces | slab | polygon | recreation |
| `surface.tactile_paving` | Tactile paving | Paving | slab | polygon | surfaces |
| `surface.sand` | Sand / loose-fill play surface | Loose fill | slab | polygon | recreation |
| `surface.mulch` | Bark mulch | Loose fill | slab | polygon | planting |
| `surface.lawn` | Lawn | Soft landscape | slab | polygon | planting |
| `surface.timber_deck` | Timber decking / platform | Decks and terraces | slab | polygon | surfaces |
| `surface.raised_terrace` | Raised terrace floor (pedestal system) | Decks and terraces | slab | polygon | surfaces |
| `surface.sports_court` | Sports court surface | Sports surfaces | slab | polygon | recreation |
| `edge.kerb` | Kerb | Edges and transitions | wall | path | roads |
| `edge.flush_kerb` | Flush kerb / transition edge | Edges and transitions | wall | path | surfaces |
| `edge.edging` | Edging / border strip | Edges and transitions | wall | path | surfaces |
| `edge.retaining_edge` | Retaining edge | Edges and transitions | wall | path | surfaces |
| `edge.surface_joint` | Movement / surface joint | Edges and transitions | linear_slab | path | surfaces |
| `level.steps` | Steps | Level changes | steps | polygon | surfaces |
| `level.ramp` | Ramp **review** | Level changes | ramp | polygon | surfaces |
| `level.landing` | Landing | Level changes | slab | polygon | surfaces |

## Roads & Mobility (21)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `road.access_road` | Internal access road (centreline) | Roads | linear_slab | path | roads |
| `road.driveway` | Driveway (centreline) | Roads | linear_slab | path | roads |
| `road.service_route` | Service route (centreline) | Roads | linear_slab | path | roads |
| `road.fire_access` | Fire-service access route (centreline) **review** | Roads | linear_slab | path | roads |
| `road.turning_area` | Turning area | Roads | slab | polygon | roads |
| `road.swept_path_placeholder` | Swept-path placeholder (not an analysis) | Roads | zone | polygon | anno_clearance |
| `road.parking_bay` | Parking bay | Parking | parking_bay | point | parking |
| `road.parking_bay_accessible` | Accessible parking bay **review** | Parking | parking_bay | point | parking |
| `road.parking_bay_ev` | EV charging parking bay | Parking | parking_bay | point | parking |
| `road.loading_bay` | Loading / unloading bay | Parking | parking_bay | point | parking |
| `road.pedestrian_crossing` | Pedestrian crossing (striped) | Markings | stripes | polygon | roads_marking |
| `road.hatched_area` | Hatched marking area | Markings | stripes | polygon | roads_marking |
| `road.marking_line` | Road marking line | Markings | linear_slab | path | roads_marking |
| `road.arrow` | Traffic arrow | Markings | arrow_marking | point | roads_marking |
| `road.speed_hump` | Speed hump | Traffic calming | box_equipment | point | roads |
| `road.raised_crossing` | Raised crossing / table | Traffic calming | slab | polygon | roads |
| `road.bollard` | Bollard | Barriers | bollard | point | furniture |
| `road.removable_bollard` | Removable bollard | Barriers | bollard | point | furniture |
| `road.vehicle_barrier` | Vehicle barrier (boom) | Barriers | gate | point | roads |
| `road.wheel_stop` | Parking stop / wheel guide | Parking | box_equipment | point | parking |
| `road.traffic_sign` | Traffic sign | Signage | sign_post | point | roads_marking |

## Cycling Infrastructure (15)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `cycling.bike_path` | Dedicated bicycle path (centreline) | Paths and lanes | linear_slab | path | cycling |
| `cycling.shared_path` | Shared-use path (centreline) | Paths and lanes | linear_slab | path | pedestrian |
| `cycling.bike_lane` | Bicycle lane marking | Paths and lanes | linear_slab | path | cycling |
| `cycling.separation_line` | Bicycle / pedestrian separation line | Paths and lanes | linear_slab | path | cycling |
| `cycling.bike_box` | Bike box (advanced stop area) | Paths and lanes | slab | polygon | cycling |
| `cycling.bike_stand` | Bicycle stand (hoop) | Parking | bike_stand | point | cycling |
| `cycling.bike_rack` | Bicycle rack (row of stands) | Parking | bike_stand | point | cycling |
| `cycling.bike_shelter` | Covered bicycle shelter | Parking | shelter | point | structures |
| `cycling.bike_locker` | Bicycle locker | Parking | box_equipment | point | cycling |
| `cycling.share_station` | Bicycle-sharing station **review** | Services | box_equipment | point | cycling |
| `cycling.repair_station` | Bicycle repair station | Services | box_equipment | point | cycling |
| `cycling.pump` | Public bicycle pump | Services | cylinder_equipment | point | cycling |
| `cycling.charging_point` | Bicycle charging point | Services | box_equipment | point | cycling |
| `cycling.route_marking` | Bicycle route marking | Signage | arrow_marking | point | cycling |
| `cycling.direction_sign` | Bicycle directional sign | Signage | sign_post | point | cycling |

## Water & Drainage (22)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `water.swimming_pool` | Swimming pool | Pools | pool | polygon | water |
| `water.infinity_pool` | Infinity pool | Pools | pool | polygon | water |
| `water.decorative_pond` | Decorative pond | Ponds and features | pool | polygon | water |
| `water.reflecting_pool` | Reflecting pool | Ponds and features | pool | polygon | water |
| `water.fountain` | Fountain | Ponds and features | fountain | point | water |
| `water.water_jet` | Water jet (ground nozzle) | Ponds and features | cylinder_equipment | point | water |
| `water.cascade_wall` | Waterfall / cascade wall | Ponds and features | wall | path | water |
| `water.equipment_zone` | Pool equipment zone | Pools | zone | polygon | water |
| `water.drainage_channel` | Linear drainage channel | Surface drainage | channel | path | drainage |
| `water.slot_drain` | Slot drain | Surface drainage | channel | path | drainage |
| `water.overflow_channel` | Overflow channel | Surface drainage | channel | path | drainage |
| `water.surface_inlet` | Surface inlet / gully grate | Surface drainage | chamber | point | drainage |
| `water.inspection_chamber` | Drainage inspection chamber | Underground drainage | chamber | point | drainage |
| `water.drain_pipe` | Drainage pipe | Underground drainage | pipe | path | drainage |
| `water.rainwater_tank` | Underground rainwater tank | Rainwater | tank | point | drainage |
| `water.rainwater_collector` | Rainwater collection butt (above ground) | Rainwater | tank | point | drainage |
| `water.infiltration_crates` | Infiltration / attenuation crates | Rainwater | tank | point | drainage |
| `water.retention_basin` | Retention / detention basin | Rainwater | basin | polygon | drainage |
| `water.rain_garden` | Rain garden | Rainwater | basin | polygon | drainage |
| `water.bioswale` | Bioswale | Rainwater | basin | polygon | drainage |
| `water.irrigation_line` | Irrigation line | Irrigation and supply | pipe | path | drainage |
| `water.supply_point` | Water supply point / tap | Irrigation and supply | box_equipment | point | drainage |

## Landscape (27)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `landscape.tree_deciduous` | Deciduous tree **review** | Trees | tree | point | planting_trees |
| `landscape.tree_evergreen` | Evergreen tree **review** | Trees | tree | point | planting_trees |
| `landscape.tree_fruit` | Fruit tree **review** | Trees | tree | point | planting_trees |
| `landscape.tree_columnar` | Columnar tree **review** | Trees | tree | point | planting_trees |
| `landscape.tree_existing` | Existing tree | Trees | tree | point | existing |
| `landscape.orchard_tree` | Orchard tree (row planting) **review** | Trees | tree | point | planting_trees |
| `landscape.shrub` | Shrub | Shrubs and hedges | shrub | point | planting |
| `landscape.hedge` | Hedge | Shrubs and hedges | wall | path | planting |
| `landscape.flower_bed` | Flower bed | Planting beds | planting_area | polygon | planting |
| `landscape.rose_garden` | Rose garden | Planting beds | planting_area | polygon | planting |
| `landscape.ornamental_grasses` | Ornamental grasses | Planting beds | planting_area | polygon | planting |
| `landscape.perennials` | Perennial planting | Planting beds | planting_area | polygon | planting |
| `landscape.ground_cover` | Ground cover | Planting beds | planting_area | polygon | planting |
| `landscape.planting_bed` | Planting bed (soil only) | Planting beds | planting_area | polygon | planting |
| `landscape.meadow` | Natural meadow | Lawns and meadows | planting_area | polygon | planting |
| `landscape.lawn_area` | Lawn area | Lawns and meadows | planting_area | polygon | planting |
| `landscape.climber` | Climbing plant | Vertical greening | box_equipment | point | planting |
| `landscape.green_roof` | Green roof | Vertical greening | slab | polygon | planting |
| `landscape.green_wall` | Green wall | Vertical greening | wall | path | planting |
| `landscape.vertical_garden` | Vertical garden module | Vertical greening | box_equipment | point | planting |
| `landscape.raised_planter` | Raised planter | Planters | planter | polygon | planting |
| `landscape.planting_container` | Large planting container | Planters | cylinder_equipment | point | planting |
| `landscape.tree_grate` | Tree pit grate | Tree protection | box_equipment | point | planting_trees |
| `landscape.tree_pit` | Tree pit (soil volume) | Tree protection | box_equipment | point | planting_trees |
| `landscape.root_protection_zone` | Root protection zone **review** | Tree protection | circle_zone | point | anno_clearance |
| `landscape.tree_protection_barrier` | Tree protection barrier | Tree protection | fence | path | construction |
| `landscape.irrigation_point` | Irrigation point / valve box | Irrigation | cylinder_equipment | point | planting |

## Utilities (20)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `utility.waste_container_ug_3m3` | Underground waste container 3 m³ **review** | Waste | underground_container | point | waste |
| `utility.waste_container_ug_5m3` | Underground waste container 5 m³ **review** | Waste | underground_container | point | waste |
| `utility.waste_container_semi` | Semi-underground waste container **review** | Waste | underground_container | point | waste |
| `utility.waste_container_above` | Above-ground waste container | Waste | box_equipment | point | waste |
| `utility.recycling_island` | Recycling island | Waste | zone | polygon | waste |
| `utility.cistern` | Underground cistern | Tanks | tank | point | utilities |
| `utility.water_tank` | Water tank (above ground) | Tanks | tank | point | utilities |
| `utility.septic_tank` | Septic tank | Tanks | tank | point | utilities |
| `utility.trench` | Utility trench (envelope) | Routes | linear_slab | path | utilities |
| `utility.service_corridor` | Underground service corridor | Routes | linear_slab | path | utilities |
| `utility.cable_duct` | Cable duct | Routes | pipe | path | utilities |
| `utility.water_pipe` | Water supply pipe | Routes | pipe | path | utilities |
| `utility.service_connection` | Service connection | Routes | pipe | path | utilities |
| `utility.electrical_chamber` | Electrical inspection chamber | Chambers | chamber | point | utilities |
| `utility.telecom_chamber` | Telecommunications chamber | Chambers | chamber | point | utilities |
| `utility.water_meter_chamber` | Water meter chamber | Chambers | chamber | point | utilities |
| `utility.valve_chamber` | Valve chamber | Chambers | chamber | point | utilities |
| `utility.manhole` | Manhole | Chambers | chamber | point | utilities |
| `utility.pump_chamber` | Pump chamber | Chambers | chamber | point | utilities |
| `utility.equipment_enclosure` | Equipment enclosure **review** | Equipment | box_equipment | point | utilities |

## Lighting (18)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `lighting.street_light` | Street light | Poles | pole_mounted | point | lighting |
| `lighting.pedestrian_light` | Pedestrian light (post-top) | Poles | pole_mounted | point | lighting |
| `lighting.solar_light` | Solar light | Poles | pole_mounted | point | lighting |
| `lighting.smart_light` | Smart light (with sensor) | Poles | pole_mounted | point | lighting |
| `lighting.bollard_light` | Bollard light | Low-level | bollard | point | lighting |
| `lighting.path_light` | Pathway light | Low-level | bollard | point | lighting |
| `lighting.recessed_ground_light` | Recessed ground light | Low-level | cylinder_equipment | point | lighting |
| `lighting.underwater_light` | Underwater light | Low-level | cylinder_equipment | point | lighting |
| `lighting.step_light` | Step light | Wall-mounted | box_equipment | point | lighting |
| `lighting.wall_light` | Wall-mounted light | Wall-mounted | box_equipment | point | lighting |
| `lighting.facade_light` | Facade light | Wall-mounted | box_equipment | point | lighting |
| `lighting.garden_light` | Decorative garden light | Decorative | cylinder_equipment | point | lighting |
| `lighting.led_strip` | LED strip | Decorative | linear_slab | path | lighting |
| `lighting.pergola_light` | Pergola light | Decorative | box_equipment | point | lighting |
| `lighting.feature_light` | Architectural feature light (uplight) | Decorative | cylinder_equipment | point | lighting |
| `lighting.control_cabinet` | Lighting control cabinet **review** | Control | box_equipment | point | lighting |
| `lighting.circuit_route` | Lighting circuit route | Control | pipe | path | lighting_circuits |
| `lighting.coverage_zone` | Illustrative lighting coverage (not photometric) | Control | circle_zone | point | anno_clearance |

## Walls & Fences (16)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `wall.metal_fence` | Metal bar fence | Fences | fence | path | fences |
| `wall.mesh_fence` | Welded-mesh fence | Fences | fence | path | fences |
| `wall.glass_railing` | Glass railing | Railings | fence | path | fences |
| `wall.timber_fence` | Timber fence | Fences | fence | path | fences |
| `wall.decorative_fence` | Decorative fence | Fences | fence | path | fences |
| `wall.safety_railing` | Safety railing **review** | Railings | fence | path | fences |
| `wall.gabion` | Gabion wall | Walls | wall | path | walls |
| `wall.concrete` | Concrete wall | Walls | wall | path | walls |
| `wall.stone` | Stone wall | Walls | wall | path | walls |
| `wall.retaining` | Retaining wall **review** | Retaining | wall | path | walls |
| `wall.planter_wall` | Planter wall | Walls | wall | path | walls |
| `wall.pedestrian_gate` | Pedestrian gate | Gates | gate | point | fences |
| `wall.vehicle_gate` | Vehicle gate (double swing) | Gates | gate | point | fences |
| `wall.sliding_gate` | Automatic sliding gate | Gates | gate | point | fences |
| `wall.swing_gate` | Swing gate | Gates | gate | point | fences |
| `wall.access_barrier` | Access-control barrier | Gates | gate | point | fences |

## Structures (12)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `structure.pergola` | Pergola | Pergolas | pergola | polygon | structures |
| `structure.bioclimatic_pergola` | Bioclimatic pergola (adjustable louvers) | Pergolas | pergola | polygon | structures |
| `structure.retractable_shade` | Pergola with retractable shade | Pergolas | pergola | polygon | structures |
| `structure.tensile_membrane` | Tensile membrane canopy (simplified) | Canopies | sail | polygon | structures |
| `structure.carport` | Carport | Canopies | pergola | polygon | structures |
| `structure.solar_canopy` | Solar canopy | Canopies | pergola | polygon | structures |
| `structure.bus_shelter` | Bus-style shelter | Shelters | shelter | point | structures |
| `structure.gazebo` | Gazebo | Pavilions | shelter | point | structures |
| `structure.pavilion` | Outdoor pavilion | Pavilions | shelter | point | structures |
| `structure.terrace_roof` | Terrace roof (glass) | Canopies | pergola | polygon | structures |
| `structure.entrance_canopy` | Lightweight entrance canopy | Canopies | box_equipment | point | structures |
| `structure.seating_shelter` | Outdoor seating shelter | Shelters | shelter | point | structures |

## Furniture & Recreation (25)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `furniture.bench` | Bench with backrest | Seating | bench | point | furniture |
| `furniture.bench_backless` | Backless bench | Seating | bench | point | furniture |
| `furniture.accessible_bench` | Accessible bench (armrests) | Seating | bench | point | furniture |
| `furniture.chair` | Outdoor chair | Seating | bench | point | furniture |
| `furniture.lounger` | Lounge seating | Seating | bench | point | furniture |
| `furniture.picnic_table` | Picnic table | Tables | table | point | furniture |
| `furniture.dining_table` | Outdoor dining table | Tables | table | point | furniture |
| `furniture.waste_bin` | Waste bin | Bins | cylinder_equipment | point | furniture |
| `furniture.ashtray` | Ashtray / cigarette bin | Bins | cylinder_equipment | point | furniture |
| `furniture.planter` | Planter (furniture) | Planters | box_equipment | point | furniture |
| `furniture.drinking_fountain` | Drinking fountain | Amenities | box_equipment | point | furniture |
| `furniture.outdoor_shower` | Outdoor shower | Amenities | pole_mounted | point | furniture |
| `furniture.pet_waste_station` | Pet waste station | Amenities | box_equipment | point | furniture |
| `recreation.fitness_station` | Outdoor fitness equipment **review** | Fitness | box_equipment | point | recreation |
| `recreation.playground_unit` | Children's play unit **review** | Playgrounds | box_equipment | point | recreation |
| `recreation.slide` | Slide **review** | Playgrounds | box_equipment | point | recreation |
| `recreation.swing` | Swing **review** | Playgrounds | swing | point | recreation |
| `recreation.climbing_structure` | Climbing structure **review** | Playgrounds | box_equipment | point | recreation |
| `recreation.trampoline` | In-ground trampoline **review** | Playgrounds | cylinder_equipment | point | recreation |
| `recreation.zip_line` | Zip-line installation **review** | Playgrounds | cable_line | path | recreation |
| `recreation.table_tennis` | Outdoor table tennis | Sports | table | point | recreation |
| `recreation.basketball_hoop` | Basketball hoop | Sports | sign_post | point | recreation |
| `recreation.chess_table` | Outdoor chess table | Sports | table | point | recreation |
| `recreation.platform` | Recreational platform | Sports | slab | polygon | recreation |
| `recreation.safety_zone` | Playground safety envelope **review** | Playgrounds | zone | polygon | anno_clearance |

## Sustainability (13)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `sustainability.solar_array` | Ground-mounted solar array | Solar | solar_array | point | sustainability |
| `sustainability.inverter` | Inverter **review** | Solar | box_equipment | point | sustainability |
| `sustainability.battery_cabinet` | Battery storage cabinet **review** | Storage | box_equipment | point | sustainability |
| `sustainability.ev_charger_pedestal` | EV charging station (pedestal) | EV charging | box_equipment | point | parking |
| `sustainability.ev_charger_wallbox` | EV charger (wallbox) | EV charging | box_equipment | point | parking |
| `sustainability.heat_pump` | Heat pump (outdoor unit) **review** | Technical units | box_equipment | point | sustainability |
| `sustainability.outdoor_unit` | Outdoor technical unit **review** | Technical units | box_equipment | point | sustainability |
| `sustainability.wind_turbine` | Vertical-axis wind turbine | Wind | vawt | point | sustainability |
| `sustainability.rainwater_harvesting` | Rainwater harvesting tank | Water | tank | point | sustainability |
| `sustainability.greywater_unit` | Greywater treatment unit **review** | Water | box_equipment | point | sustainability |
| `sustainability.compost_station` | Composting station | Waste | box_equipment | point | sustainability |
| `sustainability.energy_cabinet` | Energy monitoring cabinet | Technical units | box_equipment | point | sustainability |
| `sustainability.suds_crate` | SuDS attenuation unit | Water | tank | point | drainage |

## Climate Systems (11)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `climate.misting_line` | Misting system line | Cooling | pipe | path | climate |
| `climate.infrared_heater` | Infrared heater (mounted) **review** | Heating | box_equipment | point | climate |
| `climate.patio_heater` | Patio heater (freestanding) **review** | Heating | cylinder_equipment | point | climate |
| `climate.heated_zone` | Heated terrace zone | Heating | zone | polygon | climate |
| `climate.cooling_unit` | Outdoor cooling unit **review** | Cooling | box_equipment | point | climate |
| `climate.evaporative_cooler` | Evaporative cooling installation **review** | Cooling | box_equipment | point | climate |
| `climate.shade_sail` | Shade sail | Shading | sail | polygon | climate |
| `climate.awning` | Retractable awning | Shading | box_equipment | point | climate |
| `climate.wind_screen` | Wind screen | Shading | fence | path | climate |
| `climate.ventilation_unit` | Outdoor ventilation equipment **review** | Ventilation | box_equipment | point | climate |
| `climate.comfort_zone` | Thermal comfort equipment zone | Zones | zone | polygon | climate |

## Decorative Elements (11)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `decorative.sculpture` | Sculpture (placeholder or custom asset) | Art | box_equipment | point | decorative |
| `decorative.public_art` | Public art installation | Art | box_equipment | point | decorative |
| `decorative.decorative_wall` | Decorative wall | Walls and screens | wall | path | decorative |
| `decorative.mural` | Mural (mapped surface) | Art | wall | path | decorative |
| `decorative.feature_planter` | Feature planter | Planters | planter | polygon | decorative |
| `decorative.boulder` | Decorative stone / boulder | Stones | boulder | point | decorative |
| `decorative.focal_point` | Landscape focal point | Art | cylinder_equipment | point | decorative |
| `decorative.light_installation` | Decorative lighting installation | Lighting art | box_equipment | point | decorative |
| `decorative.entrance_monument` | Entrance monument | Monuments | box_equipment | point | decorative |
| `decorative.screen` | Decorative screen | Walls and screens | fence | path | decorative |
| `decorative.signage_structure` | Signage structure | Monuments | sign_post | point | decorative |

## Information Systems (16)

| Type id | Name | Subcategory | Builder | Geometry | Layer |
|---|---|---|---|---|---|
| `info.site_board` | Site information board | Information | sign_post | point | information |
| `info.qr_panel` | QR-code information panel | Information | sign_post | point | information |
| `info.directional_sign` | Directional sign | Wayfinding | sign_post | point | information |
| `info.wayfinding_sign` | Wayfinding sign (monolith) | Wayfinding | box_equipment | point | information |
| `info.building_sign` | Building identification sign | Wayfinding | box_equipment | point | information |
| `info.accessibility_sign` | Accessibility signage | Wayfinding | sign_post | point | information |
| `info.fire_info_point` | Fire-safety information point | Safety | sign_post | point | information |
| `info.digital_kiosk` | Digital kiosk | Information | box_equipment | point | information |
| `info.cctv_camera` | CCTV camera (pole) | Security | pole_mounted | point | information |
| `info.security_cabinet` | Security equipment cabinet | Security | box_equipment | point | information |
| `info.access_reader` | Access-control reader (post) | Security | box_equipment | point | information |
| `info.intercom` | Intercom (post) | Communication | box_equipment | point | information |
| `info.speaker` | Outdoor speaker / PA (pole) | Communication | pole_mounted | point | information |
| `info.wifi_ap` | Wi-Fi access point (pole) | Communication | pole_mounted | point | information |
| `info.emergency_call_point` | Emergency call point | Safety | box_equipment | point | information |
| `info.cable_route` | Communication cable route | Communication | pipe | path | information |

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
