using System;
using System.Collections.Generic;
using SiteDesignPro.Core.Geometry;

namespace SiteDesignPro.Core.Symbols
{
    /// <summary>
    /// Recognisable plan symbols (view from above) for element types that share the generic box or cylinder builder,
    /// so a slide looks like a slide and a heat pump like a heat pump without reading the label. Local frame: metres,
    /// origin at the insertion point, X along the width, Y along the depth, the front (user side) towards -Y.
    /// Very small objects are drawn at a legible minimum size; the real footprint stays inside the drawing.
    /// </summary>
    internal static class PlanSymbols
    {
        /// <summary>Smallest drawn size of an icon, metres (a 0.15 m light would otherwise be a dot).</summary>
        private const double MinIcon = 0.5;

        /// <summary>Builds the symbol for a type id; false when the type has no own symbol (the generic one is used).</summary>
        public static bool TryBuild(string typeId, double w, double d, double dia, List<SymbolPrimitive> items, out string symbol)
        {
            var b = new Pen(items);
            bool ok = Draw(typeId, w, d, dia, b);
            symbol = b.Symbol.Length > 0 ? b.Symbol : typeId.Replace('.', '_');
            return ok;
        }

        private static bool Draw(string typeId, double w, double d, double dia, Pen b)
        {
            double W = Math.Max(w, 0.05), D = Math.Max(d, 0.05);
            double R = Math.Max(dia / 2, 0.03);
            double S = Math.Max(Math.Max(W, D), MinIcon); // icon box for small objects
            double r = Math.Max(R, MinIcon / 2);          // icon radius for small round objects

            switch (typeId)
            {
                // ---------------- children's play and sport ----------------
                case "recreation.slide":
                    {
                        // ladder (rungs) at the left, platform, chute widening to the run-out at the right
                        double lad = Math.Min(W * 0.22, 0.9), plat = Math.Min(W * 0.2, 0.9), cw = D;
                        double x0 = -W / 2, x1 = x0 + lad, x2 = x1 + plat, x3 = W / 2;
                        double lw = cw * 0.8;
                        b.Line(x0, -lw / 2, x1, -lw / 2);
                        b.Line(x0, lw / 2, x1, lw / 2);
                        for (int i = 0; i <= 4; i++)
                        {
                            double x = x0 + (lad * i / 4);
                            b.Line(x, -lw / 2, x, lw / 2);
                        }

                        b.Rect((x1 + x2) / 2, 0, plat, cw);
                        b.Line(x1, -cw / 2, x2, cw / 2);
                        b.Line(x1, cw / 2, x2, -cw / 2);
                        double out1 = cw * 0.6, out2 = cw * 0.85;
                        b.Poly(false, x2, cw * 0.3, x3 - (W * 0.08), out1 / 2, x3, out2 / 2);
                        b.Poly(false, x2, -cw * 0.3, x3 - (W * 0.08), -out1 / 2, x3, -out2 / 2);
                        b.Line(x3, -out2 / 2, x3, out2 / 2);
                        b.Arrow(x2 + ((x3 - x2) * 0.25), 0, x2 + ((x3 - x2) * 0.75), 0, cw * 0.18);
                        b.Symbol = "slide";
                        return true;
                    }

                case "recreation.playground_unit":
                    {
                        // two towers joined by a bridge, a slide from one and a climbing net on the other
                        double t = Math.Min(W, D) * 0.3;
                        double ax = -W / 2 + (t / 2) + (W * 0.05), bx = W / 2 - (t / 2) - (W * 0.05);
                        b.Rect(ax, D * 0.15, t, t);
                        b.Line(ax - (t / 2), (D * 0.15) - (t / 2), ax + (t / 2), (D * 0.15) + (t / 2));
                        b.Rect(bx, D * 0.15, t, t);
                        b.Line(bx - (t / 2), (D * 0.15) + (t / 2), bx + (t / 2), (D * 0.15) - (t / 2));
                        b.Rect(0, D * 0.15, bx - ax - t, t * 0.45);
                        for (int i = 1; i < 6; i++)
                        {
                            double x = ax + (t / 2) + ((bx - ax - t) * i / 6);
                            b.Line(x, (D * 0.15) - (t * 0.225), x, (D * 0.15) + (t * 0.225));
                        }

                        double sy0 = (D * 0.15) - (t / 2), sy1 = -D / 2;
                        b.Line(ax - (t * 0.25), sy0, ax - (t * 0.3), sy1);
                        b.Line(ax + (t * 0.25), sy0, ax + (t * 0.3), sy1);
                        b.Line(ax - (t * 0.3), sy1, ax + (t * 0.3), sy1);
                        double ny0 = (D * 0.15) - (t / 2);
                        b.Grid(bx - (t / 2), -D / 2 + (D * 0.05), bx + (t / 2), ny0, 3, 3);
                        b.Symbol = "play_unit";
                        return true;
                    }

                case "recreation.climbing_structure":
                    {
                        b.Rect(0, 0, W, D);
                        b.Grid(-W / 2, -D / 2, W / 2, D / 2, 4, 4);
                        double h = Math.Min(W, D) * 0.06;
                        foreach ((double x, double y) in new[] { (-W / 2, -D / 2), (W / 2, -D / 2), (W / 2, D / 2), (-W / 2, D / 2) })
                        {
                            b.Rect(x, y, h * 2, h * 2);
                        }

                        b.Symbol = "climbing";
                        return true;
                    }

                case "recreation.fitness_station":
                    {
                        // post, two handles, a seat and two foot plates
                        b.Circle(0, D * 0.25, Math.Min(W, D) * 0.08);
                        b.Line(-W * 0.3, D * 0.25, W * 0.3, D * 0.25);
                        b.Circle(-W * 0.3, D * 0.25, Math.Min(W, D) * 0.05);
                        b.Circle(W * 0.3, D * 0.25, Math.Min(W, D) * 0.05);
                        b.Rect(-W * 0.25, -D * 0.15, W * 0.22, D * 0.3);
                        b.Rect(W * 0.25, -D * 0.15, W * 0.22, D * 0.3);
                        b.Rect(-W * 0.25, -D * 0.38, W * 0.16, D * 0.14);
                        b.Rect(W * 0.25, -D * 0.38, W * 0.16, D * 0.14);
                        b.Line(-W * 0.25, D * 0.25, -W * 0.25, 0);
                        b.Line(W * 0.25, D * 0.25, W * 0.25, 0);
                        b.Symbol = "fitness";
                        return true;
                    }

                case "recreation.football_goal":
                    {
                        // posts and crossbar at the front, net (hatched) behind
                        double p = Math.Max(D * 0.1, 0.08);
                        b.Rect(-W / 2, -D / 2 + (p / 2), p, p);
                        b.Rect(W / 2, -D / 2 + (p / 2), p, p);
                        b.Line(-W / 2, -D / 2 + (p / 2), W / 2, -D / 2 + (p / 2));
                        b.Poly(false, -W / 2, -D / 2 + p, -W / 2 + (D * 0.25), D / 2, W / 2 - (D * 0.25), D / 2, W / 2, -D / 2 + p);
                        int n = 8;
                        for (int i = 1; i < n; i++)
                        {
                            double x = -W / 2 + (W * i / n);
                            b.Line(x, -D / 2 + p, Clamp(x, -W / 2 + (D * 0.25), W / 2 - (D * 0.25)), D / 2);
                        }

                        b.Line(-W / 2 + (D * 0.12), 0, W / 2 - (D * 0.12), 0);
                        b.Symbol = "football_goal";
                        return true;
                    }

                case "recreation.trampoline":
                    {
                        b.Circle(0, 0, R);
                        b.Circle(0, 0, R * 0.78);
                        for (int i = 0; i < 16; i++)
                        {
                            double a = i * Math.PI / 8;
                            b.Line(Math.Cos(a) * R * 0.78, Math.Sin(a) * R * 0.78, Math.Cos(a) * R, Math.Sin(a) * R);
                        }

                        b.Circle(0, 0, R * 0.08);
                        b.Symbol = "trampoline";
                        return true;
                    }

                // ---------------- traffic, parking and cycling ----------------
                case "road.speed_hump":
                    {
                        b.Rect(0, 0, W, D);
                        double c = Math.Min(W, D) * 0.18;
                        for (int i = -1; i <= 1; i++)
                        {
                            double x = i * W * 0.3;
                            b.Poly(false, x - c, -c / 2, x, c / 2, x + c, -c / 2);
                        }

                        b.Line(-W / 2, -D * 0.38, W / 2, -D * 0.38);
                        b.Line(-W / 2, D * 0.38, W / 2, D * 0.38);
                        b.Symbol = "speed_hump";
                        return true;
                    }

                case "road.wheel_stop":
                    {
                        double h = Math.Max(D, 0.12);
                        b.Rounded(0, 0, W, h);
                        b.Circle(-W * 0.3, 0, h * 0.18);
                        b.Circle(W * 0.3, 0, h * 0.18);
                        b.Symbol = "wheel_stop";
                        return true;
                    }

                case "road.parking_meter":
                    {
                        b.Rect(0, 0, S * 0.7, S * 0.55);
                        b.Text(0, S * 0.02, S * 0.32, "P");
                        b.Line(-S * 0.12, -S * 0.2, S * 0.12, -S * 0.2);
                        b.Symbol = "parking_meter";
                        return true;
                    }

                case "cycling.bike_locker":
                    {
                        b.Rect(0, 0, W, D);
                        b.Line(-W / 2, -D / 2, W / 2, D / 2);
                        b.Wheel(-W * 0.22, D * 0.12, Math.Min(W, D) * 0.18);
                        b.Wheel(W * 0.22, -D * 0.12, Math.Min(W, D) * 0.18);
                        b.Symbol = "bike_locker";
                        return true;
                    }

                case "cycling.share_station":
                    {
                        // docking rail with bicycles side by side and a terminal at one end
                        b.Line(-W / 2, D * 0.35, W / 2, D * 0.35);
                        b.Rect(-W / 2 + (D * 0.15), D * 0.35, D * 0.3, D * 0.2);
                        int n = Math.Max(2, (int)Math.Floor((W - (D * 0.4)) / 0.8));
                        for (int i = 0; i < n; i++)
                        {
                            double x = -W / 2 + (D * 0.4) + ((W - (D * 0.4)) * (i + 0.5) / n);
                            b.Line(x, D * 0.35, x, -D * 0.45);
                            b.Circle(x, D * 0.15, 0.06);
                            b.Circle(x, -D * 0.3, 0.06);
                        }

                        b.Symbol = "bike_share";
                        return true;
                    }

                case "cycling.repair_station":
                case "cycling.service_stand":
                    {
                        // stand post with a hanging arm and a wrench
                        b.Circle(0, -S * 0.25, S * 0.08);
                        b.Line(0, -S * 0.25, 0, S * 0.2);
                        b.Line(0, S * 0.2, S * 0.3, S * 0.2);
                        b.Line(S * 0.3, S * 0.2, S * 0.3, S * 0.1);
                        b.Arc(S * 0.3, S * 0.05, S * 0.05, Math.PI, 2 * Math.PI);
                        b.Line(-S * 0.32, -S * 0.05, -S * 0.08, S * 0.19);
                        b.Arc(-S * 0.35, -S * 0.08, S * 0.06, Math.PI * 0.25, Math.PI * 1.75);
                        b.Symbol = typeId == "cycling.repair_station" ? "bike_repair" : "bike_service";
                        return true;
                    }

                case "cycling.pump":
                    {
                        b.Circle(0, 0, r * 0.35);
                        b.Circle(0, 0, r * 0.15);
                        b.Poly(false, r * 0.3, r * 0.2, r * 0.7, r * 0.5, r * 0.85, r * 0.1, r * 0.6, -r * 0.4);
                        b.Rect(r * 0.6, -r * 0.48, r * 0.16, r * 0.16);
                        b.Symbol = "bike_pump";
                        return true;
                    }

                case "cycling.charging_point":
                    {
                        b.Rect(0, 0, S * 0.6, S * 0.6);
                        b.Bolt(0, 0, S * 0.45);
                        b.Wheel(S * 0.45, -S * 0.35, S * 0.12);
                        b.Symbol = "ebike_charger";
                        return true;
                    }

                case "sustainability.ev_charger_pedestal":
                    {
                        b.Rect(0, 0, S * 0.6, S * 0.45);
                        b.Bolt(0, 0, S * 0.35);
                        b.Poly(false, S * 0.3, 0, S * 0.55, -S * 0.15, S * 0.55, -S * 0.45);
                        b.Rect(S * 0.55, -S * 0.5, S * 0.12, S * 0.1);
                        b.Symbol = "ev_pedestal";
                        return true;
                    }

                case "sustainability.ev_charger_wallbox":
                    {
                        b.Wall(S * 0.9, S * 0.25);
                        b.Rect(0, S * 0.25 - (S * 0.15), S * 0.5, S * 0.3);
                        b.Bolt(0, S * 0.1, S * 0.22);
                        b.Poly(false, S * 0.25, S * 0.05, S * 0.4, -S * 0.2, S * 0.2, -S * 0.4);
                        b.Symbol = "ev_wallbox";
                        return true;
                    }

                // ---------------- water and planting ----------------
                case "water.water_jet":
                    {
                        b.Circle(0, 0, r * 0.15);
                        for (int i = 0; i < 8; i++)
                        {
                            double a = i * Math.PI / 4;
                            b.Line(Math.Cos(a) * r * 0.3, Math.Sin(a) * r * 0.3, Math.Cos(a) * r * 0.75, Math.Sin(a) * r * 0.75);
                            b.Circle(Math.Cos(a + 0.4) * r * 0.9, Math.Sin(a + 0.4) * r * 0.9, r * 0.04);
                        }

                        b.Symbol = "water_jet";
                        return true;
                    }

                case "water.supply_point":
                    {
                        // tap: body, spout and a drop
                        b.Rect(0, S * 0.15, S * 0.35, S * 0.3);
                        b.Line(0, 0, 0, -S * 0.22);
                        b.Line(-S * 0.06, -S * 0.22, S * 0.06, -S * 0.22);
                        b.Drop(0, -S * 0.36, S * 0.08);
                        b.Symbol = "water_tap";
                        return true;
                    }

                case "furniture.drinking_fountain":
                    {
                        b.Rect(0, 0, S * 0.8, S * 0.8);
                        b.Circle(0, -S * 0.05, S * 0.25);
                        b.Circle(0, -S * 0.05, S * 0.05);
                        b.Line(0, S * 0.32, 0, S * 0.12);
                        b.Arc(0, S * 0.02, S * 0.1, Math.PI * 0.2, Math.PI * 0.8);
                        b.Symbol = "drinking_fountain";
                        return true;
                    }

                case "landscape.planting_container":
                case "landscape.seasonal_planter":
                    {
                        b.Circle(0, 0, R);
                        b.Circle(0, 0, R * 0.85);
                        b.Scallop(R * 0.6, 7, 0.45);
                        if (typeId == "landscape.seasonal_planter")
                        {
                            foreach ((double x, double y) in new[] { (0.0, 0.25), (-0.25, -0.1), (0.25, -0.15), (0.0, -0.3), (-0.3, 0.2), (0.3, 0.2) })
                            {
                                b.Flower(x * R, y * R, R * 0.07);
                            }
                        }
                        else
                        {
                            b.Circle(0, 0, R * 0.08);
                        }

                        b.Symbol = typeId == "landscape.seasonal_planter" ? "planter_flowers" : "planter_round";
                        return true;
                    }

                case "furniture.planter":
                    {
                        b.Rect(0, 0, W, D);
                        b.Rect(0, 0, W - (Math.Min(W, D) * 0.12), D - (Math.Min(W, D) * 0.12));
                        int n = Math.Max(1, (int)Math.Round(W / D));
                        for (int i = 0; i < n; i++)
                        {
                            double x = -W / 2 + (W * (i + 0.5) / n);
                            b.Scallop(x, 0, D * 0.3, 6, 0.45);
                        }

                        b.Symbol = "planter_box";
                        return true;
                    }

                case "landscape.tree_grate":
                    {
                        b.Rect(0, 0, W, D);
                        b.Circle(0, 0, Math.Min(W, D) * 0.18);
                        for (int i = 0; i < 12; i++)
                        {
                            double a = i * Math.PI / 6;
                            double r0 = Math.Min(W, D) * 0.24, r1 = Math.Min(W, D) * 0.44;
                            b.Line(Math.Cos(a) * r0, Math.Sin(a) * r0, Math.Cos(a) * r1, Math.Sin(a) * r1);
                        }

                        b.Symbol = "tree_grate";
                        return true;
                    }

                case "landscape.tree_pit":
                    {
                        b.Rect(0, 0, W, D);
                        b.Rect(0, 0, W * 0.8, D * 0.8);
                        b.Circle(0, 0, Math.Min(W, D) * 0.08);
                        b.Line(-W * 0.4, -D * 0.4, -W * 0.1, -D * 0.1);
                        b.Line(W * 0.4, D * 0.4, W * 0.1, D * 0.1);
                        b.Line(-W * 0.4, D * 0.4, -W * 0.1, D * 0.1);
                        b.Line(W * 0.4, -D * 0.4, W * 0.1, -D * 0.1);
                        b.Symbol = "tree_pit";
                        return true;
                    }

                case "landscape.irrigation_point":
                    {
                        b.Rect(0, 0, r * 1.2, r * 0.9);
                        b.Circle(0, 0, r * 0.25);
                        b.Line(-r * 0.25, 0, r * 0.25, 0);
                        b.Drop(r * 0.4, r * 0.15, r * 0.12);
                        b.Symbol = "irrigation";
                        return true;
                    }

                case "landscape.climber":
                    {
                        b.Wall(W, D);
                        b.Zigzag(-W / 2, D / 2 - (D * 0.8), W / 2, 6, D * 0.6);
                        for (int i = 0; i < 4; i++)
                        {
                            b.Scallop(-W / 2 + (W * (i + 0.5) / 4), -D * 0.2, Math.Max(D, 0.25) * 0.35, 5, 0.5);
                        }

                        b.Symbol = "climber";
                        return true;
                    }

                case "landscape.vertical_garden":
                    {
                        b.Wall(W, D);
                        int n = Math.Max(2, (int)Math.Round(W / 0.4));
                        for (int i = 0; i < n; i++)
                        {
                            double x = -W / 2 + (W * (i + 0.5) / n);
                            b.Rect(x, -D * 0.05, (W / n) * 0.8, D * 0.5);
                            b.Scallop(x, -D * 0.05, Math.Min(W / n, D) * 0.25, 5, 0.5);
                        }

                        b.Symbol = "vertical_garden";
                        return true;
                    }

                // ---------------- waste ----------------
                case "utility.waste_container_above":
                    {
                        b.Rect(0, 0, W, D);
                        b.Line(-W / 2, D * 0.32, W / 2, D * 0.32);
                        b.Rect(0, D * 0.41, W * 0.3, D * 0.08);
                        foreach ((double x, double y) in new[] { (-W * 0.38, -D * 0.38), (W * 0.38, -D * 0.38), (-W * 0.38, D * 0.2), (W * 0.38, D * 0.2) })
                        {
                            b.Circle(x, y, Math.Min(W, D) * 0.06);
                        }

                        b.Text(0, -D * 0.05, Math.Min(W, D) * 0.25, "R");
                        b.Symbol = "waste_container";
                        return true;
                    }

                case "furniture.waste_bin":
                    {
                        b.Circle(0, 0, R);
                        b.Circle(0, 0, R * 0.75);
                        b.Rect(0, 0, R * 0.9, R * 0.3);
                        b.Symbol = "waste_bin";
                        return true;
                    }

                case "furniture.ashtray":
                    {
                        b.Circle(0, 0, r * 0.6);
                        b.Circle(0, 0, r * 0.45);
                        b.Line(-r * 0.45, 0, r * 0.45, 0);
                        b.Line(0, -r * 0.45, 0, r * 0.45);
                        b.Rect(r * 0.15, r * 0.15, r * 0.5, r * 0.08);
                        b.Symbol = "ashtray";
                        return true;
                    }

                case "furniture.pet_waste_station":
                    {
                        b.Circle(-S * 0.15, 0, S * 0.22);
                        b.Circle(-S * 0.15, 0, S * 0.15);
                        b.Rect(S * 0.27, S * 0.05, S * 0.22, S * 0.3);
                        b.Paw(S * 0.27, S * 0.05, S * 0.06);
                        b.Symbol = "pet_waste";
                        return true;
                    }

                case "sustainability.compost_station":
                    {
                        b.Rect(0, 0, W, D);
                        b.Line(-W / 6, -D / 2, -W / 6, D / 2);
                        b.Line(W / 6, -D / 2, W / 6, D / 2);
                        for (int i = 1; i < 4; i++)
                        {
                            double y = -D / 2 + (D * i / 4);
                            b.Line(-W / 2, y, -W / 2 + (W * 0.06), y);
                            b.Line(W / 2 - (W * 0.06), y, W / 2, y);
                        }

                        for (int k = -1; k <= 1; k++)
                        {
                            b.Scallop(k * W / 3, -D * 0.05, Math.Min(W / 3, D) * 0.22, 5, 0.6);
                        }

                        b.Symbol = "compost";
                        return true;
                    }

                // ---------------- lighting ----------------
                case "lighting.recessed_ground_light":
                    {
                        b.Circle(0, 0, r * 0.3);
                        b.Circle(0, 0, r * 0.18);
                        b.Rays(0, 0, r * 0.42, r * 0.7, 8);
                        b.Symbol = "ground_light";
                        return true;
                    }

                case "lighting.underwater_light":
                    {
                        b.Circle(0, 0, r * 0.3);
                        b.Rays(0, 0, r * 0.4, r * 0.6, 8);
                        b.Wave(-r * 0.8, -r * 0.8, r * 0.8, 3, r * 0.06);
                        b.Wave(-r * 0.8, -r * 0.9, r * 0.8, 3, r * 0.06);
                        b.Symbol = "underwater_light";
                        return true;
                    }

                case "lighting.garden_light":
                    {
                        b.Circle(0, 0, r * 0.22);
                        b.Fill(Circle(0, 0, r * 0.1, 12));
                        b.Rays(0, 0, r * 0.32, r * 0.55, 6);
                        b.Symbol = "garden_light";
                        return true;
                    }

                case "lighting.feature_light":
                    {
                        // spot aimed upward: body and a light cone
                        b.Circle(0, -r * 0.4, r * 0.2);
                        b.Poly(true, 0, -r * 0.2, -r * 0.45, r * 0.6, r * 0.45, r * 0.6);
                        b.Line(-r * 0.15, r * 0.6, -r * 0.2, r * 0.75);
                        b.Line(r * 0.15, r * 0.6, r * 0.2, r * 0.75);
                        b.Line(0, r * 0.6, 0, r * 0.8);
                        b.Symbol = "uplight";
                        return true;
                    }

                case "lighting.step_light":
                case "lighting.wall_light":
                case "lighting.facade_light":
                    {
                        // fixture on a wall (wall at +Y), light thrown towards -Y
                        double fw = Math.Max(W, 0.3), fd = Math.Max(D, 0.12);
                        b.Wall(Math.Max(fw * 1.8, S), fd);
                        b.Rect(0, fd / 2 - (fd / 2), fw, fd);
                        b.Fill(Rect(0, 0, fw * 0.5, fd * 0.4));
                        int rays = typeId == "lighting.facade_light" ? 5 : 3;
                        for (int i = 0; i < rays; i++)
                        {
                            double t = rays == 1 ? 0 : -1 + (2.0 * i / (rays - 1));
                            double spread = typeId == "lighting.step_light" ? 0.6 : 1.0;
                            if (typeId == "lighting.facade_light")
                            {
                                b.Line(t * fw * 0.4, fd / 2, t * fw * 0.9, fd / 2 + (S * 0.45));
                            }
                            else
                            {
                                b.Line(t * fw * 0.3, -fd / 2, t * fw * spread, -fd / 2 - (S * 0.4));
                            }
                        }

                        b.Symbol = typeId.Substring("lighting.".Length);
                        return true;
                    }

                case "lighting.pergola_light":
                    {
                        b.Rounded(0, 0, Math.Max(W, 0.4), Math.Max(D, 0.08));
                        double hw = Math.Max(W, 0.4) / 2;
                        for (int i = -2; i <= 2; i++)
                        {
                            b.Line(i * hw * 0.4, -Math.Max(D, 0.08) / 2, i * hw * 0.6, -Math.Max(D, 0.08) / 2 - (hw * 0.4));
                            b.Line(i * hw * 0.4, Math.Max(D, 0.08) / 2, i * hw * 0.6, Math.Max(D, 0.08) / 2 + (hw * 0.4));
                        }

                        b.Symbol = "pergola_light";
                        return true;
                    }

                case "lighting.control_cabinet":
                    b.Cabinet(W, D, null);
                    b.Bolt(0, 0, Math.Min(W, D) * 0.7);
                    b.Symbol = "lighting_cabinet";
                    return true;

                case "decorative.light_installation":
                    {
                        b.Line(-W / 2, 0, W / 2, 0);
                        int n = Math.Max(3, (int)Math.Round(W / 0.4));
                        for (int i = 0; i < n; i++)
                        {
                            double x = -W / 2 + (W * (i + 0.5) / n);
                            b.Circle(x, 0, Math.Max(D, 0.1) * 0.35);
                            b.Rays(x, 0, Math.Max(D, 0.1) * 0.5, Math.Max(D, 0.1) * 0.85, 4);
                        }

                        b.Symbol = "light_art";
                        return true;
                    }

                // ---------------- technical cabinets and units ----------------
                case "utility.equipment_enclosure":
                    b.Cabinet(W, D, null);
                    b.Grille(W * 0.6, D * 0.5, 5);
                    b.Symbol = "enclosure";
                    return true;

                case "sustainability.inverter":
                    {
                        b.Wall(Math.Max(W * 1.4, S), D);
                        b.Rect(0, 0, W, D);
                        double a = Math.Min(W * 0.35, 0.2);
                        b.Sine(-a, 0, a, D * 0.3);
                        b.Symbol = "inverter";
                        return true;
                    }

                case "sustainability.battery_cabinet":
                    {
                        b.Cabinet(W, D, null);
                        double bw = W * 0.5, bh = D * 0.4;
                        b.Rect(0, 0, bw, bh);
                        b.Rect(bw / 2 + (bw * 0.05), 0, bw * 0.1, bh * 0.4);
                        b.Line(-bw * 0.3, 0, -bw * 0.1, 0);
                        b.Line(bw * 0.05, 0, bw * 0.25, 0);
                        b.Line(bw * 0.15, -bh * 0.25, bw * 0.15, bh * 0.25);
                        b.Symbol = "battery";
                        return true;
                    }

                case "sustainability.energy_cabinet":
                    b.Cabinet(W, D, null);
                    b.Gauge(0, 0, Math.Min(W, D) * 0.35);
                    b.Symbol = "energy_meter";
                    return true;

                case "info.security_cabinet":
                    b.Cabinet(W, D, null);
                    b.Shield(0, 0, Math.Min(W, D) * 0.4);
                    b.Symbol = "security_cabinet";
                    return true;

                case "sustainability.greywater_unit":
                    b.Rect(0, 0, W, D);
                    b.Circle(-W * 0.2, 0, Math.Min(W, D) * 0.3);
                    b.Circle(W * 0.25, 0, Math.Min(W, D) * 0.2);
                    b.Line(-W * 0.2 + (Math.Min(W, D) * 0.3), 0, W * 0.25 - (Math.Min(W, D) * 0.2), 0);
                    b.Drop(-W * 0.2, 0, Math.Min(W, D) * 0.12);
                    b.Symbol = "greywater";
                    return true;

                case "sustainability.heat_pump":
                case "sustainability.outdoor_unit":
                    {
                        b.Rect(0, 0, W, D);
                        double fr = Math.Min(W * 0.3, D * 0.45);
                        b.Fan(-W * 0.15, 0, fr);
                        b.Grille(W * 0.22, D * 0.6, 4, W * 0.32);
                        b.Symbol = typeId == "sustainability.heat_pump" ? "heat_pump" : "outdoor_unit";
                        return true;
                    }

                case "climate.cooling_unit":
                    {
                        b.Rect(0, 0, W, D);
                        b.Fan(0, 0, Math.Min(W, D) * 0.38);
                        b.Text(0, -D * 0.43, Math.Min(W, D) * 0.1, "*");
                        b.Symbol = "cooling_unit";
                        return true;
                    }

                case "climate.evaporative_cooler":
                    {
                        b.Rect(0, 0, W, D);
                        b.Fan(0, D * 0.12, Math.Min(W, D) * 0.25);
                        for (int i = 0; i < 2; i++)
                        {
                            b.Wave(-W * 0.35, -D * (0.25 + (i * 0.12)), W * 0.35, 3, D * 0.04);
                        }

                        b.Symbol = "evaporative";
                        return true;
                    }

                case "climate.ventilation_unit":
                    {
                        b.Rect(0, 0, W, D);
                        b.Fan(-W * 0.2, 0, Math.Min(W * 0.25, D * 0.4));
                        for (int i = 0; i < 4; i++)
                        {
                            double x = W * 0.1 + (W * 0.1 * i);
                            b.Line(x, -D * 0.35, x + (W * 0.06), D * 0.35);
                        }

                        b.Symbol = "ventilation";
                        return true;
                    }

                case "climate.infrared_heater":
                    {
                        b.Wall(W * 1.1, Math.Max(D, 0.12));
                        b.Rect(0, 0, W, Math.Max(D, 0.12));
                        b.Zigzag(-W * 0.4, 0, W * 0.4, 8, Math.Max(D, 0.12) * 0.5);
                        b.Wave(-W * 0.3, -Math.Max(D, 0.12) - (W * 0.08), W * 0.3, 3, W * 0.03);
                        b.Wave(-W * 0.3, -Math.Max(D, 0.12) - (W * 0.16), W * 0.3, 3, W * 0.03);
                        b.Symbol = "infrared_heater";
                        return true;
                    }

                case "climate.patio_heater":
                    {
                        b.Circle(0, 0, R);
                        b.Circle(0, 0, R * 0.25);
                        b.Rays(0, 0, R * 0.35, R * 0.75, 8);
                        b.Symbol = "patio_heater";
                        return true;
                    }

                case "climate.misting_post":
                    {
                        b.Circle(0, 0, r * 0.18);
                        for (int ring = 1; ring <= 2; ring++)
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                double a = (i + (ring * 0.5)) * Math.PI / 4;
                                double rr = r * (0.35 + (ring * 0.2));
                                b.Circle(Math.Cos(a) * rr, Math.Sin(a) * rr, r * 0.035);
                            }
                        }

                        b.Symbol = "misting";
                        return true;
                    }

