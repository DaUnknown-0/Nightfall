// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * DIE AIRSHIPS MATERIALKATALOG - 223 Oberflaechen, aus dem Prototyp uebersetzt.
 *
 * WARUM EINE EIGENE DATEI. Dieselbe Begruendung wie bei AreaSurfacesMira.cs: der Katalog
 * traegt mehr Material als Polus und die Skeld zusammen, und in AreaSurfaces.cs gefaltet
 * wuerde er die Helfer und die kleineren Karten unter sich begraben. `partial` setzt die
 * Klasse hier fort, also sind es dieselben Zeichenhelfer, dasselbe `Spec` und dieselbe
 * `Rng`; der statische Konstruktor von AreaSurfaces faltet dieses Dictionary in den einen
 * Katalog, durch den jede Abfrage laeuft.
 *
 * WIE ES ENTSTANDEN IST, und was das fuer Korrekturen bedeutet.
 *
 * Diese Datei ist UEBERSETZT, nicht abgeschrieben: aus den Prototyp-Dateien
 * Assets/NightfallWeb/src/surfaces_airship_*.js, Statement fuer Statement. Das Vokabular der
 * beiden Seiten deckt sich fast vollstaendig - `fill`/`line`/`grain` und `fillRect` machen
 * neun von zehn Aufrufen aus und haben hier ihre direkten Zwillinge. Uebersetzt wurden
 * ausserdem: Schleifen, `globalAlpha` (wird zum `a`-Parameter der Helfer), und die
 * Canvas-Pfade, soweit sie sich auf ein Primitiv abbilden lassen - ein `arc` wird
 * FillEllipse, drei oder vier `lineTo` werden FillQuad, laengere Zuege werden Linienketten.
 *
 * WAS NICHT MITGEKOMMEN IST, steht als `// PORT:`-Kommentar an Ort und Stelle. Das sind
 * Verlaufsfuellungen (Canvas2D kann kein Blending), Text (Canvas2D kann keinen - Mira
 * loest das mit dem Schablonenalphabet in AreaSurfacesMira.cs) und eine Handvoll
 * Sonderformen. Wer eine dieser Stellen nachbaut, loescht den Kommentar mit.
 *
 * DER PROTOTYP BLEIBT DIE QUELLE. Weicht hier etwas ab, ist die JS-Datei im Recht: sie ist
 * die, an der die Optik abgenommen wurde. Namen sind deshalb identisch uebernommen, damit
 * ein Material sich gegen sein Original vergleichen laesst.
 *
 * GRAIN IST DETERMINISTISCH. Der Prototyp streut mit rnd.Next(), hier haengt die Folge
 * an einem Seed, der aus dem Materialnamen kommt. Das Bild ist damit nicht dasselbe wie im
 * Browser, aber es ist in Spiel und Offline-Renderer dasselbe - und genau das ist, was das
 * Pruefen ausserhalb des Spiels ueberhaupt erst wahr macht.
 */

using System;
using System.Collections.Generic;

namespace Nightfall.Core;