                case "climate.awning":
                    {
                        b.Wall(W, D);
                        b.Rect(0, 0, W, D);
                        int n = Math.Max(4, (int)Math.Round(W / 0.4));
                        for (int i = 1; i < n; i++)
                        {
                            double x = -W / 2 + (W * i / n);
                            b.Line(x, -D / 2, x, D / 2);
                        }

                        b.Scallops(-W / 2, -D / 2, W / 2, n);
                        b.Symbol = "awning";
                        return true;
                    }

                case "structure.entrance_canopy":
                    {
                        b.Wall(W, D);
                        b.Rect(0, 0, W, D);
                        int n = Math.Max(3, (int)Math.Round(W / 0.75));
                        for (int i = 1; i < n; i++)
                        {
                            double x = -W / 2 + (W * i / n);
                            b.Line(x, -D / 2, x, D / 2);
                        }

                        b.Line(-W / 2, -D / 2, W / 2, D / 2);
                        b.Symbol = "canopy";
                        return true;
                    }

                // ---------------- art, signs and information ----------------
                case "decorative.sculpture":
                    {
                        b.Rect(0, 0, W, D);
                        b.Poly(true, -W * 0.25, -D * 0.2, -W * 0.05, -D * 0.32, W * 0.22, -D * 0.12, W * 0.18, D * 0.2, -W * 0.02, D * 0.3, -W * 0.3, D * 0.08);
                        b.Circle(W * 0.02, D * 0.02, Math.Min(W, D) * 0.07);
                        b.Symbol = "sculpture";
                        return true;
                    }