public static partial class AreaSurfaces
{
    private static readonly Dictionary<string, Spec> AirshipCatalogue = new()
    {

        // ---------------------------------------------------------------
        // aus surfaces_airship_armory.js
        // ---------------------------------------------------------------
        ["aAirArmoryFloorWood"] = new Spec { Unit = 0.76f, Detail = 1, Draw = g => {
        Fill(g, "#5e3c38");
        // Dielen: Bahn hoehe 1/4 der Einheit, jede zweite Bahn leicht abgesetzt
        float b = g.H / 4f;
        for (float i = 0f; i < 4f; i += 1f)
        {
            if (i % 2f == 1f)
            {
                Rect(g, 0f, i * b, g.W, b, "#66413c", 1f);
            }
            // Fuge unten + versetzte Stossfugen
            Line(g, 0f, (i + 1f) * b - 1f, g.W, (i + 1f) * b - 1f, "#4a2e2c", 2f, 0.8f);
            float off = ((i * 97f) % 100f) / 100f * g.W;
            Line(g, off, i * b, off, (i + 1f) * b, "#4f322f", 2f, 1f);
            Line(g, (off + g.W * 0.55f) % g.W, i * b, (off + g.W * 0.55f) % g.W, (i + 1f) * b, "#4f322f", 2f, 1f);
        }
        // helle Maserung
        for (float i = 0f; i < 5f; i += 1f)
        {
            float y = 3f + ((i * 37f) % MathF.Max(1f, g.H - 6f));
            Line(g, 0f, y, g.W, y, "#71493f", 1f, 0.35f);
        }
        Grain(g, new[] { "#6a443e", "#523431" }, 450, 0.10f, 3381);
        } },
        ["aAirArmoryCarpet"] = new Spec { Unit = 1.15f, Detail = 1, Draw = g => {
        Fill(g, "#622443");
        // Flor: feine diagonale Strichlage, zwei Richtungen ueberkreuzt
        for (float x = -g.H; x < g.W; x += 5f)
        {
        Line(g, x, 0f, x + g.H, g.H, "#6d2a4c", 1f, 1f);
        }
        for (float x = 0f; x < g.W + g.H; x += 5f)
        {
        Line(g, x, 0f, x - g.H, g.H, "#571e3a", 1f, 1f);
        }
        Grain(g, new[] { "#6d2a4c", "#521d37", "#753052" }, 550, 0.12f, 1665);
        } },
        ["aAirArmoryCarpetBorder"] = new Spec { Unit = 0.5f, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   else g.lineTo(x, y)
        Fill(g, "#622443");
        Grain(g, new[] { "#622443" }, 400, 0.06f, 3549);
        } },
        ["aAirArmoryWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#421c1e");
        for (float x = 0f; x < g.W; x += 36f)
        {
        Line(g, x, 0f, x, g.H, "#331416", 2f, 1f);
        }
        // Panelfugen
        Line(g, 0f, 3f, g.W, 3f, "#5c2c26", 2f, 1f);
        // helle Perle unter der Kappe
        Line(g, 0f, g.H * 0.30f, g.W, g.H * 0.30f, "#3a181a", 1f, 1f);
        // Blende
        // dunkler Sockel
        Rect(g, 0f, g.H * 0.80f, g.W, g.H * 0.20f, "#331416", 1f);
        Line(g, 0f, g.H * 0.80f, g.W, g.H * 0.80f, "#2a1012", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#1c0a0c", 3f, 1f);
        // Basislinie
        Grain(g, new[] { "#4b2123", "#381518" }, 400, 0.10f, 6825);
        } },
        ["aAirArmoryHull"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#230d12");
        for (float x = 0f; x < g.W; x += 42f)
        {
        Line(g, x, 0f, x, g.H, "#17070b", 2f, 1f);
        }
        for (float y = 0f; y < g.H; y += 30f)
        {
        Line(g, 0f, y, g.W, y, "#17070b", 1f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#33161a", 1f, 1f);
        for (float x = 10f; x < g.W; x += 42f)
        {
            // Nietenreihen
            g.FillEllipse(x, 7f, 1.3f, 1.3f, C("#150609"), 1f);
            g.FillEllipse(x, g.H - 7f, 1.3f, 1.3f, C("#150609"), 1f);
        }
        Grain(g, new[] { "#2b1116", "#1a070b" }, 350, 0.10f, 3709);
        } },
        ["aAirArmoryCaseLight"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#a8bcba");
        for (float y = 0f; y < g.H; y += 16f)
        {
        Line(g, 0f, y, g.W, y, "#8ba19f", 1f, 1f);
        }
        // Blechfugen
        Line(g, 0f, 2f, g.W, 2f, "#c2d2d0", 1f, 1f);
        // helle Kante oben
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#7e9492", 2f, 1f);
        // dunkle Basis
        Grain(g, new[] { "#b3c6c4", "#96aba9" }, 300, 0.10f, 1099);
        } },
        ["aAirArmoryCaseDark"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#12151a");
        for (float x = 0f; x < g.W; x += 22f)
        {
        Line(g, x, 0f, x, g.H, "#0b0d11", 1f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#232833", 1f, 1f);
        Grain(g, new[] { "#181c23", "#0d0f13" }, 250, 0.10f, 772);
        } },
        ["aAirArmoryCaseGreen"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#2f3e3e");
        for (float y = 0f; y < g.H; y += 14f)
        {
        Line(g, 0f, y, g.W, y, "#263433", 1f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#43565a", 1f, 1f);
        Grain(g, new[] { "#374847", "#283636" }, 250, 0.10f, 601);
        } },
        ["aAirArmoryCaseGlass"] = new Spec { Unit = 0.6f, Draw = g => {
        Fill(g, "#8fa8a6");
        // zwei helle Glasreflex-Streifen
        Line(g, g.W * 0.15f, g.H, g.W * 0.45f, 0f, "#c9dcd9", 3f, 0.35f);
        Line(g, g.W * 0.55f, g.H, g.W * 0.80f, 0f, "#c9dcd9", 2f, 0.35f);
        Grain(g, new[] { "#9db4b2", "#84a09e" }, 150, 0.08f, 3094);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_airship_brig.js
        // ---------------------------------------------------------------
        ["aAirBrigFloor"] = new Spec { Unit = 0.62f, Draw = g => {
        Fill(g, "#5b6d7d");
        float n = 4f, s = g.W / n;
        for (float j = 0f; j < n; j += 1f)
        {
            for (float i = 0f; i < n; i += 1f)
            {
                float cx = i * s + s / 2f, cy = j * s + s / 2f;
                g.FillQuad(cx, cy - s * 0.42f, cx + s * 0.42f, cy, cx, cy + s * 0.42f, cx - s * 0.42f, cy, C(((i + j) % 2 == 0) ? "#67798a" : "#5e7080"), 1f);
            }
        }
        for (float i = 0f; i <= n; i += 1f)
        {
            Line(g, i * s, 0f, i * s, g.H, "#4c5d6b", 1.5f, 1f);
            Line(g, 0f, i * s, g.W, i * s, "#4c5d6b", 1.5f, 1f);
        }
        Grain(g, new[] { "#6d7f8f", "#52626f" }, 400, 0.10f, 92);
        } },
        ["aAirBrigThreshold"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#6c8082");
        for (float y = 6f; y < g.H; y += 14f)
        {
        Line(g, 0f, y, g.W, y, "#5d7174", 1.5f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#7b9294", 1f, 1f);
        Grain(g, new[] { "#788f91", "#5f7376" }, 350, 0.10f, 9756);
        } },
        ["aAirBrigCellFloor"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#5b6d7d");
        for (float x = 0f; x < g.W; x += 30f)
        {
        Line(g, x, 0f, x, g.H, "#4e5e6c", 1.5f, 1f);
        }
        for (float y = 0f; y < g.H; y += 30f)
        {
        Line(g, 0f, y, g.W, y, "#4e5e6c", 1.5f, 1f);
        }
        Grain(g, new[] { "#63747f", "#515f6b" }, 300, 0.10f, 2166);
        } },
        ["aAirBrigPartition"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#a28a88");
        for (float x = 0f; x < g.W; x += g.W / 3f)
        {
        Line(g, x, 0f, x, g.H, "#8d7674", 2f, 1f);
        }
        // Niete oben und unten je Paneel
        for (float x = 8f; x < g.W; x += g.W / 3f)
        {
            g.FillEllipse(x + g.W / 6f, 7f, 1.3f, 1.3f, C("#8d7674"), 1f);
            g.FillEllipse(x + g.W / 6f, g.H * 0.72f, 1.3f, 1.3f, C("#8d7674"), 1f);
        }
        // roter Basisstreifen
        Rect(g, 0f, g.H * 0.80f, g.W, g.H * 0.20f, "#885052", 1f);
        Line(g, 0f, g.H * 0.80f, g.W, g.H * 0.80f, "#6f4141", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#54302f", 3f, 1f);
        Line(g, 0f, 2f, g.W, 2f, "#b59c9a", 1f, 1f);
        Grain(g, new[] { "#ab9290", "#977f7d" }, 350, 0.10f, 5507);
        } },
        ["aAirBrigHullWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#471f22");
        for (float y = 10f; y < g.H; y += 26f)
        {
        Line(g, 0f, y, g.W, y, "#3a181b", 2f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#5b2a2e", 1f, 1f);
        for (float x = 12f; x < g.W; x += 30f)
        {
            g.FillEllipse(x, 7f, 1.3f, 1.3f, C("#3a181b"), 1f);
        }
        Grain(g, new[] { "#522428", "#3b1a1d" }, 350, 0.10f, 4665);
        } },
        ["aAirBrigCap"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#451f22");
        for (float x = 0f; x < g.W; x += 40f)
        {
        Line(g, x, 0f, x, g.H, "#381719", 1.5f, 1f);
        }
        Grain(g, new[] { "#4e2427", "#3a191b" }, 300, 0.10f, 4641);
        } },
        ["aAirBrigSteel"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#37404a");
        for (float x = 0f; x < g.W; x += 36f)
        {
        Line(g, x, 0f, x, g.H, "#2c343d", 2f, 1f);
        }
        Line(g, 0f, 3f, g.W, 3f, "#46525d", 1.5f, 1f);
        Rect(g, 0f, g.H * 0.86f, g.W, g.H * 0.14f, "#2c343d", 1f);
        Grain(g, new[] { "#3f4a55", "#2e3740" }, 350, 0.10f, 299);
        } },
        ["aAirBrigCellFront"] = new Spec { Unit = 6.04f, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   g.font = 'bold 9px monospace'
        //   g.textAlign = 'center'
        //   g.fillText('0-1', 36, 118)
        //   g.fillText('0-2', 74, 118)
        //   g.fillText('0-3', 112, 118)
        //   g.textAlign = 'left'
        Fill(g, "#9b404b");
        Grain(g, new[] { "#9b404b" }, 400, 0.06f, 4062);
        } },
        ["aAirBrigCellRed"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#b54a57");
        Line(g, 0f, 2f, g.W, 2f, "#c65a68", 1f, 1f);
        Grain(g, new[] { "#c05260", "#a3424f" }, 350, 0.10f, 3500);
        } },
        ["aAirBrigCellSide"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#7a2e3a");
        for (float y = 8f; y < g.H; y += 24f)
        {
        Line(g, 0f, y, g.W, y, "#672530", 1.5f, 1f);
        }
        Grain(g, new[] { "#83323e", "#6d2733" }, 300, 0.10f, 8841);
        } },
        ["aAirBrigLedge"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#c05562");
        Line(g, 0f, 2f, g.W, 2f, "#d46674", 1.5f, 1f);
        Rect(g, 0f, g.H - 5f, g.W, 5f, "#832948", 1f);
        Grain(g, new[] { "#c95d6a", "#b04a58" }, 350, 0.10f, 3841);
        } },
        ["aAirBrigPillar"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#b54a57");
        Rect(g, g.W * 0.18f, 0f, g.W * 0.16f, g.H, "#c97b86", 1f);
        Rect(g, g.W * 0.78f, 0f, g.W * 0.22f, g.H, "#3c0f16", 0.4f);
        Grain(g, new[] { "#bd5160", "#a84553" }, 200, 0.10f, 6988);
        } },
        ["aAirBrigCellDoor"] = new Spec { Unit = 0.64f, Draw = g => {
        Fill(g, "#141c22");
        Rect(g, 14f, 14f, g.W - 28f, g.H - 28f, "#b54a57", 1f);
        Rect(g, 40f, 24f, 48f, 40f, "#141c22", 1f);
        Rect(g, 43f, 27f, 42f, 34f, "#99909b", 1f);
        Rect(g, 43f, 27f, 42f, 12f, "#b3aab6", 1f);
        Rect(g, 14f, 74f, g.W - 28f, 5f, "#141c22", 1f);
        Rect(g, 22f, 86f, g.W - 44f, 26f, "#a34450", 1f);
        Rect(g, 22f, 96f, g.W - 44f, 5f, "#7d3540", 1f);
        Grain(g, new[] { "#1a2530", "#0e141a" }, 200, 0.12f, 2682);
        } },
        ["aAirBrigGrille"] = new Spec { Unit = 0.38f, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   g.ellipse(w / 2, h / 2, w / 2 - 5, h / 2 - 5, 0, 0, 7)
        Fill(g, "#b54a57");
        Grain(g, new[] { "#b54a57" }, 400, 0.06f, 5933);
        } },
        ["aAirBrigCellWindow"] = new Spec { Unit = 5.15f, Draw = g => {
        Fill(g, "#c98a92");
        Rect(g, 6f, 8f, g.W - 12f, g.H - 16f, "#6f8fa8", 1f);
        Rect(g, 6f, 10f, g.W - 12f, 5f, "#8fabc2", 1f);
        Grain(g, new[] { "#7a99b2", "#62819a" }, 250, 0.10f, 5620);
        } },
        ["aAirBrigLeaf"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#4e6164");
        Line(g, 2f, 2f, 2f+g.W - 4f, 2f, "#3d4e51", 4f, 1f);
        Line(g, 2f, 2f+g.H - 4f, 2f+g.W - 4f, 2f+g.H - 4f, "#3d4e51", 4f, 1f);
        Line(g, 2f, 2f, 2f, 2f+g.H - 4f, "#3d4e51", 4f, 1f);
        Line(g, 2f+g.W - 4f, 2f, 2f+g.W - 4f, 2f+g.H - 4f, "#3d4e51", 4f, 1f);
        Rect(g, 30f, 12f, 68f, 26f, "#3d4e51", 1f);
        Rect(g, 34f, 15f, 28f, 20f, "#9aa6a8", 1f);
        Rect(g, 66f, 15f, 28f, 20f, "#9aa6a8", 1f);
        Line(g, g.W / 2f, 44f, g.W / 2f, g.H - 8f, "#3d4e51", 3f, 1f);
        Grain(g, new[] { "#57696c", "#45565a" }, 300, 0.10f, 4285);
        } },
        ["aAirBrigLeafInt"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#667b8a");
        Rect(g, 0f, 0f, g.W, 6f, "#7b909f", 1f);
        Rect(g, 0f, g.H - 8f, g.W, 8f, "#57697a", 1f);
        Line(g, 0f, g.H * 0.38f, g.W, g.H * 0.38f, "#57697a", 2f, 0.6f);
        Line(g, 0f, g.H * 0.62f, g.W, g.H * 0.62f, "#57697a", 2f, 0.6f);
        Grain(g, new[] { "#6f8494", "#5d7080" }, 300, 0.10f, 8610);
        } },
        ["aAirBrigPocket"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#53646a");
        for (float y = 8f; y < g.H; y += 20f)
        {
        Line(g, 0f, y, g.W, y, "#46565c", 1.5f, 1f);
        }
        for (float x = 10f; x < g.W; x += 24f)
        {
            g.FillEllipse(x, 5f, 1.2f, 1.2f, C("#46565c"), 1f);
            g.FillEllipse(x, g.H - 5f, 1.2f, 1.2f, C("#46565c"), 1f);
        }
        Grain(g, new[] { "#5c6e74", "#4a5a60" }, 300, 0.10f, 8878);
        } },
        ["aAirBrigCeiling"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#4a4f58");
        for (float x = 0f; x < g.W; x += g.W / 2f)
        {
        Line(g, x, 0f, x, g.H, "#434850", 2f, 1f);
        }
        for (float y = 0f; y < g.H; y += g.H / 2f)
        {
        Line(g, 0f, y, g.W, y, "#434850", 2f, 1f);
        }
        Grain(g, new[] { "#50555e", "#434850" }, 250, 0.10f, 7178);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_airship_cargobay.js
        // ---------------------------------------------------------------
        ["aAirCargoDeck"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#808a8c");
        // aus #919d9f
        float cols = 6f, rows = 6f, cw = g.W / cols, ch = g.H / rows;
        for (float r = 0f; r < rows; r += 1f)
        {
            bool up = r % 2f == 0f;
            // Kipprichtung je Zeile wechseln
            for (float c = 0f; c < cols; c += 1f)
            {
                float x = c * cw, y = r * ch;
                Line(g, x + cw * 0.18f, y + ch * (up ? 0.72f : 0.28f), x + cw * 0.82f, y + ch * (up ? 0.28f : 0.72f), "#3c4648", 2f, 0.75f);
                Line(g, x + cw * 0.34f, y + ch * (up ? 0.80f : 0.20f), x + cw * 0.94f, y + ch * (up ? 0.38f : 0.62f), "#485254", 2f, 0.6f);
            }
        }
        for (float i = 1f; i < 3f; i += 1f)
        {
            // Plattenfugen
            Line(g, g.W * i / 3f, 0f, g.W * i / 3f, g.H, "#404a4c", 2f, 0.8f);
            Line(g, 0f, g.H * i / 3f, g.W, g.H * i / 3f, "#404a4c", 2f, 0.8f);
        }
        Grain(g, new[] { "#8a9598", "#758083", "#8f9a9d" }, 800, 0.10f, 6351);
        } },
        ["aAirCargoWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#9f5f65");
        // aus #b56c73
        for (float i = 0f; i < 5f; i += 1f)
        {
            float x = g.W * i / 5f;
            Line(g, x, 0f, x, g.H, "#8a5058", 3f, 1f);
            // Welle, dunkel
            Line(g, x + 3f, 0f, x + 3f, g.H, "#ab6870", 2f, 1f);
            // Welle, Lichtkante
        }
        Line(g, 0f, g.H - 1f, g.W, g.H - 1f, "#8a5058", 2f, 1f);
        // Fusslinie
        Grain(g, new[] { "#a8656e", "#93565d" }, 500, 0.10f, 8322);
        } },
        ["aAirCargoPanel"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   { for (const f of [1 / 3, 2 / 3])
        Fill(g, "#babbae");
        Grain(g, new[] { "#babbae" }, 400, 0.06f, 8368);
        } },
        ["aAirCargoHull"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#482021");
        // aus #522426
        for (float i = 1f; i < 3f; i += 1f)
        {
        Line(g, g.W * i / 3f, 0f, g.W * i / 3f, g.H, "#38191a", 2f, 1f);
        }
        Line(g, 0f, g.H * 0.3f, g.W, g.H * 0.3f, "#3e1b1d", 2f, 1f);
        Grain(g, new[] { "#4f2426", "#3e1b1d" }, 500, 0.12f, 1407);
        } },
        ["aAirCargoCap"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#431c1e");
        Grain(g, new[] { "#4a2022", "#3a181a" }, 300, 0.12f, 7700);
        } },
        ["aAirCargoCeil"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#4a4f58");
        Line(g, g.W - 1f, 0f, g.W - 1f, g.H, "#434850", 3f, 1f);
        Line(g, 0f, g.H - 1f, g.W, g.H - 1f, "#434850", 3f, 1f);
        Grain(g, new[] { "#50555e", "#434850" }, 400, 0.10f, 8873);
        } },
        ["aAirCargoPlate"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#91a5ac");
        // aus #a5bcc4
        for (float r = 0f; r < 4f; r += 1f)
        {
            for (float c = 0f; c < 4f; c += 1f)
            {
                float x = c * g.W / 4f, y = r * g.H / 4f;
                Line(g, x + g.W * 0.14f, y + g.H * 0.7f, x + g.W * 0.7f, y + g.H * 0.14f, "#6c7e86", 2f, 0.8f);
            }
        }
        Grain(g, new[] { "#9bb0b7", "#84979e" }, 400, 0.10f, 4660);
        } },
        ["aAirCargoStep"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#889b9d");
        // aus #9ab0b3
        for (float i = 1f; i < 3f; i += 1f)
        {
        Line(g, 0f, g.H * i / 3f, g.W, g.H * i / 3f, "#718284", 2f, 1f);
        }
        Grain(g, new[] { "#93a5a8", "#7a8b8d" }, 400, 0.10f, 499);
        } },
        ["aAirCargoCage"] = new Spec { Unit = 1.3f, Detail = 1, Draw = g => {
        Fill(g, "#74533a");
        // Kistenfeld, aus #845e42
        // dunkle Innenraeume hinter dem Gurt
        Rect(g, g.W * 0.08f, g.H * 0.30f, g.W * 0.36f, g.H * 0.28f, "#432a12", 0.5f);
        Rect(g, g.W * 0.56f, g.H * 0.58f, g.W * 0.30f, g.H * 0.26f, "#432a12", 0.5f);
        for (float i = 0f; i <= 4f; i += 1f)
        {
            // das Spanngurt-Gitter
            Line(g, g.W * i / 4f, 0f, g.W * i / 4f, g.H, "#b79e3c", 3f, 1f);
            Line(g, 0f, g.H * i / 4f, g.W, g.H * i / 4f, "#b79e3c", 3f, 1f);
        }
        Line(g, 0f, 0f, g.W, g.H, "#b79e3c", 2f, 0.55f);
        // die Diagonalbracing
        Line(g, g.W, 0f, 0f, g.H, "#b79e3c", 2f, 0.55f);
        Grain(g, new[] { "#7d5b40", "#684a32" }, 500, 0.12f, 666);
        } },
        ["aAirCargoBox"] = new Spec { Unit = 0.6f, Draw = g => {
        Fill(g, "#9b7954");
        // aus #b08960
        Line(g, 0f, g.H * 0.5f, g.W, g.H * 0.5f, "#7d5f3f", 2f, 1f);
        // Klappenfuge
        // Klebestreifen, aus #c6a36e
        Rect(g, g.W * 0.42f, 0f, g.W * 0.16f, g.H, "#ae8f61", 1f);
        Line(g, 1f, 1f, 1f+g.W - 2f, 1f, "#7d5f3f", 2f, 1f);
        Line(g, 1f, 1f+g.H - 2f, 1f+g.W - 2f, 1f+g.H - 2f, "#7d5f3f", 2f, 1f);
        Line(g, 1f, 1f, 1f, 1f+g.H - 2f, "#7d5f3f", 2f, 1f);
        Line(g, 1f+g.W - 2f, 1f, 1f+g.W - 2f, 1f+g.H - 2f, "#7d5f3f", 2f, 1f);
        Grain(g, new[] { "#a5825c", "#8a6a48" }, 400, 0.10f, 5479);
        } },
        ["aAirCargoTankbox"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   { g.font = `bold $
        //   Math.round(h * 0.30)
        //   px sans-serif`
        //   g.textAlign = 'center'
        //   g.textBaseline = 'middle'
        //   g.fillText('TANK', w / 2, h * 0.32)
        //   ... und 1 weitere
        Fill(g, "#74533a");
        Grain(g, new[] { "#74533a" }, 400, 0.06f, 5357);
        } },
        ["aAirCargoSafe"] = new Spec { Unit = 0.6f, Draw = g => {
        Fill(g, "#e0c44f");
        // aus #ffdf5a
        Line(g, 2f, 2f, 2f+g.W - 4f, 2f, "#b09a35", 2f, 1f);
        Line(g, 2f, 2f+g.H - 4f, 2f+g.W - 4f, 2f+g.H - 4f, "#b09a35", 2f, 1f);
        Line(g, 2f, 2f, 2f, 2f+g.H - 4f, "#b09a35", 2f, 1f);
        Line(g, 2f+g.W - 4f, 2f, 2f+g.W - 4f, 2f+g.H - 4f, "#b09a35", 2f, 1f);
        // das Drehschloss, aus #c36c29
        g.FillEllipse(g.W * 0.34f, g.H * 0.44f, MathF.Min(g.W, g.H) * 0.15f, MathF.Min(g.W, g.H) * 0.15f, C("#ab5f24"), 1f);
        g.FillEllipse(g.W * 0.34f, g.H * 0.44f, MathF.Min(g.W, g.H) * 0.05f, MathF.Min(g.W, g.H) * 0.05f, C("#8a4a1a"), 1f);
        // die Griffklinke rechts
        Rect(g, g.W * 0.62f, g.H * 0.40f, g.W * 0.20f, g.H * 0.08f, "#b09a35", 1f);
        Rect(g, g.W * 0.76f, g.H * 0.34f, g.W * 0.06f, g.H * 0.20f, "#b09a35", 1f);
        Grain(g, new[] { "#e6cd58", "#cbb23f" }, 300, 0.08f, 500);
        } },
        ["aAirCargoHazard"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#91a5ac");
        // Hellgrund, aus #a5bcc4
        for (float x = -g.H; x < g.W + g.H; x += g.W / 3f)
        {
            Line(g, x, g.H, x + g.H, 0f, "#ae7632", MathF.Max(3f, g.W / 6f), 1f);
            // aus #c68639
        }
        Grain(g, new[] { "#9bb0b7", "#87999f" }, 200, 0.08f, 1029);
        } },
        ["aAirCargoTrim"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#2b353a");
        // aus #313c42
        Line(g, 0f, 1f, g.W, 1f, "#3a464d", 2f, 1f);
        Grain(g, new[] { "#323d44", "#232c31" }, 200, 0.10f, 700);
        } },
        ["aAirCargoRail"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#8d9da2");
        // aus #a0b2b8
        Line(g, 0f, g.H * 0.3f, g.W, g.H * 0.3f, "#a3b3b9", 2f, 1f);
        Grain(g, new[] { "#97a7ad", "#7f8f95" }, 300, 0.10f, 2058);
        } },
        ["aAirCargoVent"] = new Spec { Unit = 0.4f, Draw = g => {
        Fill(g, "#616f79");
        // aus #6e7e8a
        for (float i = 1f; i < 4f; i += 1f)
        {
        Line(g, 0f, g.H * i / 4f, g.W, g.H * i / 4f, "#49545c", 2f, 1f);
        }
        Line(g, 1f, 1f, 1f+g.W - 2f, 1f, "#49545c", 2f, 1f);
        Line(g, 1f, 1f+g.H - 2f, 1f+g.W - 2f, 1f+g.H - 2f, "#49545c", 2f, 1f);
        Line(g, 1f, 1f, 1f, 1f+g.H - 2f, "#49545c", 2f, 1f);
        Line(g, 1f+g.W - 2f, 1f, 1f+g.W - 2f, 1f+g.H - 2f, "#49545c", 2f, 1f);
        Grain(g, new[] { "#6b7a85", "#556270" }, 200, 0.08f, 4502);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_airship_cockpit.js
        // ---------------------------------------------------------------
        ["aAirCockpitDeck"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#a5825e");
        // zwei Dielenreihen: die obere etwas dunkler abgesetzt
        Rect(g, 0f, 0f, g.W, g.H / 2f - 1f, "#8f7150", 1f);
        // Fugen
        Line(g, 0f, g.H / 2f - 1f, g.W, g.H / 2f - 1f, "#644a36", 2f, 1f);
        Line(g, 0f, g.H - 1f, g.W, g.H - 1f, "#644a36", 2f, 1f);
        // versetzte Laengsstoesse je Reihe
        Line(g, g.W * 0.33f, 1f, g.W * 0.33f, g.H / 2f - 2f, "#644a36", 1f, 0.7f);
        Line(g, g.W * 0.71f, g.H / 2f + 1f, g.W * 0.71f, g.H - 2f, "#644a36", 1f, 0.7f);
        // helle Maserungsstreifen je Diele
        for (float x = 6f; x < g.W; x += 17f)
        {
            Line(g, x, 3f, x + 5f, g.H / 2f - 4f, "#b99a72", 1f, 0.25f);
            Line(g, x + 9f, g.H / 2f + 3f, x + 14f, g.H - 5f, "#b99a72", 1f, 0.25f);
        }
        Grain(g, new[] { "#b0906a", "#96764f", "#9c7c58" }, 500, 0.12f, 728);
        } },
        ["aAirCockpitCarpet"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        Fill(g, "#3f6795");
        // breite, weichere Laufspur in der Bahnmitte (die gezeichnete Abnutzung)
        Rect(g, g.W * 0.12f, g.H * 0.18f, g.W * 0.76f, g.H * 0.64f, "#48739f", 0.35f);
        Grain(g, new[] { "#48739f", "#35597f", "#2c4a70" }, 550, 0.14f, 6831);
        } },
        ["aAirCockpitWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#67737f");
        // jede zweite Platte abgesetzt
        Rect(g, g.W / 2f + 1f, 2f, g.W / 2f - 3f, g.H - 4f, "#5d6975", 1f);
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#4a5560", 2f, 0.8f);
        // vertikale Fuge in der Einheitsmitte
        Line(g, 0f, g.H * 0.24f, g.W, g.H * 0.24f, "#4a5560", 1f, 0.8f);
        // horizontale Fugen
        Line(g, 0f, g.H * 0.76f, g.W, g.H * 0.76f, "#4a5560", 1f, 0.8f);
        // Sockelband
        Rect(g, 0f, g.H * 0.88f, g.W, g.H * 0.12f, "#59656f", 1f);
        Line(g, 0f, g.H * 0.88f, g.W, g.H * 0.88f, "#39434c", 2f, 1f);
        // Nieten
        for (float x = 8f; x < g.W; x += 24f)
        {
            g.FillEllipse(x, g.H * 0.24f - 5f, 1.3f, 1.3f, C("#49545e"), 1f);
            g.FillEllipse(x, g.H * 0.76f + 5f, 1.3f, 1.3f, C("#49545e"), 1f);
        }
        Grain(g, new[] { "#6d7985", "#5a6672" }, 400, 0.10f, 863);
        } },
        ["aAirCockpitHull"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#3f1d1e");
        // der rote Zierstreifen des Bugs, leicht unter der Kappe
        Rect(g, 0f, g.H * 0.30f, g.W, g.H * 0.16f, "#8f353d", 1f);
        Line(g, 0f, g.H * 0.30f, g.W, g.H * 0.30f, "#2a1214", 2f, 1f);
        Line(g, 0f, g.H * 0.46f, g.W, g.H * 0.46f, "#5c2729", 1f, 1f);
        // Paneele
        for (float x = 0f; x < g.W; x += 48f)
        {
        Line(g, x, 0f, x, g.H, "#2e1416", 2f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#542628", 1f, 1f);
        Grain(g, new[] { "#472122", "#371a1b" }, 350, 0.10f, 8215);
        } },
        ["aAirCockpitDash"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#1a2024");
        for (float x = 0f; x < g.W; x += 22f)
        {
        Line(g, x, 0f, x, g.H, "#12171a", 1f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#2a3238", 1f, 0.7f);
        Grain(g, new[] { "#212830", "#141a1d" }, 300, 0.10f, 1788);
        } },
        ["aAirCockpitCeil"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#4a4f58");
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#474c54", 2f, 0.5f);
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#474c54", 2f, 0.5f);
        Rect(g, 3f, 3f, g.W / 2f - 6f, g.H / 2f - 6f, "#474c54", 1f);
        Grain(g, new[] { "#4e535c", "#464b53" }, 300, 0.10f, 644);
        } },
        ["aAirCockpitHolo"] = new Spec { Unit = 1.0f, Emissive = 0.55f, Detail = 1, Draw = g => {
        Fill(g, "#2b6470");
        // Kartengitter: Kuestenlinien-Andeutung als helle Polygone
        Line(g, g.W * 0.10f, g.H * 0.70f, g.W * 0.30f, g.H * 0.45f, "#9adcf0", 2f, 1f);
        Line(g, g.W * 0.30f, g.H * 0.45f, g.W * 0.52f, g.H * 0.55f, "#9adcf0", 2f, 1f);
        Line(g, g.W * 0.52f, g.H * 0.55f, g.W * 0.72f, g.H * 0.30f, "#9adcf0", 2f, 1f);
        Line(g, g.W * 0.72f, g.H * 0.30f, g.W * 0.90f, g.H * 0.42f, "#9adcf0", 2f, 1f);
        Line(g, g.W * 0.15f, g.H * 0.82f, g.W * 0.85f, g.H * 0.82f, "#7fc4d4", 1f, 0.6f);
        Line(g, g.W * 0.20f, g.H * 0.22f, g.W * 0.62f, g.H * 0.22f, "#7fc4d4", 1f, 0.6f);
        // Punktraster (Sterne/Kartenpunkte)
        for (float i = 0f; i < 26f; i += 1f)
        {
            Rect(g, (i * 37f) % g.W, (i * 53f) % g.H, 2f, 2f, "#c8ecf6", 1f);
        }
        } },
        // ---------------------------------------------------------------
        // aus surfaces_airship_comms.js
        // ---------------------------------------------------------------
        ["aAirCommsFloor"] = new Spec { Unit = 0.62f, Draw = g => {
        Fill(g, "#53645a");
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#475650", 2f, 0.55f);
        // Plattenfugenkreuz in der Einheitsmitte
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#475650", 2f, 0.55f);
        Line(g, 0f, 1f, g.W, 1f, "#5d7065", 1f, 0.4f);
        // helle Kante oben
        Line(g, 0f, g.H - 1f, g.W, g.H - 1f, "#41504a", 1f, 0.4f);
        Grain(g, new[] { "#5a6d62", "#4a5a52" }, 450, 0.10f, 8998);
        } },
        ["aAirCommsFloorS"] = new Spec { Unit = 0.62f, Draw = g => {
        Fill(g, "#4c5c54");
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#415049", 2f, 0.55f);
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#415049", 2f, 0.55f);
        Line(g, 0f, 1f, g.W, 1f, "#55675d", 1f, 0.4f);
        Grain(g, new[] { "#53655b", "#43534b" }, 450, 0.10f, 6285);
        } },
        ["aAirCommsWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#3e483f");
        for (float x = 0f; x < g.W; x += 46f)
        {
        Line(g, x, 0f, x, g.H, "#333d35", 2f, 1f);
        }
        // Paneelstoesse
        Line(g, 0f, g.H * 0.34f, g.W, g.H * 0.34f, "#37413a", 1f, 0.7f);
        // Lagerfugen
        Line(g, 0f, g.H * 0.67f, g.W, g.H * 0.67f, "#37413a", 1f, 0.7f);
        Line(g, 0f, 3f, g.W, 3f, "#4a564c", 2f, 1f);
        // helle Perle unter der Kappe
        // dunkler Sockel
        Rect(g, 0f, g.H * 0.82f, g.W, g.H * 0.18f, "#313a33", 1f);
        Line(g, 0f, g.H * 0.82f, g.W, g.H * 0.82f, "#28302a", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#1c231e", 3f, 1f);
        Grain(g, new[] { "#45503f", "#37413a" }, 400, 0.10f, 2181);
        } },
        ["aAirCommsHull"] = new Spec { Unit = 1.25f, Detail = 1, Draw = g => {
        Fill(g, "#461f21");
        for (float x = 0f; x < g.W; x += 40f)
        {
        Line(g, x, 0f, x, g.H, "#381719", 2f, 1f);
        }
        // Rippen
        Line(g, 0f, 2f, g.W, 2f, "#54262a", 1f, 1f);
        for (float x = 12f; x < g.W; x += 40f)
        {
            // Nietenreihen
            g.FillEllipse(x, 9f, 1.4f, 1.4f, C("#331518"), 1f);
            g.FillEllipse(x, g.H - 9f, 1.4f, 1.4f, C("#331518"), 1f);
        }
        Grain(g, new[] { "#4d2226", "#3a181b" }, 350, 0.10f, 5235);
        } },
        ["aAirCommsCap"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        Fill(g, "#7e8d7e");
        for (float y = 0f; y < g.H; y += 14f)
        {
        Line(g, 0f, y, g.W, y, "#6f7e70", 1f, 1f);
        }
        // Laengsfugen
        Line(g, 0f, 1f, g.W, 1f, "#8b9a8b", 1f, 1f);
        Grain(g, new[] { "#869585", "#727f72" }, 350, 0.10f, 9688);
        } },
        ["aAirCommsCeil"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#23282b");
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#1a1f22", 2f, 0.8f);
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#1a1f22", 2f, 0.8f);
        Line(g, 2f, 2f, g.W - 2f, 2f, "#2c3236", 1f, 0.5f);
        Grain(g, new[] { "#282e32", "#1d2225" }, 300, 0.10f, 6507);
        } },
        ["aAirCommsDesk"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#76827f");
        for (float y = 4f; y < g.H; y += 7f)
        {
        Line(g, 0f, y, g.W, y, "#697572", 1f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#828e8b", 1f, 1f);
        Grain(g, new[] { "#7d8986", "#697572" }, 300, 0.10f, 1449);
        } },
        ["aAirCommsBank"] = new Spec { Unit = 0.55f, Draw = g => {
        Fill(g, "#3f484a");
        for (float y = g.H / 4f; y < g.H; y += g.H / 4f)
        {
        Line(g, 0f, y, g.W, y, "#333b3d", 2f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#495356", 1f, 0.4f);
        Grain(g, new[] { "#454f52", "#384043" }, 300, 0.10f, 7457);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_airship_electrical.js
        // ---------------------------------------------------------------
        ["aAirElectricalFloor"] = new Spec { Unit = 0.56f, Draw = g => {
        Fill(g, "#565f61");
        // 2x2 Platten je Einheit, jede zweite abgesetzt
        Rect(g, g.W / 2f + 1f, 1f, g.W / 2f - 2f, g.H / 2f - 2f, "#4e5759", 1f);
        Rect(g, 1f, g.H / 2f + 1f, g.W / 2f - 2f, g.H / 2f - 2f, "#4e5759", 1f);
        // Fugenkreuz in der Einheitsmitte + Randfugen
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#2b3134", 2f, 0.9f);
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#2b3134", 2f, 0.9f);
        Line(g, 0f, 0.5f, g.W, 0.5f, "#2b3134", 1f, 1f);
        Line(g, 0.5f, 0f, 0.5f, g.H, "#2b3134", 1f, 1f);
        Grain(g, new[] { "#5d6668", "#47504f" }, 380, 0.10f, 9690);
        } },
        ["aAirElectricalWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#451e21");
        for (float x = 0f; x < g.W; x += 36f)
        {
        Line(g, x, 0f, x, g.H, "#371518", 2f, 1f);
        }
        Line(g, 0f, 3f, g.W, 3f, "#57282c", 2f, 1f);
        // Perle unter der Kappe
        // Sockelstreifen
        Rect(g, 0f, g.H * 0.80f, g.W, g.H * 0.20f, "#3a171b", 1f);
        Line(g, 0f, g.H * 0.80f, g.W, g.H * 0.80f, "#2c1013", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#1e0b0d", 3f, 1f);
        // dunkle Basislinie
        Grain(g, new[] { "#4d2226", "#3b171b" }, 400, 0.10f, 5112);
        } },
        ["aAirElectricalHull"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#2e1417");
        for (float x = 0f; x < g.W; x += 42f)
        {
        Line(g, x, 0f, x, g.H, "#200d10", 2f, 1f);
        }
        for (float y = 0f; y < g.H; y += 30f)
        {
        Line(g, 0f, y, g.W, y, "#200d10", 1f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#3d1c20", 1f, 1f);
        for (float x = 10f; x < g.W; x += 42f)
        {
            g.FillEllipse(x, 8f, 1.4f, 1.4f, C("#1a0a0c"), 1f);
            g.FillEllipse(x, g.H - 8f, 1.4f, 1.4f, C("#1a0a0c"), 1f);
        }
        Grain(g, new[] { "#361a1d", "#241013" }, 350, 0.10f, 5066);
        } },
        ["aAirElectricalMachine"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        Fill(g, "#4b5350");
        // vertikale Rippen mit heller Kante
        for (float x = 4f; x < g.W; x += 18f)
        {
            Line(g, x, 0f, x, g.H, "#3f4744", 3f, 0.8f);
            Line(g, x + 3f, 0f, x + 3f, g.H, "#575f5b", 1f, 0.7f);
        }
        // obere und untere Schraubleiste
        Rect(g, 0f, 0f, g.W, 5f, "#3a413e", 1f);
        Rect(g, 0f, g.H - 5f, g.W, 5f, "#3a413e", 1f);
        for (float x = 8f; x < g.W; x += 18f)
        {
            Rect(g, x, 1.5f, 2f, 2f, "#5a625e", 1f);
            Rect(g, x, g.H - 3.5f, 2f, 2f, "#5a625e", 1f);
        }
        Grain(g, new[] { "#525a56", "#414845" }, 320, 0.10f, 7795);
        } },
        ["aAirElectricalHazard"] = new Spec { Unit = 0.35f, Draw = g => {
        Fill(g, "#a97427");
        float s = 10f;
        for (float d = -g.H; d < g.W + g.H; d += s * 2f)
        {
            g.FillQuad(d, 0f, d + s, 0f, d + s - g.H, g.H, d - g.H, g.H, C("#14151c"), 1f);
        }
        // Abnutzung: helle Kratzer ueber die Streifen
        Grain(g, new[] { "#c08934", "#3a3d46" }, 160, 0.5f, 5836);
        } },
        ["aAirElectricalDoorSealed"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#1c242c");
        Line(g, 3f, 3f, 3f+g.W - 6f, 3f, "#151b22", 3f, 1f);
        Line(g, 3f, 3f+g.H - 6f, 3f+g.W - 6f, 3f+g.H - 6f, "#151b22", 3f, 1f);
        Line(g, 3f, 3f, 3f, 3f+g.H - 6f, "#151b22", 3f, 1f);
        Line(g, 3f+g.W - 6f, 3f, 3f+g.W - 6f, 3f+g.H - 6f, "#151b22", 3f, 1f);
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#151b22", 2f, 1f);
        // geteilte Tafel
        for (float y = 8f; y < g.H - 6f; y += 14f)
        {
            Rect(g, 5f, y, 2f, 2f, "#242e37", 1f);
            Rect(g, g.W - 7f, y, 2f, 2f, "#242e37", 1f);
        }
        Grain(g, new[] { "#222b33", "#161d24" }, 200, 0.10f, 5782);
        } },
        ["aAirElectricalLadderTread"] = new Spec { Unit = 0.37f, Draw = g => {
        Fill(g, "#2a3238");
        // helle Trittkante (Sprossen-Zitat)
        Rect(g, 0f, g.H - 5f, g.W, 4f, "#58748c", 1f);
        Line(g, 0f, g.H - 5f, g.W, g.H - 5f, "#6d8ba3", 1f, 1f);
        for (float x = 0f; x < g.W; x += 12f)
        {
        Line(g, x, 0f, x, g.H - 5f, "#232a30", 1f, 1f);
        }
        // Blech-Riffel
        Grain(g, new[] { "#313a40", "#232a30" }, 140, 0.12f, 2488);
        } },
        ["aAirElectricalLadder"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#58748c");
        for (float y = 0f; y < g.H; y += 8f)
        {
        Line(g, 0f, y, g.W, y, "#4a6378", 1f, 1f);
        }
        Line(g, 1f, 0f, 1f, g.H, "#6d8ba3", 1f, 1f);
        // helle Holmkante
        Line(g, g.W - 1f, 0f, g.W - 1f, g.H, "#3f5568", 1f, 1f);
        Grain(g, new[] { "#63809a", "#4a6378" }, 160, 0.12f, 4094);
        } },
        ["aAirElectricalCeil"] = new Spec { Unit = 1.1f, Detail = 1, Draw = g => {
        Fill(g, "#4a4f58");
        for (float x = 0f; x < g.W; x += g.W / 2f)
        {
        Line(g, x, 0f, x, g.H, "#434850", 2f, 1f);
        }
        for (float y = 0f; y < g.H; y += g.H / 2f)
        {
        Line(g, 0f, y, g.W, y, "#434850", 2f, 1f);
        }
        Line(g, 1f, 1f, g.W - 1f, 1f, "#545962", 1f, 1f);
        Grain(g, new[] { "#50555e", "#434850" }, 260, 0.10f, 8740);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_airship_engine.js
        // ---------------------------------------------------------------
        ["aAirEngineDeck"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#939ea4");
        // Querfugen alle 0.25 Einheiten, jede zweite etwas staerker
        for (float x = 0f; x <= g.W; x += g.W / 4f)
        {
            Line(g, x, 0f, x, g.H, "#767f85", 1f, 1f);
        }
        for (float x = g.W / 8f; x < g.W; x += g.W / 4f)
        {
            Line(g, x, 0f, x, g.H, "#8a949a", 1f, 1f);
        }
        // Laengsfuge in der Bahnmitte + helle Plattenmitte
        Rect(g, g.W * 0.06f, 0f, g.W * 0.38f, g.H, "#9aa5ab", 1f);
        Rect(g, g.W * 0.56f, 0f, g.W * 0.38f, g.H, "#9aa5ab", 1f);
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#767f85", 2f, 1f);
        Line(g, 0f, 1f, g.W, 1f, "#767f85", 1f, 1f);
        Line(g, 0f, g.H - 1f, g.W, g.H - 1f, "#767f85", 1f, 1f);
        Grain(g, new[] { "#a2adb2", "#87929a" }, 450, 0.10f, 1543);
        } },
        ["aAirEngineFloor"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        Fill(g, "#8b7f80");
        // grosse, blasse Plattenstoesse (die Karte zeigt kaum Raster, nur Flecken)
        Line(g, 0f, g.H * 0.33f, g.W, g.H * 0.33f, "#7d7273", 1f, 0.6f);
        Line(g, g.W * 0.5f, 0f, g.W * 0.5f, g.H, "#7d7273", 1f, 0.6f);
        Grain(g, new[] { "#97897f", "#7e7377", "#948583" }, 700, 0.14f, 2209);
        } },
        ["aAirEngineWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#9b8583");
        // vertikale Paneelfugen
        for (float x = 0f; x < g.W; x += g.W / 3f)
        {
        Line(g, x, 0f, x, g.H, "#8a7573", 2f, 1f);
        }
        Line(g, 0f, 3f, g.W, 3f, "#ab9690", 2f, 1f);
        // helle Perle unter der Kappe
        // dunkler Sockelstreifen
        Rect(g, 0f, g.H * 0.78f, g.W, g.H * 0.22f, "#7e5f67", 1f);
        Line(g, 0f, g.H * 0.78f, g.W, g.H * 0.78f, "#6d535a", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#241719", 3f, 1f);
        // dunkle Basislinie
        Grain(g, new[] { "#a68f89", "#8d7876" }, 400, 0.10f, 4792);
        } },
        ["aAirEngineCap"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#461f20");
        for (float x = 0f; x < g.W; x += g.W / 3f)
        {
        Line(g, x, 0f, x, g.H, "#360f11", 2f, 1f);
        }
        for (float y = 0f; y < g.H; y += g.H / 2f)
        {
        Line(g, 0f, y, g.W, y, "#360f11", 1f, 1f);
        }
        for (float x = g.W / 6f; x < g.W; x += g.W / 3f)
        {
            g.FillEllipse(x, g.H * 0.25f, 1.3f, 1.3f, C("#57292b"), 1f);
            g.FillEllipse(x, g.H * 0.75f, 1.3f, 1.3f, C("#57292b"), 1f);
        }
        Grain(g, new[] { "#4f2426", "#3a1416" }, 300, 0.10f, 2373);
        } },
        ["aAirEngineMachine"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#62687a");
        // horizontale Rippen (Lagerlinien der Maschine)
        for (float y = g.H * 0.12f; y < g.H; y += g.H * 0.22f)
        {
            Line(g, 0f, y, g.W, y, "#52586a", 2f, 1f);
            Line(g, 0f, y + 2f, g.W, y + 2f, "#6f7688", 1f, 1f);
        }
        // Nietreihen
        for (float x = g.W * 0.08f; x < g.W; x += g.W / 6f)
        {
            for (float y = g.H * 0.06f; y < g.H; y += g.H * 0.22f)
            {
                g.FillEllipse(x, y + 1f, 1.1f, 1.1f, C("#4e5466"), 1f);
            }
        }
        Grain(g, new[] { "#6a7082", "#575d70" }, 400, 0.10f, 6916);
        } },
        ["aAirEngineIron"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#20252c");
        for (float x = -g.H; x < g.W + g.H; x += g.W / 3f)
        {
            Line(g, x, 0f, x + g.H, g.H, "#181c22", 3f, 1f);
        }
        for (float x = -g.H + g.W / 6f; x < g.W + g.H; x += g.W / 3f)
        {
            Line(g, x, 0f, x + g.H, g.H, "#2a303a", 1f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#12151a", 2f, 1f);
        Line(g, 0f, g.H - 1f, g.W, g.H - 1f, "#12151a", 2f, 1f);
        Grain(g, new[] { "#262c36", "#1a1f26" }, 350, 0.10f, 1600);
        } },
        ["aAirEngineBrass"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#d1b65a");
        // geschliffene Bogenstreifen (laufen als Band um die Trommel)
        for (float y = 2f; y < g.H; y += 5f)
        {
        Line(g, 0f, y, g.W, y, "#b9973f", 1f, 1f);
        }
        for (float y = 4f; y < g.H; y += 5f)
        {
        Line(g, 0f, y, g.W, y, "#e6cd7d", 1f, 1f);
        }
        Line(g, 0f, 0f, g.W, 0f, "#8f7530", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#8f7530", 2f, 1f);
        Grain(g, new[] { "#dcc168", "#c2a04a" }, 300, 0.10f, 6230);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_airship_gaproom.js
        // ---------------------------------------------------------------
        ["aAirGaproomFloor"] = new Spec { Unit = 1.5f, Draw = g => {
        Fill(g, "#7a5254");
        for (float x = 0f; x <= g.W; x += g.W / 2f)
        {
        Line(g, x, 0f, x, g.H, "#6a4648", 2f, 1f);
        }
        for (float y = 0f; y <= g.H; y += g.H / 2f)
        {
        Line(g, 0f, y, g.W, y, "#6a4648", 2f, 1f);
        }
        Line(g, 1f, 1f, g.W - 1f, 1f, "#8a5e60", 1f, 1f);
        // helle Plattenkante oben
        Line(g, 1f, 1f, 1f, g.H - 1f, "#8a5e60", 1f, 1f);
        Grain(g, new[] { "#845a5c", "#6f4a4c" }, 450, 0.10f, 9842);
        } },
        ["aAirGaproomPit"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#3b3b50");
        for (float x = 0f; x <= g.W; x += g.W / 3f)
        {
        Line(g, x, 0f, x, g.H, "#32324a", 2f, 1f);
        }
        for (float y = 0f; y <= g.H; y += g.H / 2f)
        {
        Line(g, 0f, y, g.W, y, "#32324a", 2f, 1f);
        }
        // Stahldeck-Platten abgesetzt, wie das Riffelmuster des Atlas
        Rect(g, 2f, 2f, g.W / 3f - 4f, g.H / 2f - 4f, "#44445c", 1f);
        Rect(g, g.W / 3f + 2f, g.H / 2f + 2f, g.W / 3f - 4f, g.H / 2f - 4f, "#44445c", 1f);
        Grain(g, new[] { "#454560", "#313148" }, 500, 0.12f, 7561);
        } },
        ["aAirGaproomShade"] = new Spec { Unit = 1.3f, Detail = 1, Draw = g => {
        Fill(g, "#4f2839");
        for (float x = 0f; x <= g.W; x += g.W / 2f)
        {
        Line(g, x, 0f, x, g.H, "#432231", 2f, 1f);
        }
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#432231", 2f, 0.5f);
        Line(g, 1f, 1f, g.W - 1f, 1f, "#5c3044", 1f, 1f);
        Grain(g, new[] { "#582e40", "#46232f" }, 450, 0.12f, 9093);
        } },
        ["aAirGaproomPlate"] = new Spec { Unit = 0.6f, Draw = g => {
        Fill(g, "#4f6060");
        // Riffeln: versetzte Diamanten
        float st = 10f;
        for (float y = 4f; y < g.H; y += st)
        {
            for (float x = ((y / st) % 2f) * st / 2f + 3f; x < g.W; x += st)
            {
                g.FillQuad(x, y - 2.4f, x + 1.8f, y, x, y + 2.4f, x - 1.8f, y, C("#5b7070"), 1f);
            }
        }
        Line(g, 0f, 0f, g.W, 0f, "#39494b", 2f, 0.7f);
        // dunkle Fuge
        Line(g, 0f, 0f, 0f, g.H, "#39494b", 2f, 0.7f);
        Grain(g, new[] { "#576b6b", "#414f4f" }, 350, 0.10f, 192);
        } },
        ["aAirGaproomStep"] = new Spec { Unit = 0.6f, Draw = g => {
        Fill(g, "#414f52");
        // zwei Sprossen (Querlagen der Vorlage)
        Rect(g, 0f, g.H * 0.34f, g.W, g.H * 0.14f, "#4d6064", 1f);
        Rect(g, 0f, g.H * 0.62f, g.W, g.H * 0.14f, "#4d6064", 1f);
        Line(g, 0f, g.H * 0.34f, g.W, g.H * 0.34f, "#313d40", 2f, 0.55f);
        Line(g, 0f, g.H * 0.76f, g.W, g.H * 0.76f, "#313d40", 2f, 0.55f);
        // helle TRITTKANTE vorn (Canvas-oben = Nord)
        Rect(g, 0f, 0f, g.W, g.H * 0.10f, "#7e9898", 1f);
        Line(g, 0f, g.H * 0.10f, g.W, g.H * 0.10f, "#2e3a3d", 2f, 1f);
        // Schattenfuge unter der Kante
        Line(g, 0f, g.H - 1f, g.W, g.H - 1f, "#39474a", 1f, 1f);
        // dunkle Setzkante sued (offener Risser)
        Grain(g, new[] { "#46565a", "#39474a" }, 300, 0.10f, 1037);
        } },
        ["aAirGaproomPitWall"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#425254");
        for (float x = 0f; x < g.W; x += 26f)
        {
        Line(g, x, 0f, x, g.H, "#37464a", 2f, 1f);
        }
        Line(g, 0f, 3f, g.W, 3f, "#4e6062", 2f, 1f);
        // helle Perle unter der Plattenlippe
        // dunkler Fuss der Grubenwand
        Rect(g, 0f, g.H * 0.86f, g.W, g.H * 0.14f, "#39484c", 1f);
        Line(g, 0f, g.H * 0.86f, g.W, g.H * 0.86f, "#2f3d41", 2f, 1f);
        Grain(g, new[] { "#475759", "#3b4a4e" }, 400, 0.10f, 2274);
        } },
        ["aAirGaproomWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#74504f");
        for (float x = 0f; x < g.W; x += 30f)
        {
        Line(g, x, 0f, x, g.H, "#614342", 2f, 1f);
        }
        Line(g, 0f, 3f, g.W, 3f, "#845c5b", 2f, 1f);
        // helle Perle unter der Kappe
        // dunkler Sockelstreifen
        Rect(g, 0f, g.H * 0.80f, g.W, g.H * 0.20f, "#5c3f3e", 1f);
        Line(g, 0f, g.H * 0.80f, g.W, g.H * 0.80f, "#4c3433", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#382625", 3f, 1f);
        // dunkle Basislinie
        Grain(g, new[] { "#7d5756", "#684847" }, 400, 0.10f, 5862);
        } },
        ["aAirGaproomHull"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#3a1313");
        for (float y = 0f; y < g.H; y += 26f)
        {
        Line(g, 0f, y, g.W, y, "#2e0f0f", 2f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#4a1a1a", 1f, 1f);
        // Nieten an den Plankgrenzen
        for (float x = 12f; x < g.W; x += 34f)
        {
            for (float y = 13f; y < g.H; y += 26f)
            {
                g.FillEllipse(x, y, 1.3f, 1.3f, C("#4a1a1a"), 1f);
            }
        }
        Grain(g, new[] { "#421616", "#300f0f" }, 350, 0.10f, 1596);
        } },
        ["aAirGaproomCeil"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#565b64");
        for (float x = 0f; x < g.W; x += g.W / 2f)
        {
        Line(g, x, 0f, x, g.H, "#4c5159", 2f, 1f);
        }
        for (float y = 0f; y < g.H; y += g.H / 2f)
        {
        Line(g, 0f, y, g.W, y, "#4c5159", 2f, 1f);
        }
        Line(g, 1f, 1f, g.W - 1f, 1f, "#60656e", 1f, 1f);
        Grain(g, new[] { "#5b6069", "#50545c" }, 300, 0.10f, 4388);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_airship_hallofportraits.js
        // ---------------------------------------------------------------
        ["aAirPortraitsFloor"] = new Spec { Unit = 0.55f, Draw = g => {
        Fill(g, "#978269");
        // Stossfugen, versetzt
        Rect(g, g.W * 0.34f, 2f, 2f, g.H - 4f, "#7d6a55", 1f);
        Rect(g, g.W * 0.72f, 2f, 2f, g.H - 4f, "#7d6a55", 1f);
        // feine Maserung laengs
        for (float x = 4f; x < g.W; x += 7f)
        {
        Line(g, x, 2f, x + 2f, g.H - 2f, "#a98f74", 1f, 1f);
        }
        Grain(g, new[] { "#a58c71", "#877259" }, 420, 0.10f, 8900);
        } },
        ["aAirPortraitsCarpet"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#b23649");
        Grain(g, new[] { "#a52f42", "#c04358", "#982c3e" }, 750, 0.13f, 2619);
        } },
        ["aAirPortraitsWall"] = new Spec { Unit = 2.1f, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   g.ellipse(w / 2, h * 0.96, w * 0.44, h * 0.62, 0, Math.PI, 0
        Fill(g, "#c6cfd6");
        Grain(g, new[] { "#c6cfd6" }, 400, 0.06f, 4521);
        } },
        ["aAirPortraitsMaroon"] = new Spec { Unit = 2.1f, Draw = g => {
        Fill(g, "#421d1f");
        for (float x = 0f; x < g.W; x += 26f)
        {
        Line(g, x, 0f, x, g.H, "#38181a", 1f, 1f);
        }
        Line(g, 0f, 3f, g.W, 3f, "#5a2a2d", 2f, 1f);
        // helle Kante unter der Kappe
        // dunkle Sockelzone
        Rect(g, 0f, g.H * 0.82f, g.W, g.H * 0.18f, "#371719", 1f);
        Line(g, 0f, g.H * 0.82f, g.W, g.H * 0.82f, "#2b1214", 2f, 1f);
        Grain(g, new[] { "#4a2225", "#38181a" }, 400, 0.10f, 317);
        } },
        ["aAirPortraitsPilaster"] = new Spec { Unit = 0.7f, Draw = g => {
        Fill(g, "#1e2126");
        // Lichtkante links
        Line(g, 2f, 0f, 2f, g.H, "#31353c", 2f, 0.6f);
        Line(g, g.W - 2f, 0f, g.W - 2f, g.H, "#121417", 2f, 1f);
        Grain(g, new[] { "#262a30", "#171a1e" }, 260, 0.09f, 2780);
        } },
        ["aAirPortraitsFrame"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#c9a30c");
        Line(g, 0f, 2f, g.W, 2f, "#8a6d06", 3f, 1f);
        // dunkle Falz oben
        Line(g, 0f, g.H - 4f, g.W, g.H - 4f, "#8a6d06", 3f, 1f);
        // dunkle Falz unten
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#e8c22e", 2f, 1f);
        // helle Mittelschaerfe
        Grain(g, new[] { "#d9b21d", "#a8850a" }, 220, 0.10f, 5580);
        } },
        ["aAirPortraitsCeil"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#2b1618");
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#221114", 2f, 0.6f);
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#221114", 2f, 0.6f);
        Grain(g, new[] { "#321a1d", "#241214" }, 300, 0.10f, 1044);
        } },
        ["aAirPortraitsP1"] = new Spec { Unit = 1.1f, Detail = 1, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   crew(g, { ground: '#95c2c1', shade: '#12150f', pack: '#1d271
        Fill(g, "#95c2c1");
        Grain(g, new[] { "#95c2c1" }, 400, 0.06f, 2096);
        } },
        ["aAirPortraitsP2"] = new Spec { Unit = 1.1f, Detail = 1, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   crew(g, { ground: '#bcc9c4', shade: '#0d2018', pack: '#14363
        Fill(g, "#bcc9c4");
        Grain(g, new[] { "#bcc9c4" }, 400, 0.06f, 5781);
        } },
        ["aAirPortraitsP3"] = new Spec { Unit = 1.1f, Detail = 1, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   crew(g, { ground: '#150d07', shade: '#0c0705', pack: '#24161
        Fill(g, "#150d07");
        Grain(g, new[] { "#150d07" }, 400, 0.06f, 5021);
        } },
        ["aAirPortraitsP4"] = new Spec { Unit = 1.1f, Detail = 1, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   crew(g, { ground: '#0b121e', shade: '#060a12', pack: '#1c305
        Fill(g, "#0b121e");
        Grain(g, new[] { "#0b121e" }, 400, 0.06f, 183);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_airship_hallway.js
        // ---------------------------------------------------------------
        ["aAirHallwayFloor"] = new Spec { Unit = 1.02f, Detail = 1, Draw = g => {
        Fill(g, "#3e6488");
        // feine, weit auseinanderliegende Querfugen - auf 7.5 Einheiten sonst eine Plastikbahn
        Line(g, 0f, 2f, g.W, 2f, "#33547a", 1f, 0.25f);
        Line(g, 0f, g.H - 3f, g.W, g.H - 3f, "#33547a", 1f, 0.25f);
        Grain(g, new[] { "#456e94", "#365a7c", "#41678b" }, 450, 0.10f, 8763);
        } },
        ["aAirHallwayWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#9e8262");
        for (float x = 0f; x < g.W; x += 36f)
        {
        Line(g, x, 0f, x, g.H, "#8a6f54", 1f, 1f);
        }
        // Paneelstoesse
        Line(g, 0f, 3f, g.W, 3f, "#b3946f", 2f, 1f);
        // helle Perle unter der Kappe
        // dunklerer Sockelstreifen
        Rect(g, 0f, g.H * 0.8f, g.W, g.H * 0.2f, "#8a6f54", 1f);
        Line(g, 0f, g.H * 0.8f, g.W, g.H * 0.8f, "#755d46", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#5f4a38", 3f, 1f);
        // dunkle Basislinie
        Grain(g, new[] { "#a8896a", "#8f755a" }, 380, 0.10f, 5977);
        } },
        ["aAirHallwayCap"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#67727e");
        for (float y = 0f; y < g.H; y += 14f)
        {
        Line(g, 0f, y, g.W, y, "#495058", 2f, 1f);
        }
        // Riffelstreifen
        Line(g, 0f, 1f, g.W, 1f, "#77828e", 1f, 1f);
        Grain(g, new[] { "#6e7986", "#59616c" }, 320, 0.10f, 1009);
        } },
        ["aAirHallwayDoor"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        Fill(g, "#62757a");
        // die dunkle Mittelbahn
        Rect(g, g.W * 0.38f, 0f, g.W * 0.24f, g.H, "#48595c", 1f);
        Line(g, g.W * 0.38f, 0f, g.W * 0.38f, g.H, "#3d4c4f", 2f, 0.6f);
        Line(g, g.W * 0.62f, 0f, g.W * 0.62f, g.H, "#3d4c4f", 2f, 0.6f);
        Line(g, 2f, 0f, 2f, g.H, "#75888d", 1f, 1f);
        // helle Blattkante
        Line(g, g.W - 3f, 0f, g.W - 3f, g.H, "#4e5f63", 2f, 1f);
        Grain(g, new[] { "#6a7d83", "#57686d" }, 260, 0.10f, 7216);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_airship_hull.js
        // ---------------------------------------------------------------
        ["aAirHullDeck"] = new Spec { Unit = 1.1f, Detail = 1, Draw = g => {
        Fill(g, "#421d1f");
        // Riffelblech: ueberkreuzte Diagonalrippen als helles/dunkles Paar
        for (float x = -g.H; x < g.W; x += 14f)
        {
            Line(g, x, g.H, x + g.H, 0f, "#4c2527", 2f, 0.5f);
            Line(g, x + 2f, g.H, x + g.H + 2f, 0f, "#311619", 1f, 0.5f);
        }
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#2e1416", 2f, 0.7f);
        // Paneelfuge quer
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#2e1416", 2f, 0.7f);
        // Paneelfuge laengs
        Line(g, 0f, 1f, g.W, 1f, "#542628", 1f, 1f);
        // helle Plattenkante oben
        Grain(g, new[] { "#482122", "#381a1c" }, 400, 0.10f, 5535);
        } },
        ["aAirHullWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#451f22");
        // jede zweite Platte abgesetzt
        Rect(g, g.W / 2f + 1f, 2f, g.W / 2f - 3f, g.H - 4f, "#3d1b1e", 1f);
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#2e1416", 2f, 0.6f);
        // vertikale Fuge in der Einheit
        Line(g, 0f, g.H * 0.5f, g.W, g.H * 0.5f, "#2e1416", 1f, 0.6f);
        // horizontale Fuge
        // Nieten oben + unten
        for (float x = 10f; x < g.W; x += 36f)
        {
            g.FillEllipse(x, 6f, 1.3f, 1.3f, C("#55292c"), 1f);
            g.FillEllipse(x, g.H - 6f, 1.3f, 1.3f, C("#55292c"), 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#542628", 1f, 1f);
        Grain(g, new[] { "#4d2427", "#3b1a1d" }, 350, 0.10f, 9645);
        } },
        ["aAirHullSkirt"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#7c212e");
        // die hellere Falte der Zeichnung als laengs Band
        Rect(g, 0f, g.H * 0.28f, g.W, g.H * 0.20f, "#93383e", 1f);
        for (float x = 0f; x < g.W; x += 56f)
        {
        Line(g, x, 0f, x, g.H, "#5c1a24", 2f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#a84a52", 1f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#5c1a24", 2f, 1f);
        Grain(g, new[] { "#87333d", "#6e1d28" }, 380, 0.10f, 9409);
        } },
        ["aAirHullRim"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        Fill(g, "#38191b");
        for (float y = 4f; y < g.H; y += 12f)
        {
        Line(g, 0f, y, g.W, y, "#2b1315", 1f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#451f22", 1f, 1f);
        Grain(g, new[] { "#3f1d1f", "#2e1416" }, 250, 0.10f, 6685);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_airship_kitchen.js
        // ---------------------------------------------------------------
        ["aAirKitchenFloor"] = new Spec { Unit = 0.85f, Detail = 1, Draw = g => {
        Fill(g, "#c2d1d4");
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#aebfc5", 2f, 0.55f);
        // Fugenkreuz in der Einheitsmitte
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#aebfc5", 2f, 0.55f);
        Line(g, 0f, 1f, g.W, 1f, "#b7c7cd", 1f, 0.35f);
        Line(g, 1f, 0f, 1f, g.H, "#b7c7cd", 1f, 0.35f);
        // jede zweite Platte einen Hauch dunkler, wie gespachtelter Schiffsbelag
        Rect(g, 2f, 2f, g.W / 2f - 4f, g.H / 2f - 4f, "#bac9cf", 1f);
        Grain(g, new[] { "#cbd9de", "#b4c4ca" }, 420, 0.09f, 1914);
        } },
        ["aAirKitchenWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#421d1e");
        for (float x = 0f; x < g.W; x += 36f)
        {
        Line(g, x, 0f, x, g.H, "#341517", 2f, 1f);
        }
        Line(g, 0f, 3f, g.W, 3f, "#57302f", 2f, 1f);
        // Perle unter der Kappe
        // Sockelstreifen
        Rect(g, 0f, g.H * 0.78f, g.W, g.H * 0.22f, "#371719", 1f);
        Line(g, 0f, g.H * 0.78f, g.W, g.H * 0.78f, "#2a1113", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#200d0f", 3f, 1f);
        // dunkle Basislinie
        Grain(g, new[] { "#4a2325", "#3a181a" }, 380, 0.10f, 5024);
        } },
        ["aAirKitchenHull"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#301315");
        for (float x = 0f; x < g.W; x += 44f)
        {
        Line(g, x, 0f, x, g.H, "#250e10", 2f, 1f);
        }
        for (float y = 0f; y < g.H; y += 30f)
        {
        Line(g, 0f, y, g.W, y, "#250e10", 1f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#3d1a1c", 1f, 1f);
        for (float x = 10f; x < g.W; x += 44f)
        {
            g.FillEllipse(x, 8f, 1.4f, 1.4f, C("#1e0a0c"), 1f);
            g.FillEllipse(x, g.H - 8f, 1.4f, 1.4f, C("#1e0a0c"), 1f);
        }
        Grain(g, new[] { "#37171a", "#28090b" }, 320, 0.10f, 5889);
        } },
        ["aAirKitchenCabinet"] = new Spec { Unit = 0.62f, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   { for (const [cx, cy] of [[w * 0.40, h * 0.44], [w * 0.60, h
        Fill(g, "#89afbb");
        Grain(g, new[] { "#89afbb" }, 400, 0.06f, 2951);
        } },
        ["aAirKitchenSteel"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#5b6970");
        for (float i = 0f; i < 3f; i += 1f)
        {
            // drei Blechbahnen je Einheit
            float y = (g.H * i) / 3f;
            Line(g, 0f, y, g.W, y, "#49565c", 2f, 0.8f);
            // Blechnaht
            Line(g, 0f, y + 2f, g.W, y + 2f, "#7a8a92", 1f, 0.6f);
            // Lichtkante darunter
        }
        for (float y = 6f; y < g.H; y += 5f)
        {
        Line(g, 0f, y, g.W, y, "#66747b", 1f, 1f);
        }
        // Schleifspuren
        Grain(g, new[] { "#64727a", "#525f66" }, 360, 0.08f, 6311);
        } },
        ["aAirKitchenTop"] = new Spec { Unit = 0.55f, Draw = g => {
        Fill(g, "#81a3ae");
        for (float y = 3f; y < g.H; y += 7f)
        {
        Line(g, 0f, y, g.W, y, "#8badb7", 1f, 1f);
        }
        // Buerstung
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#5a7680", 2f, 0.85f);
        // Plattenfugen-Kreuz
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#5a7680", 2f, 0.85f);
        Line(g, g.W / 2f + 2f, 0f, g.W / 2f + 2f, g.H, "#a0bec8", 1f, 0.5f);
        // Lichtkante an der Fuge
        Line(g, 0f, g.H / 2f + 2f, g.W, g.H / 2f + 2f, "#a0bec8", 1f, 0.5f);
        Line(g, 0f, 1f, g.W, 1f, "#93b3bd", 1f, 0.5f);
        Grain(g, new[] { "#89abb6", "#789aa6" }, 260, 0.07f, 3278);
        } },
        ["aAirKitchenTopTeal"] = new Spec { Unit = 0.55f, Draw = g => {
        Fill(g, "#305b67");
        for (float y = 3f; y < g.H; y += 7f)
        {
        Line(g, 0f, y, g.W, y, "#38656f", 1f, 1f);
        }
        // Buerstung
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#1e3a42", 2f, 0.9f);
        // Plattenfugen-Kreuz
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#1e3a42", 2f, 0.9f);
        Line(g, g.W / 2f + 2f, 0f, g.W / 2f + 2f, g.H, "#6e96a0", 1f, 0.45f);
        // Lichtkante an der Fuge
        Line(g, 0f, g.H / 2f + 2f, g.W, g.H / 2f + 2f, "#6e96a0", 1f, 0.45f);
        Line(g, 0f, 1f, g.W, 1f, "#3d6a74", 1f, 0.5f);
        Grain(g, new[] { "#355f6b", "#2b525e" }, 240, 0.07f, 1480);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_airship_lounge.js
        // ---------------------------------------------------------------
        ["aAirLoungeFloorPurple"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        Fill(g, "#4b3f60");
        for (float y = -g.H; y < g.H * 2f; y += g.W / 2f + g.H / 2f)
        {
            // Rauten als versetztes Diagonalraster
            for (float x = -g.W; x < g.W * 2f; x += g.W / 2f + g.H / 2f)
            {
                Line(g, x + (g.W / 2f + g.H / 2f) / 2f, y, x + (g.W / 2f + g.H / 2f), y + (g.W / 2f + g.H / 2f) / 2f, "#423757", 2f, 0.45f);
                Line(g, x + (g.W / 2f + g.H / 2f), y + (g.W / 2f + g.H / 2f) / 2f, x + (g.W / 2f + g.H / 2f) / 2f, y + (g.W / 2f + g.H / 2f), "#423757", 2f, 0.45f);
                Line(g, x + (g.W / 2f + g.H / 2f) / 2f, y + (g.W / 2f + g.H / 2f), x, y + (g.W / 2f + g.H / 2f) / 2f, "#423757", 2f, 0.45f);
            }
        }
        Grain(g, new[] { "#544768", "#413554" }, 550, 0.12f, 2520);
        } },
        ["aAirLoungeTile"] = new Spec { Unit = 0.6f, Draw = g => {
        Fill(g, "#99a1a5");
        // Plattenmitte minimal heller
        Rect(g, 2f, 2f, g.W / 2f - 4f, g.H / 2f - 4f, "#a2aab0", 1f);
        Rect(g, g.W / 2f + 2f, g.H / 2f + 2f, g.W / 2f - 4f, g.H / 2f - 4f, "#a2aab0", 1f);
        // jede zweite Platte abgesetzt
        Rect(g, g.W / 2f + 2f, 2f, g.W / 2f - 4f, g.H / 2f - 4f, "#8d959b", 1f);
        Rect(g, 2f, g.H / 2f + 2f, g.W / 2f - 4f, g.H / 2f - 4f, "#8d959b", 1f);
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#79828a", 2f, 0.85f);
        // Fugenkreuz
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#79828a", 2f, 0.85f);
        Line(g, 0f, 1f, g.W, 1f, "#79828a", 1f, 1f);
        Line(g, 1f, 0f, 1f, g.H, "#79828a", 1f, 1f);
        Grain(g, new[] { "#a6aeb4", "#8a9298" }, 400, 0.09f, 7106);
        } },
        ["aAirLoungeStallFloor"] = new Spec { Unit = 0.7f, Draw = g => {
        Fill(g, "#6d8087");
        for (float x = 0f; x < g.W; x += 22f)
        {
        Line(g, x, 0f, x, g.H, "#5f727a", 1f, 1f);
        }
        for (float y = 0f; y < g.H; y += 22f)
        {
        Line(g, 0f, y, g.W, y, "#5f727a", 1f, 1f);
        }
        Grain(g, new[] { "#77898f", "#5f727a" }, 450, 0.10f, 7322);
        } },
        ["aAirLoungeWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#421d20");
        for (float x = 0f; x < g.W; x += 36f)
        {
        Line(g, x, 0f, x, g.H, "#361719", 2f, 1f);
        }
        Line(g, 0f, 3f, g.W, 3f, "#54292c", 2f, 1f);
        // helle Perle unter der Kappe
        // dunkler Sockelstreifen
        Rect(g, 0f, g.H * 0.78f, g.W, g.H * 0.22f, "#331518", 1f);
        Line(g, 0f, g.H * 0.78f, g.W, g.H * 0.78f, "#2a1113", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#200d0f", 3f, 1f);
        // dunkle Basislinie
        Grain(g, new[] { "#4b2225", "#38181b" }, 420, 0.10f, 5164);
        } },
        ["aAirLoungeHull"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#2b1417");
        for (float x = 0f; x < g.W; x += 44f)
        {
        Line(g, x, 0f, x, g.H, "#210f12", 2f, 1f);
        }
        for (float y = 0f; y < g.H; y += 30f)
        {
        Line(g, 0f, y, g.W, y, "#210f12", 1f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#381b1f", 1f, 1f);
        for (float x = 10f; x < g.W; x += 44f)
        {
            g.FillEllipse(x, 8f, 1.4f, 1.4f, C("#1d0d10"), 1f);
            g.FillEllipse(x, g.H - 8f, 1.4f, 1.4f, C("#1d0d10"), 1f);
        }
        Grain(g, new[] { "#31161a", "#241013" }, 350, 0.10f, 696);
        } },
        ["aAirLoungeCeil"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#565b64");
        for (float x = 0f; x < g.W; x += g.W / 2f)
        {
        Line(g, x, 0f, x, g.H, "#4c5159", 2f, 1f);
        }
        for (float y = 0f; y < g.H; y += g.H / 2f)
        {
        Line(g, 0f, y, g.W, y, "#4c5159", 2f, 1f);
        }
        Grain(g, new[] { "#5b6069", "#50545c" }, 300, 0.08f, 7719);
        } },
        ["aAirLoungeStallGreen"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#2b5031");
        for (float x = 6f; x < g.W; x += 16f)
        {
        Line(g, x, 0f, x, g.H, "#224227", 2f, 1f);
        }
        // Vertaelung
        Line(g, 0f, 2f, g.W, 2f, "#3a6244", 1f, 1f);
        // helle Oberkante
        // dunkler Fuss
        Rect(g, 0f, g.H * 0.86f, g.W, g.H * 0.14f, "#1d3a22", 1f);
        Grain(g, new[] { "#32593a", "#244529" }, 380, 0.10f, 6023);
        } },
        ["aAirLoungeStallDoor"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#31593a");
        for (float x = 6f; x < g.W; x += 16f)
        {
        Line(g, x, 0f, x, g.H, "#284a30", 2f, 1f);
        }
        // Klinke als dunkler Vertikalstreifen
        Rect(g, g.W - 8f, g.H * 0.4f, 3f, g.H * 0.2f, "#1d3a22", 1f);
        Line(g, 0f, 2f, g.W, 2f, "#3d6647", 1f, 1f);
        Rect(g, 0f, g.H * 0.88f, g.W, g.H * 0.12f, "#1d3a22", 1f);
        Grain(g, new[] { "#385f41", "#294b31" }, 320, 0.10f, 3977);
        } },
        ["aAirLoungeWood"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#57503f");
        for (float y = 4f; y < g.H; y += 10f)
        {
        Line(g, 0f, y, g.W, y, "#4a4436", 1f, 1f);
        }
        // Maserung
        Line(g, 0f, 2f, g.W, 2f, "#665d49", 1f, 1f);
        Grain(g, new[] { "#5e5644", "#4c4536" }, 400, 0.10f, 8475);
        } },
        ["aAirLoungeFelt"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#337750");
        Grain(g, new[] { "#3a825a", "#2c6a47" }, 700, 0.14f, 7393);
        } },
        ["aAirLoungeFeltOlive"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#2a2a22");
        Grain(g, new[] { "#31312a", "#23231c" }, 700, 0.14f, 4558);
        } },
        ["aAirLoungeRail"] = new Spec { Unit = 0.6f, Draw = g => {
        Fill(g, "#07070d");
        Line(g, 0f, 2f, g.W, 2f, "#12121e", 1f, 0.5f);
        Line(g, 0f, g.H - 3f, g.W, g.H - 3f, "#03030a", 1f, 0.5f);
        Grain(g, new[] { "#0c0c16", "#05050b" }, 260, 0.10f, 6955);
        } },
        ["aAirLoungePorcelain"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#76939b");
        Line(g, 0f, g.H * 0.3f, g.W, g.H * 0.3f, "#8aa6ae", 2f, 0.35f);
        // Lichtkante
        Grain(g, new[] { "#7f9ba3", "#6a8790" }, 350, 0.08f, 4815);
        } },
        ["aAirLoungeBin"] = new Spec { Unit = 0.6f, Draw = g => {
        Fill(g, "#3b3546");
        for (float y = 0f; y < g.H; y += 12f)
        {
        Line(g, 0f, y, g.W, y, "#312c3b", 1f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#474055", 1f, 1f);
        Grain(g, new[] { "#423c50", "#332e3e" }, 320, 0.10f, 6795);
        } },
        ["aAirLoungeStoolRed"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#67282b");
        Line(g, 0f, g.H * 0.35f, g.W, g.H * 0.35f, "#7a3438", 2f, 0.35f);
        Grain(g, new[] { "#712d31", "#5a2225" }, 320, 0.10f, 5623);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_airship_mainhall.js
        // ---------------------------------------------------------------
        ["aAirMainhallFloor"] = new Spec { Unit = 1.6f, Draw = g => {
        Fill(g, "#3e6388");
        // Plattenmitte leicht aufgehellt
        Rect(g, 3f, 3f, g.W / 2f - 5f, g.H / 2f - 5f, "#4470a0", 1f);
        Rect(g, g.W / 2f + 2f, g.H / 2f + 2f, g.W / 2f - 5f, g.H / 2f - 5f, "#4470a0", 1f);
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#33567c", 2f, 0.7f);
        // Fugenkreuz
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#33567c", 2f, 0.7f);
        Line(g, 0f, 1f, g.W, 1f, "#2f5074", 1f, 1f);
        Line(g, 1f, 0f, 1f, g.H, "#2f5074", 1f, 1f);
        Grain(g, new[] { "#4a77a8", "#375a80" }, 500, 0.10f, 6353);
        } },
        ["aAirMainhallGrey"] = new Spec { Unit = 0.62f, Draw = g => {
        Fill(g, "#848f92");
        Rect(g, 2f, 2f, g.W / 2f - 3f, g.H / 2f - 3f, "#8d989b", 1f);
        Rect(g, g.W / 2f + 1f, g.H / 2f + 1f, g.W / 2f - 3f, g.H / 2f - 3f, "#8d989b", 1f);
        // jede zweite Platte abgesetzt
        Rect(g, g.W / 2f + 1f, 2f, g.W / 2f - 3f, g.H / 2f - 3f, "#798487", 1f);
        Rect(g, 2f, g.H / 2f + 1f, g.W / 2f - 3f, g.H / 2f - 3f, "#798487", 1f);
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#6b7578", 2f, 0.8f);
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#6b7578", 2f, 0.8f);
        Line(g, 0f, 1f, g.W, 1f, "#646e71", 1f, 1f);
        Line(g, 1f, 0f, 1f, g.H, "#646e71", 1f, 1f);
        Grain(g, new[] { "#8d989b", "#798487" }, 400, 0.10f, 3370);
        } },
        ["aAirMainhallMauve"] = new Spec { Unit = 1.1f, Detail = 1, Draw = g => {
        Fill(g, "#725660");
        for (float y = 0f; y < g.H; y += 18f)
        {
        Line(g, 0f, y, g.W, y, "#644c55", 1f, 1f);
        }
        Grain(g, new[] { "#7a5d68", "#684e58" }, 450, 0.11f, 4586);
        } },
        ["aAirMainhallTerra"] = new Spec { Unit = 0.55f, Draw = g => {
        Fill(g, "#8d5b53");
        for (float y = 2f; y < g.H - 6f; y += 14f)
        {
            for (float x = 2f; x < g.W - 6f; x += 14f)
            {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   g.fillRect(x, y, 10, 10)
        Fill(g, "#8a8a8a");
        Grain(g, new[] { "#8a8a8a" }, 400, 0.06f, 4105);
            }
        }
        for (float y = 0f; y < g.H; y += 14f)
        {
        Line(g, 0f, y, g.W, y, "#754a43", 1f, 1f);
        }
        for (float x = 0f; x < g.W; x += 14f)
        {
        Line(g, x, 0f, x, g.H, "#754a43", 1f, 1f);
        }
        Grain(g, new[] { "#95625a", "#83544c" }, 400, 0.10f, 4105);
        } },
        ["aAirMainhallWallRed"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#a94546");
        for (float x = 0f; x < g.W; x += 30f)
        {
        Line(g, x, 0f, x, g.H, "#8f3a3b", 2f, 1f);
        }
        Line(g, 0f, 3f, g.W, 3f, "#c07a72", 2f, 1f);
        // helle Perle unter der Kappe
        // dunkler Sockelstreifen
        Rect(g, 0f, g.H * 0.80f, g.W, g.H * 0.20f, "#8f3a3b", 1f);
        Line(g, 0f, g.H * 0.80f, g.W, g.H * 0.80f, "#753031", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#5c2627", 3f, 1f);
        // dunkle Basislinie
        Grain(g, new[] { "#b24d4c", "#9c3f40" }, 400, 0.10f, 4216);
        } },
        ["aAirMainhallCap"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#421d1f");
        for (float x = 0f; x < g.W; x += 40f)
        {
        Line(g, x, 0f, x, g.H, "#351618", 2f, 1f);
        }
        for (float y = 0f; y < g.H; y += 26f)
        {
        Line(g, 0f, y, g.W, y, "#351618", 1f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#542528", 1f, 1f);
        Grain(g, new[] { "#4a2124", "#3a191b" }, 350, 0.10f, 8468);
        } },
        ["aAirMainhallHull"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#331619");
        for (float x = 0f; x < g.W; x += 44f)
        {
        Line(g, x, 0f, x, g.H, "#281114", 2f, 1f);
        }
        for (float y = 0f; y < g.H; y += 30f)
        {
        Line(g, 0f, y, g.W, y, "#281114", 1f, 1f);
        }
        for (float x = 10f; x < g.W; x += 44f)
        {
            g.FillEllipse(x, 8f, 1.4f, 1.4f, C("#241013"), 1f);
            g.FillEllipse(x, g.H - 8f, 1.4f, 1.4f, C("#241013"), 1f);
        }
        Grain(g, new[] { "#3a191c", "#2b1316" }, 350, 0.10f, 6264);
        } },
        ["aAirMainhallSteel"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#4a5a5e");
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#3c4a4d", 2f, 0.7f);
        // die Mittelfuge
        for (float y = 0f; y < g.H; y += 22f)
        {
        Line(g, 0f, y, g.W, y, "#41504f", 1f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#5c6f73", 1f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#33403f", 2f, 1f);
        Grain(g, new[] { "#516266", "#434f52" }, 350, 0.10f, 3111);
        } },
        ["aAirMainhallCeiling"] = new Spec { Unit = 1.3f, Detail = 1, Draw = g => {
        Fill(g, "#31383e");
        for (float x = 0f; x < g.W; x += 32f)
        {
        Line(g, x, 0f, x, g.H, "#282e33", 2f, 1f);
        }
        for (float y = 0f; y < g.H; y += 32f)
        {
        Line(g, 0f, y, g.W, y, "#282e33", 2f, 1f);
        }
        Grain(g, new[] { "#384047", "#2b3136" }, 350, 0.10f, 2458);
        } },
        ["aAirMainhallPhotos"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   g.quadraticCurveTo(w / 2, h * 0.52, w - 4, h * 0.42)
        Fill(g, "#421d1f");
        Grain(g, new[] { "#421d1f" }, 400, 0.06f, 7631);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_airship_medical.js
        // ---------------------------------------------------------------
        ["aAirMedicalWood"] = new Spec { Unit = 0.62f, Draw = g => {
        Fill(g, "#72514a");
        // jede zweite Diele abgesetzt
        Rect(g, 0f, g.H / 2f, g.W, g.H / 2f, "#63443c", 1f);
        Line(g, 0f, 1f, g.W, 1f, "#452f2a", 2f, 0.8f);
        // Fuge oben
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#452f2a", 2f, 0.8f);
        // Fuge Mitte
        Line(g, 0f, g.H - 1f, g.W, g.H - 1f, "#452f2a", 2f, 0.8f);
        // Fuge unten
        // versetzte Stossfugen
        Rect(g, g.W * 0.31f, 2f, 2f, g.H / 2f - 4f, "#452f2a", 1f);
        Rect(g, g.W * 0.67f, g.H / 2f + 2f, 2f, g.H / 2f - 4f, "#452f2a", 1f);
        Grain(g, new[] { "#7a584e", "#5c4038" }, 420, 0.10f, 4912);
        } },
        ["aAirMedicalHatch"] = new Spec { Unit = 0.62f, Draw = g => {
        Fill(g, "#6e6459");
        for (float x = -g.H; x < g.W; x += 24f)
        {
            // 45°-Streifen, SW -> NO
            Line(g, x, g.H + 2f, x + g.H + 4f, -2f, "#544a42", 5f, 1f);
        }
        Grain(g, new[] { "#786e62", "#635a50" }, 400, 0.10f, 3321);
        } },
        ["aAirMedicalThreshold"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#5f6a72");
        Line(g, g.W * 0.82f, 0f, g.W * 0.82f, g.H, "#26282a", 4f, 0.7f);
        // die dunkle Fuge zur Schraffur
        for (float y = 6f; y < g.H; y += 18f)
        {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   g.fillRect(w * 0.3, y, 2, 2)
        Fill(g, "#8a8a8a");
        Grain(g, new[] { "#8a8a8a" }, 400, 0.06f, 889);
        }
        // Nieten
        Grain(g, new[] { "#68737b", "#525c64" }, 260, 0.10f, 889);
        } },
        ["aAirMedicalWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#421d1f");
        for (float x = 0f; x < g.W; x += 34f)
        {
        Line(g, x, 0f, x, g.H, "#331517", 2f, 1f);
        }
        Line(g, 0f, 3f, g.W, 3f, "#5c2e30", 2f, 1f);
        // Perle unter der Kappe
        // dunkler Sockelstreifen
        Rect(g, 0f, g.H * 0.8f, g.W, g.H * 0.2f, "#331517", 1f);
        Line(g, 0f, g.H * 0.8f, g.W, g.H * 0.8f, "#240f11", 2f, 1f);
        Grain(g, new[] { "#4a2224", "#38181a" }, 380, 0.10f, 8231);
        } },
        ["aAirMedicalBandWhite"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#aebfc6");
        for (float x = 0f; x < g.W; x += 42f)
        {
        Line(g, x, 0f, x, g.H, "#9aadb4", 2f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#c2d2d8", 2f, 1f);
        // helle Perle unter der Kappe
        // dunkle Sockelkante
        Rect(g, 0f, g.H * 0.86f, g.W, g.H * 0.14f, "#4a2f28", 1f);
        Line(g, 0f, g.H * 0.86f, g.W, g.H * 0.86f, "#3a231e", 2f, 1f);
        Grain(g, new[] { "#b8c8ce", "#a2b3ba" }, 320, 0.08f, 7879);
        } },
        ["aAirMedicalWainscot"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#b5b6a6");
        // creme Oberwand
        Line(g, 0f, g.H * 0.55f, g.W, g.H * 0.55f, "#5a4632", 3f, 1f);
        // Kante zur Verkleidung
        // Holzverkleidung unten
        Rect(g, 0f, g.H * 0.55f, g.W, g.H * 0.45f, "#95705f", 1f);
        for (float x = 5f; x < g.W; x += 11f)
        {
        Line(g, x, g.H * 0.55f, x, g.H, "#6f5344", 2f, 1f);
        }
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#4a382a", 2f, 1f);
        Grain(g, new[] { "#bdbfb0", "#a8a996" }, 300, 0.08f, 373);
        } },
        ["aAirMedicalColumn"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#5f6a72");
        for (float y = 4f; y < g.H; y += 12f)
        {
        Line(g, 0f, y, g.W, y, "#4e5860", 2f, 1f);
        }
        // Riffel
        // weisse Kappe oben
        Rect(g, 0f, 0f, g.W, g.H * 0.16f, "#b5b6a6", 1f);
        Line(g, 0f, g.H * 0.16f, g.W, g.H * 0.16f, "#3f4850", 2f, 1f);
        // dunkler Fuss
        Rect(g, 0f, g.H * 0.94f, g.W, g.H * 0.06f, "#3f4850", 1f);
        Grain(g, new[] { "#68737b", "#545e66" }, 260, 0.10f, 8607);
        } },
        ["aAirMedicalWhite"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        Fill(g, "#aebfc6");
        for (float x = 0f; x < g.W; x += 30f)
        {
        Line(g, x, 0f, x, g.H, "#9aadb4", 1f, 1f);
        }
        // dunkle Sockelkante (wie am Band)
        Rect(g, 0f, g.H * 0.88f, g.W, g.H * 0.12f, "#4a2f28", 1f);
        Line(g, 0f, g.H * 0.88f, g.W, g.H * 0.88f, "#3a231e", 2f, 1f);
        Grain(g, new[] { "#b8c8ce", "#a2b3ba" }, 260, 0.08f, 9332);
        } },
        ["aAirMedicalCabinet"] = new Spec { Unit = 0.6f, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   { for (const dy of [0.08, 0.56])
        Fill(g, "#a4c4ca");
        Grain(g, new[] { "#a4c4ca" }, 400, 0.06f, 6435);
        } },
        ["aAirMedicalSideboard"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#6f5436");
        Line(g, 0f, 3f, g.W, 3f, "#8a6a46", 2f, 1f);
        // helle Kante unter der Platte
        Line(g, g.W / 3f, g.H * 0.2f, g.W / 3f, g.H * 0.95f, "#54402a", 2f, 0.8f);
        // Tuerfugen
        Line(g, g.W * 2f / 3f, g.H * 0.2f, g.W * 2f / 3f, g.H * 0.95f, "#54402a", 2f, 0.8f);
        Line(g, 0f, g.H * 0.2f, g.W, g.H * 0.2f, "#54402a", 2f, 0.8f);
        Grain(g, new[] { "#7a5e3e", "#5f4830" }, 300, 0.10f, 7367);
        } },
        ["aAirMedicalDoorSteel"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#767f81");
        for (float x = -g.H; x < g.W; x += 26f)
        {
            // Diagonalrippen wie im Crop
            Line(g, x, g.H + 2f, x + g.H + 4f, -2f, "#5f6a72", 4f, 1f);
        }
        Line(g, 2f, 0f, 2f, g.H, "#59626a", 3f, 0.8f);
        // Randstile
        Line(g, g.W - 2f, 0f, g.W - 2f, g.H, "#59626a", 3f, 0.8f);
        Grain(g, new[] { "#7e878a", "#6a7376" }, 240, 0.10f, 4777);
        } },
        ["aAirMedicalHull"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#3f1c1e");
        for (float x = 0f; x < g.W; x += 44f)
        {
        Line(g, x, 0f, x, g.H, "#2e1214", 2f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#4e2426", 1f, 1f);
        Grain(g, new[] { "#482022", "#341517" }, 300, 0.10f, 3514);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_airship_meetingroom.js
        // ---------------------------------------------------------------
        ["aAirMeetingCarpet"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        Fill(g, "#4c5c6a");
        for (float y = 4f; y < g.H; y += 7f)
        {
        Line(g, 0f, y, g.W, y, "#46555f", 1f, 1f);
        }
        for (float x = 6f; x < g.W; x += 13f)
        {
        Line(g, x, 0f, x, g.H, "#52626f", 1f, 1f);
        }
        Grain(g, new[] { "#5d6e7d", "#41505b" }, 550, 0.12f, 8418);
        } },
        ["aAirMeetingFloor"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#557586");
        Line(g, 0f, 1f, g.W, 1f, "#496878", 2f, 0.7f);
        Line(g, 1f, 0f, 1f, g.H, "#496878", 2f, 0.7f);
        for (float x = 0.6f; x < g.W; x += 1.2f)
        {
        Line(g, x, 0f, x, g.H, "#4c6a7d", 1f, 1f);
        }
        Grain(g, new[] { "#62859a", "#4b6a7c" }, 450, 0.10f, 7158);
        } },
        ["aAirMeetingLanding"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#939ba3");
        for (float y = 3f; y < g.H; y += 5f)
        {
        Line(g, 0f, y, g.W, y, "#828d96", 1f, 1f);
        }
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#77828a", 2f, 1f);
        Grain(g, new[] { "#a0a8b0", "#848e97" }, 400, 0.10f, 298);
        } },
        ["aAirMeetingWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#b8b2a9");
        for (float x = 0f; x < g.W; x += 36f)
        {
        Line(g, x, 0f, x, g.H, "#a29c92", 1f, 1f);
        }
        Line(g, 0f, 3f, g.W, 3f, "#c8c2b8", 2f, 1f);
        // helle Perle unter der Kappe
        // dunkler Fuss
        Rect(g, 0f, g.H * 0.76f, g.W, g.H * 0.24f, "#8f887e", 1f);
        Line(g, 0f, g.H * 0.76f, g.W, g.H * 0.76f, "#7d766c", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#4a453f", 3f, 1f);
        Grain(g, new[] { "#c1bbb1", "#a8a298" }, 400, 0.10f, 457);
        } },
        ["aAirMeetingWallArch"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#a2988f");
        for (float x = 0f; x < g.W; x += 44f)
        {
        Line(g, x, 0f, x, g.H, "#948a80", 2f, 1f);
        }
        Line(g, 0f, 3f, g.W, 3f, "#b5aba1", 2f, 1f);
        Rect(g, 0f, g.H * 0.8f, g.W, g.H * 0.2f, "#8a8076", 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#57504a", 3f, 1f);
        Grain(g, new[] { "#aca298", "#968c82" }, 400, 0.10f, 867);
        } },
        ["aAirMeetingArch"] = new Spec { Unit = 0.85f, Detail = 1, Draw = g => {
        Fill(g, "#b8b2a9");
        // Bogen: Rundbogen ueber der Mittellinie
        float r = g.W * 0.34f, cx = g.W / 2f, ay = g.H * 0.34f;
        Line(g, cx - r, g.H - 6f, cx - r, ay, "#948d82", 3f, 1f);
        Line(g, cx - r, ay, cx + r, g.H - 6f, "#948d82", 3f, 1f);
        Line(g, cx - r + 5f, g.H - 8f, cx - r + 5f, ay + 2f, "#a59e94", 1f, 1f);
        Line(g, cx - r + 5f, ay + 2f, cx + r - 5f, g.H - 8f, "#a59e94", 1f, 1f);
        // Nische leicht aufgehellt
        Rect(g, cx - r + 7f, ay + 4f, r * 2f - 14f, g.H - ay - 10f, "#c4beb3", 1f);
        Grain(g, new[] { "#beb8ad", "#aca699" }, 250, 0.10f, 9803);
        } },
        ["aAirMeetingWallRed"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#763f4a");
        for (float x = 0f; x < g.W; x += 30f)
        {
        Line(g, x, 0f, x, g.H, "#63323d", 2f, 1f);
        }
        Line(g, 0f, 3f, g.W, 3f, "#8a505c", 2f, 1f);
        Rect(g, 0f, g.H * 0.84f, g.W, g.H * 0.16f, "#5c3039", 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#38181f", 3f, 1f);
        Grain(g, new[] { "#824753", "#6a3843" }, 350, 0.10f, 5795);
        } },
        ["aAirMeetingHull"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#241f22");
        for (float x = 0f; x < g.W; x += 40f)
        {
        Line(g, x, 0f, x, g.H, "#1a1619", 2f, 1f);
        }
        for (float y = 0f; y < g.H; y += 34f)
        {
        Line(g, 0f, y, g.W, y, "#1a1619", 1f, 1f);
        }
        for (float x = 12f; x < g.W; x += 40f)
        {
            g.FillEllipse(x, 9f, 1.4f, 1.4f, C("#312a2e"), 1f);
            g.FillEllipse(x, g.H - 9f, 1.4f, 1.4f, C("#312a2e"), 1f);
        }
        Grain(g, new[] { "#2c262a", "#1c181b" }, 350, 0.10f, 686);
        } },
        ["aAirMeetingTable"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#8b7c63");
        for (float y = 4f; y < g.H; y += 10f)
        {
        Line(g, 0f, y, g.W, y, "#7d6f57", 1f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#9a8b71", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#6d604b", 2f, 1f);
        Grain(g, new[] { "#94856b", "#7c6e57" }, 400, 0.10f, 5977);
        } },
        ["aAirMeetingBench"] = new Spec { Unit = 0.6f, Draw = g => {
        Fill(g, "#215588");
        Line(g, 0f, 2f, g.W, 2f, "#2f6da6", 2f, 0.7f);
        Line(g, 0f, g.H - 3f, g.W, g.H - 3f, "#194470", 2f, 0.7f);
        Grain(g, new[] { "#2a6096", "#1c4b79" }, 300, 0.10f, 5850);
        } },
        ["aAirMeetingThreshold"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#4f233b");
        for (float x = 4f; x < g.W; x += 9f)
        {
        Line(g, x, 0f, x, g.H, "#411d30", 1f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#5e2c47", 2f, 1f);
        Grain(g, new[] { "#5a2a44", "#431f33" }, 300, 0.10f, 1890);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_airship_records.js
        // ---------------------------------------------------------------
        ["aAirRecordsFloor"] = new Spec { Unit = 1.1f, Detail = 1, Draw = g => {
        Fill(g, "#3a7287");
        // Plattenfugen, zwei pro Einheit
        for (float x = 0f; x <= g.W; x += g.W / 2f)
        {
        Line(g, x, 0f, x, g.H, "#2c5a70", 2f, 1f);
        }
        for (float y = 0f; y <= g.H; y += g.H / 2f)
        {
        Line(g, 0f, y, g.W, y, "#2c5a70", 2f, 1f);
        }
        // der hellere Plattenkern, wie im Original leicht gefleckt
        Rect(g, g.W * 0.06f, g.H * 0.08f, g.W * 0.40f, g.H * 0.38f, "#458098", 0.25f);
        Rect(g, g.W * 0.56f, g.H * 0.54f, g.W * 0.38f, g.H * 0.38f, "#458098", 0.25f);
        Grain(g, new[] { "#40798f", "#315f75" }, 450, 0.10f, 3849);
        } },
        ["aAirRecordsSill"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#a28365");
        for (float x = 6f; x < g.W; x += 14f)
        {
        Line(g, x, 0f, x, g.H, "#8d7054", 1f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#7d6349", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#71593f", 2f, 1f);
        Grain(g, new[] { "#ac8c6c", "#96795c" }, 300, 0.10f, 9397);
        } },
        ["aAirRecordsWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#472023");
        for (float x = 0f; x < g.W; x += 34f)
        {
        Line(g, x, 0f, x, g.H, "#38191c", 2f, 1f);
        }
        Line(g, 0f, 3f, g.W, 3f, "#6b3036", 2f, 1f);
        // Perle unter der Kappe
        // Sockelband
        Rect(g, 0f, g.H * 0.78f, g.W, g.H * 0.22f, "#38191c", 1f);
        Line(g, 0f, g.H * 0.78f, g.W, g.H * 0.78f, "#2b1315", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#200d0f", 3f, 1f);
        // dunkle Basislinie
        Grain(g, new[] { "#4e2427", "#3d1b1e" }, 400, 0.10f, 8466);
        } },
        ["aAirRecordsHull"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#431d20");
        for (float x = 0f; x < g.W; x += 44f)
        {
        Line(g, x, 0f, x, g.H, "#341517", 2f, 1f);
        }
        for (float y = 0f; y < g.H; y += 30f)
        {
        Line(g, 0f, y, g.W, y, "#341517", 1f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#57282c", 1f, 1f);
        for (float x = 10f; x < g.W; x += 44f)
        {
            g.FillEllipse(x, 8f, 1.4f, 1.4f, C("#301213"), 1f);
            g.FillEllipse(x, g.H - 8f, 1.4f, 1.4f, C("#301213"), 1f);
        }
        Grain(g, new[] { "#4a2124", "#391719" }, 350, 0.10f, 3559);
        } },
        ["aAirRecordsDesk"] = new Spec { Unit = 0.85f, Detail = 1, Draw = g => {
        Fill(g, "#a58467");
        for (float y = 4f; y < g.H; y += 11f)
        {
        Line(g, 0f, y, g.W, y, "#93744f", 1f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#bd9a72", 2f, 0.5f);
        // Lichtkante
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#7c6244", 2f, 0.5f);
        // Schattenkante
        Grain(g, new[] { "#ad8c6c", "#997a58" }, 350, 0.09f, 199);
        } },
        ["aAirRecordsTable"] = new Spec { Unit = 0.7f, Draw = g => {
        Fill(g, "#a98283");
        Line(g, 0f, 1f, g.W, 1f, "#c09a99", 2f, 0.4f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#84636a", 2f, 0.4f);
        Grain(g, new[] { "#b28a88", "#9c7679" }, 250, 0.09f, 7071);
        } },
        ["aAirRecordsShelf"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   { while (x < w - 3)
        //   g.fillRect(x, y0 + 2, bw, bh - 6)
        //   x += bw + 2
        Fill(g, "#a25259");
        Grain(g, new[] { "#a25259" }, 400, 0.06f, 3585);
        } },
        ["aAirRecordsTrim"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#1b0d0d");
        for (float y = 3f; y < g.H; y += 9f)
        {
        Line(g, 0f, y, g.W, y, "#241010", 1f, 1f);
        }
        Grain(g, new[] { "#221010", "#150909" }, 220, 0.08f, 5958);
        } },
        ["aAirRecordsSilver"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        Fill(g, "#96a0a8");
        for (float y = 2f; y < g.H; y += 6f)
        {
        Line(g, 0f, y, g.W, y, "#8a939b", 1f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#aab3ba", 1f, 0.7f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#767f87", 2f, 0.7f);
        // Schraubpunkte an den Ecken
        g.FillEllipse(4f, 4f, 1.3f, 1.3f, C("#6e777f"), 1f);
        g.FillEllipse(g.W - 4f, 4f, 1.3f, 1.3f, C("#6e777f"), 1f);
        Grain(g, new[] { "#9da7af", "#8c959d" }, 280, 0.09f, 4124);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_airship_security.js
        // ---------------------------------------------------------------
        ["aAirSecurityFloor"] = new Spec { Unit = 1.15f, Detail = 1, Draw = g => {
        Fill(g, "#86929c");
        for (float x = 0f; x < g.W; x += 32f)
        {
        Line(g, x, 0f, x, g.H, "#79858f", 1f, 1f);
        }
        for (float y = 0f; y < g.H; y += 32f)
        {
        Line(g, 0f, y, g.W, y, "#79858f", 1f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#9aa6b0", 1f, 1f);
        Grain(g, new[] { "#8f9ba5", "#7d8993" }, 450, 0.10f, 6713);
        } },
        ["aAirSecurityCorridor"] = new Spec { Unit = 1.15f, Detail = 1, Draw = g => {
        Fill(g, "#3d4a4b");
        for (float y = 6f; y < g.H; y += 18f)
        {
        Line(g, 0f, y, g.W, y, "#364344", 2f, 1f);
        }
        for (float x = 8f; x < g.W; x += 26f)
        {
            Line(g, x, 2f, x + 7f, g.H - 2f, "#465354", 1f, 0.45f);
            // schraege Riffel
        }
        Line(g, 0f, 2f, g.W, 2f, "#4d5a5b", 1f, 1f);
        Grain(g, new[] { "#47545a", "#333f40" }, 500, 0.11f, 3149);
        } },
        ["aAirSecurityWood"] = new Spec { Unit = 0.92f, Detail = 1, Draw = g => {
        Fill(g, "#c2a184");
        float plank = g.H / 4f;
        for (float i = 0f; i < 4f; i += 1f)
        {
            float y = i * plank;
            if (i % 2f == 1f)
            {
                Rect(g, 0f, y, g.W, plank, "#b5946f", 1f);
            }
            Rect(g, 0f, y, g.W, 2f, "#8f7156", 1f);
            // Fuge
            for (float x = (i * 17f) % 30f; x < g.W; x += 30f)
            {
        Line(g, x, y + 4f, x + 14f, y + plank - 4f, "#a98a67", 1f, 1f);
            }
        }
        Grain(g, new[] { "#cbab8b", "#b08d68" }, 420, 0.10f, 6028);
        } },
        ["aAirSecurityWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#97706e");
        for (float x = 0f; x < g.W; x += 38f)
        {
        Line(g, x, 0f, x, g.H, "#85605e", 2f, 1f);
        }
        Line(g, 0f, 3f, g.W, 3f, "#a03a3c", 2f, 1f);
        // rote Perle unter der Kappe
        // Sockelstreifen
        Rect(g, 0f, g.H * 0.80f, g.W, g.H * 0.20f, "#7a5451", 1f);
        Line(g, 0f, g.H * 0.80f, g.W, g.H * 0.80f, "#654442", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#4e3231", 3f, 1f);
        Grain(g, new[] { "#a17976", "#8a6462" }, 400, 0.10f, 2122);
        } },
        ["aAirSecurityHull"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#451f22");
        for (float x = 0f; x < g.W; x += 44f)
        {
        Line(g, x, 0f, x, g.H, "#38181b", 2f, 1f);
        }
        for (float y = 0f; y < g.H; y += 34f)
        {
        Line(g, 0f, y, g.W, y, "#38181b", 1f, 1f);
        }
        for (float x = 10f; x < g.W; x += 44f)
        {
            g.FillEllipse(x, 8f, 1.3f, 1.3f, C("#55292c"), 1f);
            g.FillEllipse(x, g.H - 8f, 1.3f, 1.3f, C("#55292c"), 1f);
        }
        Grain(g, new[] { "#4d2427", "#3b1a1d" }, 350, 0.10f, 3680);
        } },
        ["aAirSecurityHullRed"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#93383e");
        for (float x = 0f; x < g.W; x += 56f)
        {
        Line(g, x, 0f, x, g.H, "#7e2d34", 2f, 1f);
        }
        Line(g, 0f, 4f, g.W, 4f, "#a4454b", 1f, 1f);
        Line(g, 0f, g.H - 3f, g.W, g.H - 3f, "#6e252b", 2f, 1f);
        Grain(g, new[] { "#9c3d43", "#873137" }, 380, 0.10f, 2263);
        } },
        ["aAirSecurityMetal"] = new Spec { Unit = 0.85f, Detail = 1, Draw = g => {
        Fill(g, "#26282a");
        for (float y = 3f; y < g.H; y += 7f)
        {
        Line(g, 0f, y, g.W, y, "#2e3134", 1f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#34373a", 1f, 1f);
        Grain(g, new[] { "#2c2f32", "#1f2124" }, 400, 0.10f, 8334);
        } },
        ["aAirSecurityRail"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#8d979c");
        for (float y = 4f; y < g.H; y += 12f)
        {
        Line(g, 0f, y, g.W, y, "#7f8990", 1f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#a5afb4", 1f, 1f);
        Grain(g, new[] { "#98a2a7", "#7e888e" }, 300, 0.10f, 7559);
        } },
        ["aAirSecurityCeil"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#6a7075");
        for (float x = 0f; x < g.W; x += 40f)
        {
        Line(g, x, 0f, x, g.H, "#5f656a", 1f, 1f);
        }
        for (float y = 0f; y < g.H; y += 40f)
        {
        Line(g, 0f, y, g.W, y, "#5f656a", 1f, 1f);
        }
        Grain(g, new[] { "#71777c", "#60666b" }, 300, 0.08f, 1546);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_airship_showers.js
        // ---------------------------------------------------------------
        ["aAirShowersFloorGrey"] = new Spec { Unit = 0.88f, Detail = 1, Draw = g => {
        Fill(g, "#42464a");
        // Plattenmitte (Messung #575a57 - Stufe)
        Rect(g, 2f, 2f, g.W / 2f - 3f, g.H / 2f - 3f, "#484d51", 1f);
        Rect(g, g.W / 2f + 1f, g.H / 2f + 1f, g.W / 2f - 3f, g.H / 2f - 3f, "#484d51", 1f);
        // jede zweite Platte abgesetzt
        Rect(g, g.W / 2f + 1f, 2f, g.W / 2f - 3f, g.H / 2f - 3f, "#3a3e42", 1f);
        Rect(g, 2f, g.H / 2f + 1f, g.W / 2f - 3f, g.H / 2f - 3f, "#3a3e42", 1f);
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#303437", 2f, 0.85f);
        // Fugenkreuz in der Einheitenmitte
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#303437", 2f, 0.85f);
        Line(g, 0f, 1f, g.W, 1f, "#2b2f32", 1f, 1f);
        Line(g, 1f, 0f, 1f, g.H, "#2b2f32", 1f, 1f);
        Grain(g, new[] { "#4c5155", "#383c40" }, 450, 0.10f, 2445);
        } },
        ["aAirShowersTilePale"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        Fill(g, "#adbfc4");
        // Plattenmitte heller
        Rect(g, 2f, 2f, g.W - 4f, g.H / 2f - 3f, "#b8cacd", 1f);
        Line(g, 0f, g.H * 0.30f, g.W, g.H * 0.30f, "#5f8b96", 3f, 0.9f);
        // Teal-Rinne (Messung #649ba6)
        Line(g, 0f, g.H * 0.72f, g.W, g.H * 0.72f, "#587f89", 2f, 0.9f);
        // Ablaufgitter in der Rinne
        for (float x = 6f; x < g.W; x += 14f)
        {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   g.fillRect(x, h * 0.30 - 2, 6, 4)
        Fill(g, "#8a8a8a");
        Grain(g, new[] { "#8a8a8a" }, 400, 0.06f, 3593);
        }
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#93a7ab", 2f, 1f);
        // Fugenkreuz
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#93a7ab", 2f, 1f);
        Grain(g, new[] { "#c3d3d6", "#9db1b6" }, 400, 0.10f, 3593);
        } },
        ["aAirShowersWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#878f88");
        for (float x = 0f; x < g.W; x += 30f)
        {
        Line(g, x, 0f, x, g.H, "#767e77", 2f, 1f);
        }
        // Paneelstöße
        Line(g, 0f, 3f, g.W, 3f, "#98a099", 2f, 1f);
        // helle Perle unter der Kappe
        // dunkler Sockelstreifen
        Rect(g, 0f, g.H * 0.80f, g.W, g.H * 0.20f, "#6d746e", 1f);
        Line(g, 0f, g.H * 0.80f, g.W, g.H * 0.80f, "#5a615c", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#474d49", 3f, 1f);
        // Basislinie
        Grain(g, new[] { "#90988f", "#787f79" }, 380, 0.10f, 1138);
        } },
        ["aAirShowersHull"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#421c1f");
        for (float x = 0f; x < g.W; x += 40f)
        {
        Line(g, x, 0f, x, g.H, "#341517", 2f, 1f);
        }
        for (float y = 0f; y < g.H; y += 26f)
        {
        Line(g, 0f, y, g.W, y, "#341517", 1f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#55262a", 1f, 1f);
        for (float x = 12f; x < g.W; x += 40f)
        {
            g.FillEllipse(x, 7f, 1.3f, 1.3f, C("#2c1113"), 1f);
            g.FillEllipse(x, g.H - 7f, 1.3f, 1.3f, C("#2c1113"), 1f);
        }
        Grain(g, new[] { "#4b2124", "#361618" }, 320, 0.10f, 2876);
        } },
        ["aAirShowersWood"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#53443c");
        for (float y = 4f; y < g.H; y += 10f)
        {
        Line(g, 0f, y, g.W, y, "#463a33", 2f, 1f);
        }
        // Lagen
        for (float i = 0f; i < g.W; i += 22f)
        {
            // versetzte Stoßfugen
            Line(g, i + ((i / 22f) % 2f) * 11f, 4f, i + ((i / 22f) % 2f) * 11f, 14f, "#463a33", 1f, 1f);
        }
        Grain(g, new[] { "#5d4d43", "#493b34" }, 420, 0.10f, 7553);
        } },
        ["aAirShowersWoodDark"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#362b26");
        for (float y = 3f; y < g.H; y += 9f)
        {
        Line(g, 0f, y, g.W, y, "#2c231f", 1f, 1f);
        }
        Grain(g, new[] { "#3d312b", "#2e2521" }, 300, 0.10f, 5879);
        } },
        ["aAirShowersTrim"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#131110");
        for (float x = 0f; x < g.W; x += 24f)
        {
        Line(g, x, 0f, x, g.H, "#0c0b0a", 1f, 1f);
        }
        Grain(g, new[] { "#1a1714", "#0e0d0c" }, 260, 0.10f, 2835);
        } },
        ["aAirShowersLocker"] = new Spec { Unit = 0.62f, Draw = g => {
        Fill(g, "#434f58");
        // Türfeld abgesetzt
        Rect(g, g.W * 0.10f, g.H * 0.06f, g.W * 0.80f, g.H * 0.88f, "#39434c", 1f);
        // Lüftungsschlitze oben
        for (float i = 0f; i < 3f; i += 1f)
        {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   g.fillRect(w * 0.22, h * (0.14 + i * 0.05), w * 0.56, 2)
        Fill(g, "#8a8a8a");
        Grain(g, new[] { "#8a8a8a" }, 400, 0.06f, 4388);
        }
        // Griffleiste
        Rect(g, g.W * 0.78f, g.H * 0.42f, 2f, g.H * 0.16f, "#8b999f", 1f);
        Line(g, g.W * 0.10f, g.H * 0.06f, g.W * 0.10f+g.W * 0.80f, g.H * 0.06f, "#2b3339", 2f, 1f);
        Line(g, g.W * 0.10f, g.H * 0.06f+g.H * 0.88f, g.W * 0.10f+g.W * 0.80f, g.H * 0.06f+g.H * 0.88f, "#2b3339", 2f, 1f);
        Line(g, g.W * 0.10f, g.H * 0.06f, g.W * 0.10f, g.H * 0.06f+g.H * 0.88f, "#2b3339", 2f, 1f);
        Line(g, g.W * 0.10f+g.W * 0.80f, g.H * 0.06f, g.W * 0.10f+g.W * 0.80f, g.H * 0.06f+g.H * 0.88f, "#2b3339", 2f, 1f);
        Grain(g, new[] { "#4b5862", "#3a454e" }, 280, 0.10f, 4388);
        } },
        ["aAirShowersTowel"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#8d6f48");
        for (float y = 2f; y < g.H; y += 5f)
        {
        Line(g, 0f, y, g.W, y, "#7a5e3d", 1f, 1f);
        }
        // Webkante
        Line(g, 0f, g.H * 0.18f, g.W, g.H * 0.18f, "#6e5638", 2f, 1f);
        // Bordüre
        Grain(g, new[] { "#9a7a52", "#7d6240", "#a5855c" }, 500, 0.14f, 1687);
        } },
        ["aAirShowersGlass"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   g.bezierCurveTo(x + 3, h * 0.3, x - 3, h * 0.6, x + 1, h)
        Fill(g, "#bcd8dd");
        Grain(g, new[] { "#bcd8dd" }, 400, 0.06f, 1756);
        } },
        ["aAirShowersCeil"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#3a4144");
        for (float x = 0f; x <= g.W; x += g.W / 2f)
        {
        Line(g, x, 0f, x, g.H, "#31383b", 2f, 1f);
        }
        for (float y = 0f; y <= g.H; y += g.H / 2f)
        {
        Line(g, 0f, y, g.W, y, "#31383b", 2f, 1f);
        }
        Grain(g, new[] { "#41494c", "#343b3e" }, 240, 0.08f, 8414);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_airship_vaultroom.js
        // ---------------------------------------------------------------
        ["aAirVaultFloor"] = new Spec { Unit = 1.7f, Draw = g => {
        Fill(g, "#3f678f");
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#396084", 2f, 0.35f);
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#396084", 2f, 0.35f);
        Line(g, 0f, 2f, g.W, 2f, "#47719c", 1f, 0.25f);
        Grain(g, new[] { "#47719c", "#385e83", "#43698f" }, 420, 0.10f, 1634);
        } },
        ["aAirVaultTunnel"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        Fill(g, "#66797b");
        for (float y = 6f; y < g.H; y += 10f)
        {
        Line(g, 0f, y, g.W, y, "#5a6c6e", 1f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#4c5c5e", 2f, 1f);
        Grain(g, new[] { "#708486", "#5c6f71" }, 380, 0.10f, 4729);
        } },
        ["aAirVaultWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#575e68");
        for (float x = 6f; x < g.W; x += 34f)
        {
        Line(g, x, 0f, x, g.H, "#495059", 2f, 1f);
        }
        Line(g, 0f, 4f, g.W, 4f, "#6b737e", 2f, 0.5f);
        // helle Perle unter der Kappe
        // dunkler Sockel
        Rect(g, 0f, g.H * 0.8f, g.W, g.H * 0.2f, "#454c55", 1f);
        Line(g, 0f, g.H * 0.8f, g.W, g.H * 0.8f, "#3a4048", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#2b3037", 3f, 1f);
        // dunkle Basislinie
        Grain(g, new[] { "#5f6670", "#4d545d" }, 380, 0.10f, 6881);
        } },
        ["aAirVaultHull"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#1b1e24");
        for (float x = 0f; x < g.W; x += 44f)
        {
        Line(g, x, 0f, x, g.H, "#14161b", 2f, 1f);
        }
        for (float y = 0f; y < g.H; y += 30f)
        {
        Line(g, 0f, y, g.W, y, "#14161b", 1f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#242830", 1f, 1f);
        for (float x = 10f; x < g.W; x += 44f)
        {
            g.FillEllipse(x, 8f, 1.3f, 1.3f, C("#101216"), 1f);
            g.FillEllipse(x, g.H - 8f, 1.3f, 1.3f, C("#101216"), 1f);
        }
        Grain(g, new[] { "#20242b", "#15171d" }, 320, 0.10f, 1909);
        } },
        ["aAirVaultCeil"] = new Spec { Unit = 1.1f, Detail = 1, Draw = g => {
        Fill(g, "#2c313b");
        for (float x = 0f; x <= g.W; x += g.W / 2f)
        {
        Line(g, x, 0f, x, g.H, "#232830", 2f, 1f);
        }
        for (float y = 0f; y <= g.H; y += g.H / 2f)
        {
        Line(g, 0f, y, g.W, y, "#232830", 2f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#343a45", 1f, 1f);
        Grain(g, new[] { "#313742", "#262b34" }, 300, 0.10f, 8063);
        } },
        ["aAirVaultWood"] = new Spec { Unit = 0.85f, Detail = 1, Draw = g => {
        Fill(g, "#96755c");
        for (float y = 0f; y < g.H; y += g.H / 3f)
        {
        Line(g, 0f, y, g.W, y, "#7f624c", 2f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#a5825f", 1f, 0.35f);
        Grain(g, new[] { "#a17f60", "#85674f" }, 420, 0.12f, 5031);
        } },
        ["aAirVaultTrim"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#2c3a6e");
        Line(g, 0f, 2f, g.W, 2f, "#3d4f8a", 2f, 0.5f);
        Line(g, 0f, g.H - 1f, g.W, g.H - 1f, "#1e2949", 2f, 1f);
        Grain(g, new[] { "#33437c", "#25315c" }, 260, 0.10f, 2433);
        } },
        ["aAirVaultGold"] = new Spec { Unit = 0.6f, Draw = g => {
        Fill(g, "#d2a524");
        Grain(g, new[] { "#e6bc33", "#b8901f" }, 260, 0.5f, 8139);
        for (float i = 0f; i < 14f; i += 1f)
        {
            float x = (i * 37f) % g.W, y = (i * 53f) % g.H, r = 2f + (i % 3f);
            g.FillEllipse(x, y, r, r, C(i % 2 != 0f ? "#e9c23a" : "#c2941f"), 0.35f);
        }
        } },
        ["aAirVaultRuby"] = new Spec { Unit = 0.4f, Draw = g => {
        Fill(g, "#7e2c4e");
        g.FillQuad(g.W * 0.5f, 0f, g.W, g.H * 0.45f, g.W * 0.5f, g.H * 0.6f, g.W * 0.5f, g.H * 0.6f, C("#a83a66"), 1f);
        g.FillQuad(g.W * 0.5f, 0f, g.W * 0.5f, g.H * 0.6f, 0f, g.H * 0.45f, 0f, g.H * 0.45f, C("#c8547f"), 1f);
        g.FillQuad(0f, g.H * 0.45f, g.W * 0.5f, g.H * 0.6f, g.W * 0.5f, g.H, g.W * 0.5f, g.H, C("#8e3357"), 1f);
        Line(g, g.W * 0.5f, 0f, g.W * 0.5f, g.H, "#5e2138", 1f, 1f);
        Line(g, 0f, g.H * 0.45f, g.W, g.H * 0.45f, "#5e2138", 1f, 1f);
        } },
        ["aAirVaultPedestal"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#8b86aa");
        for (float y = 0f; y < g.H; y += g.H / 3f)
        {
        Line(g, 0f, y, g.W, y, "#767193", 2f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#9c97b8", 1f, 0.4f);
        Grain(g, new[] { "#948fb0", "#7d7899" }, 260, 0.10f, 6149);
        } },
        ["aAirVaultStand"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#8d99a0");
        Line(g, 0f, 2f, g.W, 2f, "#a3adb3", 2f, 0.45f);
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#79848b", 1f, 0.45f);
        Line(g, 0f, g.H - 1f, g.W, g.H - 1f, "#6a757c", 2f, 1f);
        Grain(g, new[] { "#96a1a8", "#818d94" }, 240, 0.10f, 2383);
        } },
        ["aAirVaultFrameGold"] = new Spec { Unit = 0.4f, Draw = g => {
        Fill(g, "#987f2c");
        Line(g, 0f, 1f, g.W, 1f, "#b09538", 2f, 0.5f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#7a6524", 2f, 0.5f);
        Grain(g, new[] { "#a58a32", "#877028" }, 200, 0.12f, 4412);
        } },
        ["aAirVaultDrive"] = new Spec { Unit = 0.2f, Draw = g => {
        Fill(g, "#173a4a");
        Rect(g, g.W * 0.2f, g.H * 0.35f, g.W * 0.6f, g.H * 0.18f, "#4fc3e8", 1f);
        Rect(g, g.W * 0.35f, g.H * 0.6f, g.W * 0.3f, g.H * 0.15f, "#0d2530", 1f);
        } },
        ["aAirVaultDummy"] = new Spec { Unit = 0.45f, Draw = g => {
        Fill(g, "#bdb29c");
        Line(g, 0f, 2f, g.W, 2f, "#cfc4ae", 1f, 0.4f);
        Grain(g, new[] { "#c8bda7", "#aca190" }, 220, 0.10f, 3212);
        } },
        ["aAirVaultJacket"] = new Spec { Unit = 0.45f, Draw = g => {
        Fill(g, "#544b83");
        for (float y = 3f; y < g.H; y += 6f)
        {
        Line(g, 0f, y, g.W, y, "#463e6e", 1f, 1f);
        }
        Grain(g, new[] { "#5d5490", "#4a4274" }, 240, 0.12f, 7084);
        } },
        ["aAirVaultShako"] = new Spec { Unit = 0.4f, Draw = g => {
        Fill(g, "#962424");
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#7a1c1c", 2f, 0.45f);
        Line(g, 0f, 2f, g.W, 2f, "#ab3030", 1f, 0.45f);
        Grain(g, new[] { "#a52a2a", "#861f1f" }, 220, 0.10f, 8051);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_airship_ventilation.js
        // ---------------------------------------------------------------
        ["aAirVentFloor"] = new Spec { Unit = 0.95f, Detail = 1, Draw = g => {
        Fill(g, "#4d3338");
        // Plattenfeld: grosse Kacheln mit abgesetzter Mitte, Fugenkreuz in der Einheitenmitte
        Rect(g, 3f, 3f, g.W / 2f - 5f, g.H / 2f - 5f, "#573a40", 1f);
        Rect(g, g.W / 2f + 2f, g.H / 2f + 2f, g.W / 2f - 5f, g.H / 2f - 5f, "#573a40", 1f);
        Rect(g, g.W / 2f + 2f, 3f, g.W / 2f - 5f, g.H / 2f - 5f, "#452c31", 1f);
        Rect(g, 3f, g.H / 2f + 2f, g.W / 2f - 5f, g.H / 2f - 5f, "#452c31", 1f);
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#38232a", 2f, 0.85f);
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#38232a", 2f, 0.85f);
        Line(g, 0f, 1f, g.W, 1f, "#33202a", 1f, 1f);
        Line(g, 1f, 0f, 1f, g.H, "#33202a", 1f, 1f);
        Grain(g, new[] { "#61414a", "#54363e", "#3e2830" }, 550, 0.12f, 1448);
        } },
        ["aAirVentApron"] = new Spec { Unit = 0.7f, Draw = g => {
        Fill(g, "#26181b");
        for (float x = 0f; x < g.W; x += 10f)
        {
        Line(g, x, 0f, x + 4f, g.H, "#1d1215", 1f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#312026", 2f, 1f);
        Grain(g, new[] { "#302026", "#1c1114" }, 450, 0.12f, 4114);
        } },
        ["aAirVentWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#421d1e");
        for (float x = 0f; x < g.W; x += 34f)
        {
        Line(g, x, 0f, x, g.H, "#331618", 2f, 1f);
        }
        Line(g, 0f, 3f, g.W, 3f, "#54282a", 2f, 1f);
        // helle Perle unter der Kappe
        // Sockelband
        Rect(g, 0f, g.H * 0.78f, g.W, g.H * 0.22f, "#341517", 1f);
        Line(g, 0f, g.H * 0.78f, g.W, g.H * 0.78f, "#280f11", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#1c0a0c", 3f, 1f);
        // Basislinie
        // Nieten je Paneelstoss
        for (float x = 17f; x < g.W; x += 34f)
        {
            g.FillEllipse(x, 9f, 1.3f, 1.3f, C("#58292b"), 1f);
            g.FillEllipse(x, g.H * 0.72f, 1.3f, 1.3f, C("#58292b"), 1f);
        }
        Grain(g, new[] { "#4b2224", "#391719" }, 400, 0.10f, 7832);
        } },
        ["aAirVentHull"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#15161a");
        for (float x = 0f; x < g.W; x += 44f)
        {
        Line(g, x, 0f, x, g.H, "#101116", 2f, 1f);
        }
        for (float y = 0f; y < g.H; y += 30f)
        {
        Line(g, 0f, y, g.W, y, "#101116", 1f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#22242c", 1f, 1f);
        for (float x = 10f; x < g.W; x += 44f)
        {
            g.FillEllipse(x, 8f, 1.4f, 1.4f, C("#0d0e13"), 1f);
            g.FillEllipse(x, g.H - 8f, 1.4f, 1.4f, C("#0d0e13"), 1f);
        }
        Grain(g, new[] { "#1d2027", "#0f1015" }, 350, 0.10f, 538);
        } },
        ["aAirVentSteel"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#57686f");
        for (float x = 6f; x < g.W; x += 26f)
        {
            Line(g, x, 4f, x, g.H - 4f, "#48575e", 3f, 0.45f);
            // Rippe
            Line(g, x + 5f, 4f, x + 5f, g.H - 4f, "#657880", 1f, 0.45f);
            // Lichtkante
        }
        Line(g, 0f, 2f, g.W, 2f, "#6b7e86", 2f, 1f);
        // Oberkante
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#39454b", 3f, 1f);
        // Fusslinie
        // Bolzenreihe oben und unten
        for (float x = 13f; x < g.W; x += 26f)
        {
            g.FillEllipse(x, 10f, 1.5f, 1.5f, C("#435158"), 1f);
            g.FillEllipse(x, g.H - 10f, 1.5f, 1.5f, C("#435158"), 1f);
        }
        Grain(g, new[] { "#60737b", "#4c5b62" }, 420, 0.10f, 3063);
        } },
        ["aAirVentSteelTop"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#627476");
        // Lamellenpaerchen
        float step = MathF.Max(8f, MathF.Floor(g.H / 6f));
        for (float y = step; y < g.H - step / 2f; y += step)
        {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   g.fillRect(4, y, w - 8, Math.max(2, step / 3))
        Fill(g, "#8a8a8a");
        Grain(g, new[] { "#8a8a8a" }, 400, 0.06f, 752);
        }
        Line(g, 0f, 1f, g.W, 1f, "#78898b", 2f, 0.7f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#48585c", 2f, 0.7f);
        Grain(g, new[] { "#6d8082", "#56676b" }, 380, 0.10f, 752);
        } },
        ["aAirVentGrate"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#39464c");
        // Roststäbe quer über die Bahn, mit Lichtkante oben
        float step = MathF.Max(6f, MathF.Floor(g.H / 8f));
        for (float y = step / 2f; y < g.H; y += step)
        {
            Rect(g, 0f, y, g.W, MathF.Max(2f, MathF.Floor(step / 3f)), "#2c373d", 1f);
            Rect(g, 0f, y - 1f, g.W, 1f, "#4b5b62", 1f);
        }
        // Randprofil
        Line(g, 0f, 1f, g.W, 1f, "#54666e", 2f, 0.8f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#263036", 2f, 0.8f);
        // Warnstreifen: versetzte Ocker-Dashen entlang aller vier Ränder
        for (float x = 2f; x < g.W - 2f; x += 12f)
        {
            Rect(g, x, 2f, 7f, 3f, "#a08144", 1f);
            Rect(g, x + 6f, g.H - 5f, 7f, 3f, "#a08144", 1f);
        }
        for (float y = 14f; y < g.H - 10f; y += 12f)
        {
            Rect(g, 2f, y, 3f, 7f, "#a08144", 1f);
            Rect(g, g.W - 5f, y + 6f, 3f, 7f, "#a08144", 1f);
        }
        Grain(g, new[] { "#455459", "#313d43", "#96793f" }, 420, 0.12f, 803);
        } },
        ["aAirVentFan"] = new Spec { Unit = 0.6f, Draw = g => {
        Fill(g, "#2e3a3b");
        g.StrokeEllipse(g.W / 2f, g.H / 2f, MathF.Min(g.W, g.H) * 0.36f, MathF.Min(g.W, g.H) * 0.36f, 2f, C("#242e2f"), 1f);
        for (float i = 0f; i < 3f; i += 1f)
        {
            // drei Laufradfluegel
            float a = (i * MathF.PI * 2f) / 3f + 0.5f;
            Line(g, g.W / 2f, g.H / 2f, g.W / 2f + MathF.Cos(a) * MathF.Min(g.W, g.H) * 0.34f, g.H / 2f + MathF.Sin(a) * MathF.Min(g.W, g.H) * 0.34f, "#1b2324", 3f, 1f);
        }
        g.FillEllipse(g.W / 2f, g.H / 2f, MathF.Min(g.W, g.H) * 0.08f, MathF.Min(g.W, g.H) * 0.08f, C("#39494a"), 1f);
        Grain(g, new[] { "#37454a", "#26302f" }, 260, 0.10f, 1197);
        } },
        ["aAirVentPipe"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#49545c");
        Line(g, 0f, g.H * 0.3f, g.W, g.H * 0.3f, "#5a676f", 2f, 0.6f);
        // Längslicht
        Line(g, 0f, g.H * 0.72f, g.W, g.H * 0.72f, "#39434a", 2f, 0.6f);
        // Längsschatten
        for (float x = 8f; x < g.W; x += 40f)
        {
        Line(g, x, 0f, x, g.H, "#39434a", 2f, 1f);
        }
        // Schellen
        Grain(g, new[] { "#556270", "#3d4750" }, 300, 0.10f, 1720);
        } },
        ["aAirVentRail"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#576872");
        Line(g, 0f, 2f, g.W, 2f, "#6d8290", 2f, 0.5f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#414f58", 2f, 0.5f);
        Grain(g, new[] { "#647884", "#485660" }, 300, 0.12f, 6139);
        } },
        ["aAirVentTrim"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#1c2124");
        Line(g, 0f, 1f, g.W, 1f, "#2b3236", 1f, 0.5f);
        Grain(g, new[] { "#242b2f", "#141920" }, 260, 0.10f, 8767);
        } },
        ["aAirVentCeil"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#1e2426");
        for (float x = 0f; x < g.W; x += 24f)
        {
        Line(g, x, 0f, x, g.H, "#171c1e", 2f, 1f);
        }
        for (float y = 0f; y < g.H; y += 18f)
        {
        Line(g, 0f, y, g.W, y, "#171c1e", 1f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#2a3234", 1f, 1f);
        Grain(g, new[] { "#252d2f", "#161b1d" }, 320, 0.10f, 3272);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_airship_viewingdeck.js
        // ---------------------------------------------------------------
        ["aAirViewingFloor"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#6e6b5b");
        // grosse, versetzte Plattenfugen - auf dem Foto eben noch als Tonwechsel lesbar
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#5d5b4d", 2f, 0.35f);
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#5d5b4d", 2f, 0.35f);
        Rect(g, 3f, 3f, g.W / 2f - 6f, g.H / 2f - 6f, "#78755f", 0.2f);
        Grain(g, new[] { "#78755f", "#63604f", "#5a584a" }, 550, 0.12f, 7194);
        } },
        ["aAirViewingMat"] = new Spec { Unit = 0.6f, Draw = g => {
        Fill(g, "#575144");
        // grober Gewebe-Schlitz in beide Richtungen
        for (float x = 0f; x < g.W; x += 5f)
        {
        Line(g, x, 0f, x, g.H, "#494438", 1f, 1f);
        }
        for (float y = 0f; y < g.H; y += 5f)
        {
        Line(g, 0f, y, g.W, y, "#494438", 1f, 1f);
        }
        // der abgesetzte Rand als dunkle Einfassung
        Line(g, 1.5f, 1.5f, 1.5f+g.W - 3f, 1.5f, "#3d4a50", 3f, 1f);
        Line(g, 1.5f, 1.5f+g.H - 3f, 1.5f+g.W - 3f, 1.5f+g.H - 3f, "#3d4a50", 3f, 1f);
        Line(g, 1.5f, 1.5f, 1.5f, 1.5f+g.H - 3f, "#3d4a50", 3f, 1f);
        Line(g, 1.5f+g.W - 3f, 1.5f, 1.5f+g.W - 3f, 1.5f+g.H - 3f, "#3d4a50", 3f, 1f);
        Grain(g, new[] { "#635c49", "#4c4739" }, 400, 0.12f, 3301);
        } },
        ["aAirViewingApron"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#7b7869");
        for (float y = 4f; y < g.H; y += g.H / 3f)
        {
        Line(g, 0f, y, g.W, y, "#63614f", 2f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#8a8776", 1f, 1f);
        Grain(g, new[] { "#847f6e", "#6a675a" }, 400, 0.10f, 4713);
        } },
        ["aAirViewingSteel"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#343c41");
        for (float x = 0f; x < g.W; x += 20f)
        {
        Line(g, x, 0f, x, g.H, "#262c30", 1f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#434d53", 1f, 1f);
        Rect(g, 0f, g.H - 4f, g.W, 4f, "#2a3134", 1f);
        Grain(g, new[] { "#3b444a", "#2b3236" }, 350, 0.10f, 572);
        } },
        ["aAirViewingWoodDeck"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#b3937a");
        float rows = 4f;
        for (float i = 0f; i < rows; i += 1f)
        {
            float y0 = (i * g.H) / rows;
            // jede zweite Diele abgesetzt
            if (i % 2f == 0f)
            {
                Rect(g, 0f, y0, g.W, g.H / rows - 1f, "#a5866c", 1f);
            }
            Line(g, 0f, y0 + g.H / rows - 1f, g.W, y0 + g.H / rows - 1f, "#4e585a", 1f, 0.9f);
            // versetzte Laengsstoesse
            float jx = ((i * 0.37f) % 0.8f + 0.15f) * g.W;
            Line(g, jx, y0 + 1f, jx, y0 + g.H / rows - 2f, "#8a6d55", 1f, 0.7f);
        }
        // helle Maserung
        for (float x = 5f; x < g.W; x += 13f)
        {
            Line(g, x, 2f, x + 4f, g.H - 4f, "#c7a988", 1f, 0.25f);
        }
        Grain(g, new[] { "#bd9d82", "#a1816a" }, 500, 0.12f, 6333);
        } },
        ["aAirViewingWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#6c5b5e");
        // Rippen: dunkle Vertikalstege, dazwischen die Flaeche leicht hell
        for (float x = 0f; x < g.W; x += g.W / 2f)
        {
        Line(g, x, 0f, x, g.H, "#57484c", 3f, 1f);
        }
        Rect(g, g.W / 2f + 2f, 2f, g.W / 2f - 4f, g.H * 0.8f - 4f, "#786569", 0.25f);
        // helle Perle unter der Kappe
        Line(g, 0f, 3f, g.W, 3f, "#7d6a6e", 2f, 1f);
        // Sockelband (der helle gezeichnete Streifen) + dunkle Basislinie
        Rect(g, 0f, g.H * 0.82f, g.W, g.H * 0.12f, "#65757f", 1f);
        Line(g, 0f, g.H * 0.82f, g.W, g.H * 0.82f, "#4c5761", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#3a4148", 3f, 1f);
        Grain(g, new[] { "#746266", "#5f4f53" }, 400, 0.10f, 1395);
        } },
        ["aAirViewingHull"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#421d1f");
        // Paneele und eine dunkle Fuge
        for (float x = 0f; x < g.W; x += 48f)
        {
        Line(g, x, 0f, x, g.H, "#2e1416", 2f, 1f);
        }
        for (float y = 0f; y < g.H; y += 34f)
        {
        Line(g, 0f, y, g.W, y, "#2e1416", 1f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#542628", 1f, 1f);
        Grain(g, new[] { "#482122", "#381a1c" }, 350, 0.10f, 795);
        } },
        ["aAirViewingCeil"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#4e4245");
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#3e3538", 2f, 0.8f);
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#3e3538", 2f, 0.8f);
        Rect(g, 3f, 3f, g.W / 2f - 6f, g.H / 2f - 6f, "#443a3d", 1f);
        Grain(g, new[] { "#54484b", "#453b3e" }, 300, 0.10f, 1675);
        } },

        // ---------------------------------------------------------------
        // aus surfaces_airship_engine.js (Review-Runde 5: Kran + Kohle am Ostende)

        /// Der Kran: Blech mit Nietreihen, Gelb eine Stufe dunkler (#e3b431 gemessen).
        ["aAirEngineCrane"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#c49a22");
        Line(g, 0f, g.H * 0.5f, g.W, g.H * 0.5f, "#9a7616", 2f, 1f);
        for (float x = 6f; x < g.W; x += 14f)
        {
        Rect(g, x, 5f, 3f, 3f, "#8f6d12", 1f);
        Rect(g, x, g.H - 8f, 3f, 3f, "#8f6d12", 1f);
        }
        Grain(g, new[] { "#d1a72c", "#b08a1c" }, 300, 0.10f, 3311);
        } },

        /// Kohle: die schwarzen Atlas-Massen am Ostende (#262b34 / #1a1c22), matt und koernig.
        ["aAirEngineCoal"] = new Spec { Unit = 0.7f, Detail = 1, Draw = g => {
        Fill(g, "#1f2126");
        Grain(g, new[] { "#15161a", "#2c2f36", "#3a3d45", "#101114" }, 900, 0.35f, 8123);
        } },

        // ---------------------------------------------------------------
        // DAS SCHIFF VON AUSSEN (AirshipExterior.cs). Drei Materialien, die kein Prototyp-
        // Pendant in surfaces_airship_*.js haben, weil die Huelle dort mit einer im Browser
        // gezeichneten Canvas-Textur laeuft (world.js airshipBody): Ballonhaut, Gondel,
        // Wolke. Farben sind die des Prototyps, je eine Stufe dunkler wie ueberall hier.

        /// Die Ballonhaut: Spanten quer, Laengsbahnen laengs, ein cremefarbenes Emblemband.
        /// Anders als im Browser laeuft das Band hier MIT der Bahn (eine Textur weiss nicht,
        /// wo an der Huelle sie sitzt) - aus jeder Entfernung, aus der man das Schiff sieht,
        /// ist das derselbe Streifen.
        ["aAirBodySkin"] = new Spec { Unit = 6.0f, Detail = 1, Draw = g => {
        Fill(g, "#5c242c");
        for (float y = 0f; y < g.H; y += g.H / 7f)
        {
        Line(g, 0f, y, g.W, y, "#4b1d24", 4f, 1f);
        }
        for (float x = 0f; x < g.W; x += g.W / 12f)
        {
        Line(g, x, 0f, x, g.H, "#672831", 2f, 0.7f);
        }
        Rect(g, 0f, g.H * 0.46f, g.W, g.H * 0.08f, "#bcab94", 1f);
        Line(g, 0f, g.H * 0.46f, g.W, g.H * 0.46f, "#9c2f3f", 3f, 1f);
        Line(g, 0f, g.H * 0.54f, g.W, g.H * 0.54f, "#9c2f3f", 3f, 1f);
        Grain(g, new[] { "#642a32", "#4f1f26" }, 420, 0.09f, 4711);
        } },

        /// Die Motorgondel: dunkles Blech mit Nietband.
        ["aAirBodyNacelle"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#31292d");
        Line(g, 0f, g.H * 0.5f, g.W, g.H * 0.5f, "#252023", 2f, 1f);
        for (float x = 6f; x < g.W; x += 18f)
        {
        Rect(g, x, g.H * 0.5f - 2f, 3f, 3f, "#4a4046", 1f);
        }
        Grain(g, new[] { "#3a3237", "#282225" }, 260, 0.10f, 5150);
        } },

        /// Die Wolke unter dem Schiff. Kein Verlauf (Canvas2D kann keinen): heller Kern,
        /// blaeuliche Unterseite als Band, ein paar weiche Flecken.
        ["aAirCloudPuff"] = new Spec { Unit = 8.0f, Detail = 1, Draw = g => {
        Fill(g, "#c9d2e2");
        Rect(g, 0f, 0f, g.W, g.H * 0.42f, "#e8edf5", 1f);
        Rect(g, 0f, g.H * 0.78f, g.W, g.H * 0.22f, "#9fb0c8", 1f);
        Grain(g, new[] { "#dde3ee", "#b8c3d6" }, 300, 0.08f, 2609);
        } },
    };
}