                case "decorative.public_art":
                    {
                        b.Poly(true, -W * 0.45, -D * 0.1, -W * 0.15, -D * 0.45, W * 0.3, -D * 0.35, W * 0.45, D * 0.05, W * 0.1, D * 0.45, -W * 0.35, D * 0.3);
                        b.Poly(true, -W * 0.2, -D * 0.05, W * 0.05, -D * 0.2, W * 0.2, D * 0.05, 0, D * 0.2);
                        b.Line(-W * 0.45, -D * 0.1, -W * 0.2, -D * 0.05);
                        b.Line(W * 0.45, D * 0.05, W * 0.2, D * 0.05);
                        b.Line(W * 0.1, D * 0.45, 0, D * 0.2);
                        b.Symbol = "public_art";
                        return true;
                    }

                case "decorative.focal_point":
                    {
                        b.Circle(0, 0, R);
                        b.Star(0, 0, R * 0.7, R * 0.3, 5);
                        b.Symbol = "focal_point";
                        return true;
                    }

                case "decorative.entrance_monument":
                    {
                        b.Rect(0, 0, W, D);
                        b.Rect(0, 0, W * 0.92, D * 0.6);
                        double th = Math.Min(D * 0.45, W * 0.12);
                        b.Text(0, 0, th, "ULAZ");
                        b.Symbol = "entrance_monument";
                        return true;
                    }

                case "info.wayfinding_sign":
                    {
                        double sw = Math.Max(W, 0.5), sd = Math.Max(D, 0.1);
                        b.Rect(0, 0, sw, sd);
                        b.Arrow(-sw * 0.3, -sd - (sw * 0.15), sw * 0.3, -sd - (sw * 0.15), sw * 0.08);
                        b.Symbol = "wayfinding";
                        return true;
                    }

                case "info.building_sign":
                    {
                        double sw = Math.Max(W, 0.5), sd = Math.Max(D, 0.12);
                        b.Rect(0, 0, sw, sd);
                        b.Rect(0, -sd / 2 - (sw * 0.22), sw * 0.6, sw * 0.3);
                        b.Text(0, -sd / 2 - (sw * 0.22), sw * 0.18, "1");
                        b.Symbol = "building_sign";
                        return true;
                    }

                case "info.digital_kiosk":
                    {
                        b.Rect(0, 0, S * 0.8, S * 0.55);
                        b.Rect(0, -S * 0.02, S * 0.6, S * 0.32);
                        b.Fill(Rect(0, -S * 0.02, S * 0.5, S * 0.22));
                        b.Symbol = "kiosk";
                        return true;
                    }

                case "info.access_reader":
                    {
                        b.Rect(0, 0, S * 0.35, S * 0.35);
                        b.Rect(S * 0.28, -S * 0.15, S * 0.28, S * 0.18);
                        b.Arc(0, 0, S * 0.25, -Math.PI * 0.75, -Math.PI * 0.25);
                        b.Arc(0, 0, S * 0.33, -Math.PI * 0.75, -Math.PI * 0.25);
                        b.Symbol = "access_reader";
                        return true;
                    }

                case "info.intercom":
                    {
                        b.Rect(0, 0, S * 0.45, S * 0.35);
                        b.Circle(0, 0, S * 0.12);
                        b.Line(-S * 0.08, 0, S * 0.08, 0);
                        b.Line(0, -S * 0.08, 0, S * 0.08);
                        b.Arc(0, 0, S * 0.3, -Math.PI * 0.8, -Math.PI * 0.2);
                        b.Symbol = "intercom";
                        return true;
                    }

                case "info.emergency_call_point":
                    {
                        b.Rect(0, 0, S * 0.7, S * 0.7);
                        b.Line(-S * 0.3, -S * 0.3, -S * 0.3, S * 0.3);
                        b.Text(S * 0.04, S * 0.0, S * 0.2, "SOS");
                        b.Symbol = "emergency_call";
                        return true;
                    }

                default:
                    return false;
            }
        }

        /// <summary>Types that get their own symbol (used by tests and the legend).</summary>
        public static bool Has(string typeId) => TryBuild(typeId, 1, 1, 1, new List<SymbolPrimitive>(), out _);

        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;

        private static List<Vec2> Rect(double cx, double cy, double w, double h) => new List<Vec2>
        {
            new Vec2(cx - (w / 2), cy - (h / 2)), new Vec2(cx + (w / 2), cy - (h / 2)), new Vec2(cx + (w / 2), cy + (h / 2)), new Vec2(cx - (w / 2), cy + (h / 2)),
        };

        private static List<Vec2> Circle(double cx, double cy, double r, int n)
        {
            var pts = new List<Vec2>(n);
            for (int i = 0; i < n; i++)
            {
                double t = i * 2 * Math.PI / n;
                pts.Add(new Vec2(cx + (Math.Cos(t) * r), cy + (Math.Sin(t) * r)));
            }

            return pts;
        }

        /// <summary>Small drawing helper that appends primitives to the symbol being built.</summary>
        private sealed class Pen
        {
            private readonly List<SymbolPrimitive> items;

            public Pen(List<SymbolPrimitive> items) => this.items = items;

            public string Symbol { get; set; } = string.Empty;

            public void Line(double x1, double y1, double x2, double y2) => items.Add(new SymLine(new Vec2(x1, y1), new Vec2(x2, y2)));

            public void Circle(double x, double y, double r) => items.Add(new SymCircle(new Vec2(x, y), Math.Max(r, 0.005)));

            public void Arc(double x, double y, double r, double a0, double a1) => items.Add(new SymArc(new Vec2(x, y), r, a0, a1));

            public void Rect(double cx, double cy, double w, double h) => items.Add(new SymPolyline(PlanSymbols.Rect(cx, cy, w, h), true));

            public void Fill(List<Vec2> pts) => items.Add(new SymFill(pts));

            public void Text(double x, double y, double h, string t) => items.Add(new SymText(new Vec2(x, y), h, t));

            public void Poly(bool closed, params double[] xy)
            {
                var pts = new List<Vec2>();
                for (int i = 0; i + 1 < xy.Length; i += 2)
                {
                    pts.Add(new Vec2(xy[i], xy[i + 1]));
                }

                items.Add(new SymPolyline(pts, closed));
            }

            /// <summary>Rectangle with rounded ends (a capsule).</summary>
            public void Rounded(double cx, double cy, double w, double h)
            {
                double rr = Math.Min(h / 2, w / 2);
                var pts = new List<Vec2>
                {
                    new Vec2(cx - (w / 2) + rr, cy - (h / 2)), new Vec2(cx + (w / 2) - rr, cy - (h / 2)),
                    new Vec2(cx + (w / 2) - rr, cy + (h / 2)), new Vec2(cx - (w / 2) + rr, cy + (h / 2)),
                };
                items.Add(new SymPolyline(pts, true, new List<double> { 0, 1, 0, 1 }));
            }

            /// <summary>Wall behind a wall-mounted object: a line along +Y with short hatch strokes behind it.</summary>
            public void Wall(double w, double d)
            {
                double y = d / 2;
                Line(-w / 2, y, w / 2, y);
                double step = Math.Max(w / 8, 0.05);
                for (double x = -w / 2; x < w / 2 - 1e-9; x += step)
                {
                    Line(x, y, x + (step * 0.6), y + (step * 0.6));
                }
            }

            /// <summary>Cabinet: body and a door line with a handle on the front.</summary>
            public void Cabinet(double w, double d, string? code)
            {
                Rect(0, 0, w, d);
                Line(-w / 2, -d / 2 + (d * 0.12), w / 2, -d / 2 + (d * 0.12));
                Line(w * 0.3, -d / 2 + (d * 0.12), w * 0.3, -d / 2 - (d * 0.05));
                if (code != null)
                {
                    Text(0, d * 0.1, Math.Min(w, d) * 0.3, code);
                }
            }

            public void Grille(double w, double h, int n, double cx = 0)
            {
                for (int i = 0; i < n; i++)
                {
                    double y = -h / 2 + (h * (i + 0.5) / n);
                    Line(cx - (w / 2), y, cx + (w / 2), y);
                }
            }

            /// <summary>Fan: housing circle, hub and four curved blades.</summary>
            public void Fan(double cx, double cy, double r)
            {
                Circle(cx, cy, r);
                Circle(cx, cy, r * 0.15);
                for (int i = 0; i < 4; i++)
                {
                    double a = i * Math.PI / 2;
                    Arc(cx + (Math.Cos(a) * r * 0.45), cy + (Math.Sin(a) * r * 0.45), r * 0.4, a + (Math.PI * 0.5), a + (Math.PI * 1.2));
                }
            }

            public void Bolt(double cx, double cy, double h)
            {
                Poly(true, cx + (h * 0.1), cy + (h * 0.5), cx - (h * 0.25), cy - (h * 0.05), cx, cy - (h * 0.05), cx - (h * 0.1), cy - (h * 0.5), cx + (h * 0.25), cy + (h * 0.05), cx, cy + (h * 0.05));
            }

            public void Drop(double cx, double cy, double r)
            {
                Arc(cx, cy, r, Math.PI, 2 * Math.PI);
                Line(cx - r, cy, cx, cy + (r * 2));
                Line(cx + r, cy, cx, cy + (r * 2));
            }

            public void Arrow(double x1, double y1, double x2, double y2, double head)
            {
                Line(x1, y1, x2, y2);
                double a = Math.Atan2(y2 - y1, x2 - x1);
                Line(x2, y2, x2 - (Math.Cos(a - 0.5) * head), y2 - (Math.Sin(a - 0.5) * head));
                Line(x2, y2, x2 - (Math.Cos(a + 0.5) * head), y2 - (Math.Sin(a + 0.5) * head));
            }

            public void Rays(double cx, double cy, double r0, double r1, int n)
            {
                for (int i = 0; i < n; i++)
                {
                    double a = i * 2 * Math.PI / n;
                    Line(cx + (Math.Cos(a) * r0), cy + (Math.Sin(a) * r0), cx + (Math.Cos(a) * r1), cy + (Math.Sin(a) * r1));
                }
            }

            public void Wave(double x0, double y, double x1, int n, double amp)
            {
                var pts = new List<double>();
                int steps = n * 8;
                for (int i = 0; i <= steps; i++)
                {
                    double t = (double)i / steps;
                    pts.Add(x0 + ((x1 - x0) * t));
                    pts.Add(y + (Math.Sin(t * n * 2 * Math.PI) * amp));
                }

                Poly(false, pts.ToArray());
            }

            public void Sine(double x0, double y, double x1, double amp) => Wave(x0, y, x1, 1, amp);

            public void Zigzag(double x0, double y, double x1, int n, double amp)
            {
                var pts = new List<double>();
                for (int i = 0; i <= n * 2; i++)
                {
                    pts.Add(x0 + ((x1 - x0) * i / (n * 2)));
                    pts.Add(y + ((i % 2 == 0 ? -1 : 1) * amp / 2));
                }

                Poly(false, pts.ToArray());
            }

            public void Grid(double x0, double y0, double x1, double y1, int nx, int ny)
            {
                Rect((x0 + x1) / 2, (y0 + y1) / 2, x1 - x0, y1 - y0);
                for (int i = 1; i < nx; i++)
                {
                    double x = x0 + ((x1 - x0) * i / nx);
                    Line(x, y0, x, y1);
                }

                for (int j = 1; j < ny; j++)
                {
                    double y = y0 + ((y1 - y0) * j / ny);
                    Line(x0, y, x1, y);
                }
            }

            public void Scallop(double r, int n, double bulge) => Scallop(0, 0, r, n, bulge);

            public void Scallop(double cx, double cy, double r, int n, double bulge)
            {
                List<Vec2> pts = PlanSymbols.Circle(cx, cy, r, n);
                var bulges = new List<double>();
                for (int i = 0; i < n; i++)
                {
                    bulges.Add(bulge);
                }

                items.Add(new SymPolyline(pts, true, bulges));
            }

            /// <summary>Scalloped front edge of an awning valance.</summary>
            public void Scallops(double x0, double y, double x1, int n)
            {
                var pts = new List<Vec2>();
                var bulges = new List<double>();
                for (int i = 0; i <= n; i++)
                {
                    pts.Add(new Vec2(x0 + ((x1 - x0) * i / n), y));
                    bulges.Add(i < n ? -0.5 : 0);
                }

                items.Add(new SymPolyline(pts, false, bulges));
            }

            public void Wheel(double cx, double cy, double r)
            {
                Circle(cx, cy, r);
                Line(cx - r, cy, cx + r, cy);
                Line(cx, cy - r, cx, cy + r);
            }

            public void Flower(double cx, double cy, double r)
            {
                Circle(cx, cy, r * 0.4);
                for (int i = 0; i < 5; i++)
                {
                    double a = i * 2 * Math.PI / 5;
                    Circle(cx + (Math.Cos(a) * r), cy + (Math.Sin(a) * r), r * 0.45);
                }
            }

            public void Paw(double cx, double cy, double r)
            {
                Circle(cx, cy - (r * 0.6), r);
                for (int i = 0; i < 4; i++)
                {
                    double a = Math.PI * (0.2 + (i * 0.2));
                    Circle(cx + (Math.Cos(a) * r * 1.7), cy - (r * 0.6) + (Math.Sin(a) * r * 1.7), r * 0.4);
                }
            }

            public void Gauge(double cx, double cy, double r)
            {
                Arc(cx, cy, r, 0, Math.PI);
                Line(cx - r, cy, cx + r, cy);
                Line(cx, cy, cx + (r * 0.6), cy + (r * 0.6));
                for (int i = 1; i < 4; i++)
                {
                    double a = Math.PI * i / 4;
                    Line(cx + (Math.Cos(a) * r * 0.8), cy + (Math.Sin(a) * r * 0.8), cx + (Math.Cos(a) * r), cy + (Math.Sin(a) * r));
                }
            }

            public void Shield(double cx, double cy, double r)
            {
                items.Add(new SymPolyline(
                    new List<Vec2> { new Vec2(cx - r, cy + (r * 0.7)), new Vec2(cx + r, cy + (r * 0.7)), new Vec2(cx + r, cy), new Vec2(cx, cy - r), new Vec2(cx - r, cy) },
                    true,
                    new List<double> { 0, 0, -0.3, -0.3, 0 }));
                Line(cx - (r * 0.4), cy, cx - (r * 0.1), cy - (r * 0.3));
                Line(cx - (r * 0.1), cy - (r * 0.3), cx + (r * 0.45), cy + (r * 0.35));
            }

            public void Star(double cx, double cy, double r1, double r0, int n)
            {
                var pts = new List<double>();
                for (int i = 0; i < n * 2; i++)
                {
                    double a = (Math.PI / 2) + (i * Math.PI / n);
                    double rr = i % 2 == 0 ? r1 : r0;
                    pts.Add(cx + (Math.Cos(a) * rr));
                    pts.Add(cy + (Math.Sin(a) * rr));
                }

                Poly(true, pts.ToArray());
            }
        }
    }
}
