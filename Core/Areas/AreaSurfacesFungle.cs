// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * DIE FUNGLES MATERIALKATALOG - 236 Oberflaechen, aus dem Prototyp uebersetzt.
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
 * Assets/NightfallWeb/src/surfaces_fungle_*.js, Statement fuer Statement. Das Vokabular der
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
    private static readonly Dictionary<string, Spec> FungleCatalogue = new()
    {

        // ---------------------------------------------------------------
        // aus surfaces_fungle_cafeteria.js
        // ---------------------------------------------------------------
        ["funCafeteriaFloorSand"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#d9bd99");
        Grain(g, new[] { "#e8d0ac", "#e3c6a2", "#d7bc97", "#cdb28d" }, 800, 0.13f, 9280);
        // Duenen-Andeutung, KEINE Fugenlinien
        for (float i = 0f; i < 2f; i += 1f)
        {
            g.StrokeEllipse(g.W * (0.35f + 0.3f * i), g.H * (0.32f + 0.36f * i), g.W * (0.38f + 0.16f * i), g.W * (0.38f + 0.16f * i), MathF.Max(4f, g.W * 0.06f), C("#f2ddb9"), 0.08f);
        }
        } },
        ["funCafeteriaOutSand"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#d9bd99");
        Grain(g, new[] { "#e6cda9", "#e3c6a2", "#d5ba95", "#caaf89" }, 900, 0.13f, 5127);
        // Duenen-Andeutung, KEINE Fugenlinien
        for (float i = 0f; i < 3f; i += 1f)
        {
            g.StrokeEllipse(g.W * (0.3f + 0.22f * i), g.H * (0.3f + 0.2f * i), g.W * (0.34f + 0.14f * i), g.W * (0.34f + 0.14f * i), MathF.Max(4f, g.W * 0.06f), C("#f2ddb9"), 0.08f);
        }
        // pebbles
        for (float i = 0f; i < 10f; i += 1f)
        {
            Rect(g, (i * 0.317f % 1f) * g.W, (i * 0.611f % 1f) * g.H, 3f, 2f, "#b3946f", 1f);
        }
        } },
        ["funCafeteriaWalkWay"] = new Spec { Unit = 1.8f, Draw = g => {
        Fill(g, "#a56a3c");
        Grain(g, new[] { "#b57845", "#955d34" }, 700, 0.14f, 8047);
        // trampled cross-lines
        for (float i = 0f; i < 4f; i += 1f)
        {
            float y = g.H * (0.2f + 0.2f * i);
            Line(g, 0f, y, g.W, y + g.H * 0.04f, "#8a5330", 2f, 1f);
        }
        } },
        ["funCafeteriaWallTeal"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   wallPanels(g, w, h, '#4b626b', '#3a4f57', '#2c3c43', '#57727
        Fill(g, "#4b626b");
        Grain(g, new[] { "#4b626b" }, 400, 0.06f, 5131);
        } },
        ["funCafeteriaWallMaroon"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   wallPanels(g, w, h, '#743846', '#5d2b38', '#451f2a', '#84455
        Fill(g, "#743846");
        Grain(g, new[] { "#743846" }, 400, 0.06f, 3140);
        } },
        ["funCafeteriaWallRed"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   wallPanels(g, w, h, '#8e2f31', '#732527', '#571c1e', '#a03c3
        Fill(g, "#8e2f31");
        Grain(g, new[] { "#8e2f31" }, 400, 0.06f, 7940);
        } },
        ["funCafeteriaCap"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#1d1615");
        Grain(g, new[] { "#261d1b", "#151010" }, 400, 0.12f, 2031);
        } },
        ["funCafeteriaSkirt"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#1f1916");
        Grain(g, new[] { "#281f1b", "#171211" }, 300, 0.12f, 2590);
        } },
        ["funCafeteriaTableBlue"] = new Spec { Unit = 1.6f, Draw = g => {
        Fill(g, "#3b5670");
        Grain(g, new[] { "#446282", "#324a61" }, 500, 0.11f, 649);
        // lengthwise brush marks
        for (float i = 0f; i < 4f; i += 1f)
        {
            float y = g.H * (0.18f + 0.2f * i);
            Line(g, 0f, y, g.W, y, "#2c4258", 2f, 1f);
        }
        } },
        ["funCafeteriaBenchRed"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#871e2a");
        Grain(g, new[] { "#96252f", "#751823" }, 400, 0.12f, 2326);
        // cushion seam
        Line(g, 2f, 2f, 2f+g.W - 4f, 2f, "#6b1520", 2f, 1f);
        Line(g, 2f, 2f+g.H - 4f, 2f+g.W - 4f, 2f+g.H - 4f, "#6b1520", 2f, 1f);
        Line(g, 2f, 2f, 2f, 2f+g.H - 4f, "#6b1520", 2f, 1f);
        Line(g, 2f+g.W - 4f, 2f, 2f+g.W - 4f, 2f+g.H - 4f, "#6b1520", 2f, 1f);
        } },
        ["funCafeteriaBenchGreen"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#2c573e");
        Grain(g, new[] { "#346449", "#254834" }, 400, 0.12f, 1275);
        Line(g, 2f, 2f, 2f+g.W - 4f, 2f, "#1f3d2b", 2f, 1f);
        Line(g, 2f, 2f+g.H - 4f, 2f+g.W - 4f, 2f+g.H - 4f, "#1f3d2b", 2f, 1f);
        Line(g, 2f, 2f, 2f, 2f+g.H - 4f, "#1f3d2b", 2f, 1f);
        Line(g, 2f+g.W - 4f, 2f, 2f+g.W - 4f, 2f+g.H - 4f, "#1f3d2b", 2f, 1f);
        } },
        ["funCafeteriaBenchNavy"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#3d4a5b");
        Grain(g, new[] { "#475567", "#333f4e" }, 400, 0.12f, 1431);
        Line(g, 2f, 2f, 2f+g.W - 4f, 2f, "#2b3542", 2f, 1f);
        Line(g, 2f, 2f+g.H - 4f, 2f+g.W - 4f, 2f+g.H - 4f, "#2b3542", 2f, 1f);
        Line(g, 2f, 2f, 2f, 2f+g.H - 4f, "#2b3542", 2f, 1f);
        Line(g, 2f+g.W - 4f, 2f, 2f+g.W - 4f, 2f+g.H - 4f, "#2b3542", 2f, 1f);
        } },
        ["funCafeteriaWood"] = new Spec { Unit = 1.3f, Detail = 1, Draw = g => {
        Fill(g, "#a06f3c");
        Grain(g, new[] { "#ad7a44", "#916034" }, 500, 0.12f, 4355);
        // plank lines
        for (float i = 0f; i < 5f; i += 1f)
        {
            float y = g.H * (0.12f + 0.18f * i);
            Line(g, 0f, y, g.W, y + 1f, "#7d5330", 2f, 1f);
        }
        } },
        ["funCafeteriaRail"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        Fill(g, "#70241f");
        Grain(g, new[] { "#7f2b25", "#5e1c18" }, 400, 0.14f, 474);
        // branch grooves
        for (float i = 0f; i < 3f; i += 1f)
        {
            float x = g.W * (0.2f + 0.3f * i);
            Line(g, x, 0f, x + 3f, g.H, "#511713", 2f, 1f);
        }
        } },
        ["funCafeteriaStone"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#484038");
        Grain(g, new[] { "#544b42", "#3c352e" }, 500, 0.15f, 5624);
        } },
        ["funCafeteriaTarp"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#642847");
        // from #81355e at (-15.50, 8.00), fold shadow
        Grain(g, new[] { "#7d3259", "#501f3a" }, 600, 0.14f, 9026);
        // fold lines
        float fold = g.W / MathF.Max(2f, MathF.Round(g.W / (0.42f * 128f)));
        for (float x = fold; x < g.W - 1f; x += fold)
        {
            Line(g, x, 0f, x + fold * 0.18f, g.H, "#501f3a", 3f, 1f);
        }
        // worn fold highlights
        for (float x = fold * 0.5f; x < g.W - 1f; x += fold)
        {
            Line(g, x, 0f, x + fold * 0.12f, g.H, "#8f3f68", 2f, 1f);
        }
        // rope bands at 1/3 and 2/3
        Rect(g, 0f, g.H * 0.32f, g.W, g.H * 0.045f, "#3f1830", 1f);
        Rect(g, 0f, g.H * 0.64f, g.W, g.H * 0.045f, "#3f1830", 1f);
        // from #a4537c at (-16.30, 8.12), worn spots
        for (float i = 0f; i < 7f; i += 1f)
        {
            Rect(g, (i * 0.37f % 1f) * g.W, (i * 0.53f % 1f) * g.H, g.W * 0.05f, g.H * 0.05f, "#a4537c", 1f);
        }
        } },
        // ---------------------------------------------------------------
        // aus surfaces_fungle_comms.js
        // ---------------------------------------------------------------
        ["funCommsFloor"] = new Spec { Unit = 0.85f, Detail = 1, Draw = g => {
        Fill(g, "#3d594f");
        // von #44635b
        Line(g, 1f, 1f, 1f+g.W - 2f, 1f, "#334a42", 2f, 1f);
        Line(g, 1f, 1f+g.H - 2f, 1f+g.W - 2f, 1f+g.H - 2f, "#334a42", 2f, 1f);
        Line(g, 1f, 1f, 1f, 1f+g.H - 2f, "#334a42", 2f, 1f);
        Line(g, 1f+g.W - 2f, 1f, 1f+g.W - 2f, 1f+g.H - 2f, "#334a42", 2f, 1f);
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#334a42", 2f, 1f);
        Grain(g, new[] { "#46645a", "#355047" }, 450, 0.10f, 7893);
        } },
        ["funCommsTerrasse"] = new Spec { Unit = 1.25f, Detail = 1, Draw = g => {
        var rnd = new Rng(8739);
        Fill(g, "#3d594f");
        // von #44635b
        for (float i = 0f; i < 26f; i += 1f)
        {
            float s = 6f + rnd.Next() * 16f;
            Rect(g, rnd.Next() * g.W, rnd.Next() * g.H, s, s * 0.6f, i % 2 != 0f ? "#35514a" : "#46645a", 0.5f);
        }
        Grain(g, new[] { "#44624f", "#31493f" }, 500, 0.12f, 8739);
        } },
        ["funCommsPath"] = new Spec { Unit = 1.1f, Detail = 1, Draw = g => {
        Fill(g, "#c29250");
        // von #d7a259
        Grain(g, new[] { "#cfa05b", "#ad8347" }, 550, 0.14f, 9937);
        } },
        ["funCommsPlateau"] = new Spec { Unit = 1.3f, Detail = 1, Draw = g => {
        Fill(g, "#222725");
        // von #262c29
        Grain(g, new[] { "#2b322e", "#1c211e" }, 550, 0.13f, 2936);
        } },
        ["funCommsCliffFace"] = new Spec { Unit = 1.6f, Draw = g => {
        Fill(g, "#a86b3c");
        // von #ba7742
        for (float y = 4f; y < g.H; y += 13f)
        {
        Line(g, 0f, y, g.W, y, "#8f5730", 2f, 1f);
        }
        Grain(g, new[] { "#b5764a", "#96603a", "#7e5233" }, 600, 0.14f, 2648);
        } },
        ["funCommsWallRock"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        var rnd = new Rng(5745);
        Fill(g, "#6b4839");
        // von #77503f
        for (float i = 0f; i < 18f; i += 1f)
        {
            float s = 8f + rnd.Next() * 20f;
            Rect(g, rnd.Next() * g.W, rnd.Next() * g.H, s, s * 0.7f, i % 2 != 0f ? "#5a3c2f" : "#7a5542", 0.4f);
        }
        Grain(g, new[] { "#77503f", "#4c332a" }, 550, 0.13f, 5745);
        } },
        ["funCommsWallMetal"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#787f7e");
        // von #858c8b
        for (float y = 0f; y < g.H; y += 26f)
        {
        Line(g, 0f, y, g.W, y, "#687070", 2f, 1f);
        }
        for (float x = 0f; x < g.W; x += 34f)
        {
        Line(g, x, 0f, x, g.H, "#687070", 2f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#8b9291", 2f, 1f);
        Grain(g, new[] { "#838a89", "#6a7271" }, 450, 0.11f, 2004);
        } },
        ["funCommsDoor"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        Fill(g, "#c29250");
        // von #d7a259
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#9c7440", 3f, 1f);
        for (float x = 6f; x < g.W; x += 22f)
        {
        Line(g, x, 0f, x, g.H, "#ad8147", 2f, 1f);
        }
        Grain(g, new[] { "#cfa05b", "#a87c44" }, 350, 0.10f, 5);
        } },
        ["funCommsCounter"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#56615c");
        Line(g, 2f, 2f, 2f+g.W - 4f, 2f, "#46504c", 3f, 1f);
        Line(g, 2f, 2f+g.H - 4f, 2f+g.W - 4f, 2f+g.H - 4f, "#46504c", 3f, 1f);
        Line(g, 2f, 2f, 2f, 2f+g.H - 4f, "#46504c", 3f, 1f);
        Line(g, 2f+g.W - 4f, 2f, 2f+g.W - 4f, 2f+g.H - 4f, "#46504c", 3f, 1f);
        Line(g, 0f, 2f, g.W, 2f, "#66716c", 2f, 1f);
        Grain(g, new[] { "#5e6964", "#4a544f" }, 300, 0.10f, 7793);
        } },
        ["funCommsMachine"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        Fill(g, "#a6aeaa");
        // geschaetzt (Sprite weiss-grau #b8c0bc)
        Line(g, 0f, 2f, g.W, 2f, "#b8c0bc", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#8b938f", 2f, 1f);
        Grain(g, new[] { "#b0b8b4", "#98a09c" }, 350, 0.10f, 3069);
        } },
        ["funCommsCabinetDoor"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#8b9490");
        // geschaetzt (Sprite #9aa4a0)
        Line(g, 3f, 3f, 3f+g.W - 6f, 3f, "#6f7874", 3f, 1f);
        Line(g, 3f, 3f+g.H - 6f, 3f+g.W - 6f, 3f+g.H - 6f, "#6f7874", 3f, 1f);
        Line(g, 3f, 3f, 3f, 3f+g.H - 6f, "#6f7874", 3f, 1f);
        Line(g, 3f+g.W - 6f, 3f, 3f+g.W - 6f, 3f+g.H - 6f, "#6f7874", 3f, 1f);
        // der Buegelgriff
        Rect(g, g.W * 0.72f, g.H * 0.42f, g.W * 0.10f, g.H * 0.16f, "#5f6864", 1f);
        Grain(g, new[] { "#95a09b", "#7e8783" }, 300, 0.10f, 3104);
        } },
        ["funCommsBulb"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#f4e7c2");
        for (float y = 3f; y < g.H; y += 9f)
        {
        Line(g, 0f, y, g.W, y, "#e4d2a4", 2f, 1f);
        }
        Grain(g, new[] { "#faf0d4", "#e2d0a2" }, 200, 0.08f, 5613);
        } },
        ["funCommsCrateWood"] = new Spec { Unit = 0.7f, Draw = g => {
        Fill(g, "#684b37");
        // von #74533d
        for (float y = 5f; y < g.H; y += 12f)
        {
        Line(g, 0f, y, g.W, y, "#553d2c", 2f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#7a5a42", 2f, 1f);
        Grain(g, new[] { "#71523c", "#5b4230" }, 350, 0.11f, 1735);
        } },
        ["funCommsLadder"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#a34025");
        // von #b5472a
        Line(g, 0f, 1f, g.W, 1f, "#c25a37", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#83331d", 2f, 1f);
        Grain(g, new[] { "#b04a2c", "#8f3a22" }, 250, 0.10f, 4924);
        } },
        ["funCommsDrum"] = new Spec { Unit = 0.7f, Draw = g => {
        Fill(g, "#515c67");
        // von #5a6672
        Line(g, 0f, 2f, g.W, 2f, "#64717d", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#414b54", 2f, 1f);
        Grain(g, new[] { "#5b6873", "#47515a" }, 300, 0.10f, 3943);
        } },
        ["funCommsRock"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        var rnd = new Rng(7503);
        Fill(g, "#c4924e");
        // von #d9a256
        for (float i = 0f; i < 14f; i += 1f)
        {
            float s = 7f + rnd.Next() * 18f;
            Rect(g, rnd.Next() * g.W, rnd.Next() * g.H, s, s * 0.65f, i % 2 != 0f ? "#b07f42" : "#d4a35e", 0.4f);
        }
        Grain(g, new[] { "#d0a05c", "#a67a40" }, 450, 0.12f, 7503);
        } },
        ["funCommsCrystal"] = new Spec { Unit = 0.6f, Draw = g => {
        var rnd = new Rng(6762);
        Fill(g, "#8d4670");
        // von #9c4d75
        for (float i = 0f; i < 10f; i += 1f)
        {
            float x0 = rnd.Next() * g.W, y0 = g.H;
            g.FillQuad(x0, y0, x0 + 4f, y0 - 8f - rnd.Next() * 8f, x0 + 8f, y0, x0 + 8f, y0, C(i % 2 != 0f ? "#a5598a" : "#753a5c"), 0.5f);
        }
        Grain(g, new[] { "#9b5080", "#7c3f63" }, 250, 0.10f, 6762);
        } },
        ["funCommsSummit"] = new Spec { Unit = 2.0f, Draw = g => {
        var rnd = new Rng(1001);
        Fill(g, "#403f3d");
        // von #474644
        for (float i = 0f; i < 20f; i += 1f)
        {
            float s = 10f + rnd.Next() * 26f;
            Rect(g, rnd.Next() * g.W, rnd.Next() * g.H, s, s * 0.6f, i % 2 != 0f ? "#4a4947" : "#363533", 0.4f);
        }
        Grain(g, new[] { "#4b4a48", "#323130" }, 500, 0.12f, 1001);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_fungle_dropship.js
        // ---------------------------------------------------------------
        ["funDropshipDeck"] = new Spec { Unit = 2.6f, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   plates(g, w, h, '#3a363a', '#6a666a')
        Fill(g, "#565256");
        Grain(g, new[] { "#565256" }, 400, 0.06f, 7050);
        } },
        ["funDropshipDeckGreen"] = new Spec { Unit = 2.6f, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   plates(g, w, h, '#454f42', '#78867a')
        Fill(g, "#62705f");
        Grain(g, new[] { "#62705f" }, 400, 0.06f, 3675);
        } },
        ["funDropshipRubble"] = new Spec { Unit = 2.2f, Draw = g => {
        Fill(g, "#6b615f");
        Grain(g, new[] { "#584f4d", "#7d7371", "#4e4644", "#857a78" }, 1400, 0.22f, 2768);
        } },
        ["funDropshipRampSand"] = new Spec { Unit = 2.4f, Draw = g => {
        Fill(g, "#524a48");
        Grain(g, new[] { "#47403e", "#5e5553", "#3f3937" }, 900, 0.16f, 6610);
        } },
        ["funDropshipRockGround"] = new Spec { Unit = 2.0f, Draw = g => {
        Fill(g, "#1e1917");
        Grain(g, new[] { "#171310", "#282220", "#232b36" }, 1100, 0.25f, 7430);
        } },
        ["funDropshipSand"] = new Spec { Unit = 3.0f, Draw = g => {
        Fill(g, "#dcbd96");
        Grain(g, new[] { "#ceb088", "#e8caa4", "#c2a37c" }, 1000, 0.12f, 2665);
        } },
        ["funDropshipPath"] = new Spec { Unit = 2.4f, Draw = g => {
        Fill(g, "#a9713f");
        Grain(g, new[] { "#986234", "#bb8351", "#875731" }, 1200, 0.18f, 7337);
        } },
        ["funDropshipHull"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   plates(g, w, h, '#414c40', '#77857a')
        Fill(g, "#5f6e5e");
        Grain(g, new[] { "#5f6e5e" }, 400, 0.06f, 8942);
        } },
        ["funDropshipHullDark"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   plates(g, w, h, '#1c1614', '#3a2f2c')
        Fill(g, "#2e2523");
        Grain(g, new[] { "#2e2523" }, 400, 0.06f, 7488);
        } },
        ["funDropshipHullCap"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#2b2926");
        Grain(g, new[] { "#232120", "#343230" }, 400, 0.12f, 9333);
        } },
        ["funDropshipScorch"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#2e2b28");
        Grain(g, new[] { "#26241f", "#383430", "#201e1b" }, 800, 0.2f, 4254);
        } },
        ["funDropshipShade"] = new Spec { Unit = 1.6f, Draw = g => {
        Fill(g, "#1d1522");
        Grain(g, new[] { "#171017", "#251c28" }, 500, 0.15f, 6487);
        } },
        ["funDropshipBoulder"] = new Spec { Unit = 1.3f, Detail = 1, Draw = g => {
        Fill(g, "#3a3336");
        Grain(g, new[] { "#2e282b", "#463e41", "#251912" }, 700, 0.22f, 2752);
        } },
        ["funDropshipStone"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#6b3f24");
        Grain(g, new[] { "#59331c", "#7d4c2c", "#4c2b17" }, 500, 0.2f, 3620);
        } },
        ["funDropshipEmber"] = new Spec { Unit = 0.5f, Draw = g => {
        var rnd = new Rng(6927);
        Fill(g, "#c84a12");
        Grain(g, new[] { "#ff9a30", "#e86a1a", "#ffd24a" }, 350, 0.5f, 6927);
        for (float i = 0f; i < 14f; i += 1f)
        {
            Rect(g, rnd.Next() * g.W, rnd.Next() * g.H, 2f + rnd.Next() * 3f, 2f + rnd.Next() * 2f, "#ffe9a0", 1f);
        }
        } },
        ["funDropshipFlame"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#ffb43a");
        Grain(g, new[] { "#ffe08a", "#ff9a28", "#fff3c8" }, 260, 0.55f, 3136);
        } },
        ["funDropshipLog"] = new Spec { Unit = 1.1f, Detail = 1, Draw = g => {
        Fill(g, "#3d4850");
        Grain(g, new[] { "#333d44", "#48545c" }, 400, 0.15f, 6174);
        for (float y = g.H * 0.2f; y < g.H; y += g.H * 0.25f)
        {
            Line(g, 0f, y, g.W, y, "#2b3339", 2f, 1f);
        }
        } },
        ["funDropshipTube"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        Fill(g, "#380f1e");
        Grain(g, new[] { "#2c0a16", "#48182a", "#54202f" }, 450, 0.2f, 3228);
        for (float x = g.W * 0.2f; x < g.W; x += g.W * 0.25f)
        {
            Line(g, x, 0f, x, g.H, "#240812", 2f, 1f);
        }
        } },
        ["funDropshipCrateBox"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        Fill(g, "#3a4a45");
        Grain(g, new[] { "#30403b", "#455750" }, 400, 0.15f, 3571);
        Line(g, 2f, 2f, 2f+g.W - 4f, 2f, "#28352f", 3f, 1f);
        Line(g, 2f, 2f+g.H - 4f, 2f+g.W - 4f, 2f+g.H - 4f, "#28352f", 3f, 1f);
        Line(g, 2f, 2f, 2f, 2f+g.H - 4f, "#28352f", 3f, 1f);
        Line(g, 2f+g.W - 4f, 2f, 2f+g.W - 4f, 2f+g.H - 4f, "#28352f", 3f, 1f);
        } },
        ["funDropshipPlateMetal"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#525462");
        Grain(g, new[] { "#464855", "#5e6070" }, 300, 0.12f, 2043);
        for (float d = -g.H; d < g.W + g.H; d += 10f)
        {
            Line(g, d, 0f, d + g.H, g.H, "#3c3e4a", 2f, 1f);
        }
        } },
        // ---------------------------------------------------------------
        // aus surfaces_fungle_fishingdock.js
        // ---------------------------------------------------------------
        ["funDockPlanks"] = new Spec { Unit = 1.10f, Detail = 1, Draw = g => {
        Fill(g, "#6b858d");
        // querlaufende Planken: Fugen senkrecht im Bild (= Nord-Sued auf dem Steg)
        for (float x = 0f; x < g.W; x += g.W / 3.2f)
        {
        Line(g, x, 0f, x, g.H, "#48606b", 2f, 1f);
        }
        // versetzte Längsstoesse der drei Plankenreihen
        Rect(g, g.W / 3.2f, g.H * 0.33f, g.W / 3.2f, 2f, "#557079", 1f);
        Rect(g, 2f * g.W / 3.2f, g.H * 0.66f, g.W / 3.2f, 2f, "#557079", 1f);
        // jede mittlere Reihe einen Hauch heller (die abgetretenene Laufspur)
        Rect(g, 0f, g.H * 0.36f, g.W, g.H * 0.30f, "#7c96a0", 0.35f);
        Grain(g, new[] { "#78929b", "#5e7681", "#86a0a8" }, 520, 0.11f, 8482);
        } },
        ["funDockEdge"] = new Spec { Unit = 1.20f, Detail = 1, Draw = g => {
        Fill(g, "#202227");
        for (float y = 0f; y < g.H; y += 24f)
        {
        Line(g, 0f, y, g.W, y, "#181a1f", 1f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#33363e", 1f, 1f);
        Grain(g, new[] { "#26282e", "#191b20" }, 300, 0.10f, 372);
        } },
        ["funDockSandKante"] = new Spec { Unit = 1.60f, Draw = g => {
        var rnd = new Rng(563);
        Fill(g, "#dcc09c");
        Grain(g, new[] { "#e4cba6", "#d2b68f", "#c9ad85" }, 700, 0.13f, 563);
        for (float i = 0f; i < 14f; i += 1f)
        {
            Rect(g, rnd.Next() * g.W, rnd.Next() * g.H, 2f + rnd.Next() * 3f, 2f + rnd.Next() * 2f, "#ecd6b4", 0.16f);
        }
        } },
        ["funDockPost"] = new Spec { Unit = 0.90f, Detail = 1, Draw = g => {
        Fill(g, "#31353f");
        for (float x = 3f; x < g.W; x += 10f)
        {
        Line(g, x, 0f, x, g.H, "#272b34", 1f, 1f);
        }
        Line(g, 1f, 0f, 1f, g.H, "#3d434f", 1f, 1f);
        Grain(g, new[] { "#383d49", "#2a2e38" }, 240, 0.09f, 1547);
        } },
        ["funDockConsole"] = new Spec { Unit = 0.62f, Draw = g => {
        Fill(g, "#79848a");
        for (float x = 4f; x < g.W; x += 12f)
        {
        Line(g, x, g.H * 0.30f, x, g.H * 0.72f, "#626d73", 2f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#8d989e", 1f, 1f);
        Rect(g, 0f, g.H * 0.84f, g.W, g.H * 0.16f, "#57626a", 1f);
        Grain(g, new[] { "#828d93", "#6d787e" }, 260, 0.08f, 8695);
        } },
        ["funDockBuoy"] = new Spec { Unit = 0.50f, Draw = g => {
        Fill(g, "#c4ba29");
        Rect(g, 0f, g.H * 0.10f, g.W, g.H * 0.14f, "#a89f1f", 1f);
        // Deckelband
        Rect(g, 0f, g.H * 0.76f, g.W, g.H * 0.10f, "#a89f1f", 1f);
        Line(g, 0f, g.H * 0.24f, g.W, g.H * 0.24f, "#dcd230", 1f, 1f);
        Grain(g, new[] { "#cfC42e", "#b3a923" }, 200, 0.10f, 9900);
        Rect(g, g.W * 0.2f, g.H * 0.5f, 3f, g.H * 0.2f, "#847c17", 0.25f);
        Rect(g, g.W * 0.65f, g.H * 0.42f, 2f, g.H * 0.26f, "#847c17", 0.25f);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_fungle_greenhouse.js
        // ---------------------------------------------------------------
        ["funGreenhouseTile"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        Fill(g, "#a4b6b5");
        // from #bbcfcf at (9.20,-10.10)
        // from the grout tone #a5b8b7 (same field)
        Line(g, 1f, 1f, 1f+g.W - 2f, 1f, "#93a6a5", 2f, 1f);
        Line(g, 1f, 1f+g.H - 2f, 1f+g.W - 2f, 1f+g.H - 2f, "#93a6a5", 2f, 1f);
        Line(g, 1f, 1f, 1f, 1f+g.H - 2f, "#93a6a5", 2f, 1f);
        Line(g, 1f+g.W - 2f, 1f, 1f+g.W - 2f, 1f+g.H - 2f, "#93a6a5", 2f, 1f);
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#98abaa", 2f, 1f);
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#98abaa", 2f, 1f);
        Grain(g, new[] { "#aebfbf", "#9cacab" }, 400, 0.10f, 3378);
        } },
        ["funGreenhousePlanter"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#aabdbb");
        // from #bed0cf at (9.00,-9.50)
        Line(g, 0f, 2f, g.W, 2f, "#bccfcc", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#96a9a7", 2f, 1f);
        Grain(g, new[] { "#b4c6c4", "#9db0ae" }, 300, 0.10f, 9438);
        } },
        ["funGreenhouseSoil"] = new Spec { Unit = 0.7f, Draw = g => {
        Fill(g, "#49280c");
        // from #5b320f at (9.00,-11.10)
        Grain(g, new[] { "#5a3413", "#3a1e08", "#6b4520" }, 700, 0.22f, 634);
        } },
        ["funGreenhouseGlass"] = new Spec { Unit = 1.4f, Detail = 1, Draw = g => {
        Fill(g, "#9fc0be");
        // from #bed0cf at (9.00,-9.50)
        Line(g, 0f, 1f, g.W, 1f, "#c2dedc", 2f, 0.5f);
        // der helle Scheibenrand oben
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#7fa5a3", 2f, 0.5f);
        for (float x = 10f; x < g.W; x += 24f)
        {
        Line(g, x, 0f, x, g.H, "#b0ccca", 1f, 1f);
        }
        // Scheibenfugen
        Grain(g, new[] { "#aac9c7", "#90b2b0" }, 200, 0.08f, 2788);
        } },
        ["funGreenhouseFrame"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#100b10");
        // from #171117 at (6.95,-9.50)
        Line(g, 1f, 0f, 1f, g.H, "#2a242a", 2f, 1f);
        Grain(g, new[] { "#181218", "#0a060a" }, 250, 0.12f, 6214);
        } },
        ["funGreenhouseJungle"] = new Spec { Unit = 2.2f, Draw = g => {
        var rnd = new Rng(2226);
        Fill(g, "#301328");
        // gemischt aus #2e1527/#3d152d/#502341 (Naht-Anker), -1 Stufe
        Grain(g, new[] { "#3c1c33", "#261020", "#4a2540", "#1d0a18" }, 1100, 0.20f, 2226);
        for (float i = 0f; i < 14f; i += 1f)
        {
            // verlaufene Braunroste (Bodensaeume)
            float s = 8f + rnd.Next() * 22f;
            Rect(g, rnd.Next() * g.W, rnd.Next() * g.H, s, s * (0.4f + rnd.Next() * 0.5f), i % 2 != 0f ? "#4a2c14" : "#3a1c10", 0.16f);
        }
        } },
        ["funGreenhouseWeg"] = new Spec { Unit = 1.6f, Draw = g => {
        var rnd = new Rng(1251);
        Fill(g, "#a06a38");
        // from #c8854b at (9.00,-3.50)
        Grain(g, new[] { "#b0763f", "#8f5c30", "#c08147" }, 800, 0.20f, 1251);
        for (float i = 0f; i < 8f; i += 1f)
        {
            // Trittbluren
            Rect(g, rnd.Next() * g.W, rnd.Next() * g.H, 10f + rnd.Next() * 18f, 3f, "#7c4e28", 0.14f);
        }
        } },
        ["funGreenhouseFels"] = new Spec { Unit = 1.8f, Draw = g => {
        var rnd = new Rng(3820);
        Fill(g, "#150a14");
        // from #1a0e19 (Ownership) / #1d0c1f
        Grain(g, new[] { "#221020", "#0e060e", "#2c1424" }, 900, 0.22f, 3820);
        for (float i = 0f; i < 10f; i += 1f)
        {
            // Felskanten / Risse
            Line(g, rnd.Next() * g.W, rnd.Next() * g.H, rnd.Next() * g.W, rnd.Next() * g.H, i % 2 != 0f ? "#3c1e30" : "#241020", 1f + rnd.Next() * 2f, 0.18f);
        }
        } },
        ["funGreenhouseStep"] = new Spec { Unit = 0.55f, Draw = g => {
        Fill(g, "#1e1018");
        // geschaetzt nach funGreenhouseFels-Werten
        for (float y = 4f; y < g.H; y += 9f)
        {
        Line(g, 0f, y, g.W, y, "#33202c", 2f, 1f);
        }
        // Sprossenfugen
        Line(g, 0f, 1f, g.W, 1f, "#4a3342", 2f, 1f);
        // helle Tretkante (eine Stufe heller)
        Line(g, 0f, 3f, g.W, 3f, "#241620", 1f, 1f);
        // Schattfuge unter der Kante
        Grain(g, new[] { "#281822", "#160c12" }, 350, 0.14f, 9713);
        } },
        ["funGreenhouseWood"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#5c4023");
        // geschaetzt (Holzton, from #271712 aufgehellt)
        for (float y = 5f; y < g.H; y += 12f)
        {
        Line(g, 0f, y, g.W, y, "#46301a", 2f, 1f);
        }
        // Brettfugen
        Line(g, 0f, 1f, g.W, 1f, "#6f5030", 2f, 1f);
        // helle Kante
        Grain(g, new[] { "#664827", "#4e361e" }, 350, 0.14f, 102);
        } },
        ["funGreenhouseLadderYellow"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#d8a834");
        // from #f8c43f at (-9.79,1.64)
        for (float x = 6f; x < g.W; x += 14f)
        {
        Line(g, x, 0f, x, g.H, "#b8882a", 2f, 1f);
        }
        // Sprossenstreifen
        Line(g, 0f, 1f, g.W, 1f, "#ecc254", 2f, 1f);
        // helle Kante
        Grain(g, new[] { "#e0b53c", "#c49a2e" }, 250, 0.12f, 7540);
        } },
        ["funGreenhouseCrystal"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        var rnd = new Rng(9653);
        Fill(g, "#6a3648");
        // from #7d4058 at (9.20,-8.80)
        // Facetten
        for (float i = 0f; i < 7f; i += 1f)
        {
            float fx = rnd.Next() * g.W, fy = rnd.Next() * g.H, s = 6f + rnd.Next() * 14f;
            g.FillQuad(fx, fy, fx + s, fy + s * 0.4f, fx + s * 0.3f, fy + s, fx + s * 0.3f, fy + s, C(i % 2 != 0f ? "#7d4258" : "#582a3a"), 0.4f);
        }
        // from #7f4156 at (9.70,-8.00), Kante
        Line(g, 1f, 1f, 1f+g.W - 2f, 1f, "#8a4c64", 2f, 1f);
        Line(g, 1f, 1f+g.H - 2f, 1f+g.W - 2f, 1f+g.H - 2f, "#8a4c64", 2f, 1f);
        Line(g, 1f, 1f, 1f, 1f+g.H - 2f, "#8a4c64", 2f, 1f);
        Line(g, 1f+g.W - 2f, 1f, 1f+g.W - 2f, 1f+g.H - 2f, "#8a4c64", 2f, 1f);
        Grain(g, new[] { "#754054", "#5e2e40" }, 300, 0.12f, 9653);
        } },
        ["funGreenhouseMushStem"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#9a8894");
        // geschaetzt (Mushroom-Sprite)
        Line(g, 1f, 0f, 1f, g.H, "#b09da8", 2f, 1f);
        Grain(g, new[] { "#a4919c", "#8a7884" }, 250, 0.12f, 1397);
        } },
        ["funGreenhouseMushCap"] = new Spec { Unit = 0.5f, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   grd.addColorStop(0, '#c6adb8')
        //   grd.addColorStop(0.55, '#a88c97')
        //   grd.addColorStop(1, '#7e646f')
        Fill(g, "#a88c97");
        Grain(g, new[] { "#a88c97" }, 400, 0.06f, 5762);
        } },
        ["funGreenhouseGlowCap"] = new Spec { Unit = 0.5f, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   grd.addColorStop(0, '#25626b')
        //   grd.addColorStop(0.55, '#173a41')
        //   grd.addColorStop(1, '#102a2f')
        Fill(g, "#173a41");
        Grain(g, new[] { "#173a41" }, 400, 0.06f, 867);
        } },
        ["funGreenhouseLeaf"] = new Spec { Unit = 0.5f, Draw = g => {
        var rnd = new Rng(137);
        Fill(g, "#314224");
        // from #3c502c at (9.00,-13.10)
        for (float i = 0f; i < 10f; i += 1f)
        {
            // Blattbuendel
            g.FillEllipse(rnd.Next() * g.W, rnd.Next() * g.H, 2f + rnd.Next() * 4f, 2f + rnd.Next() * 4f, C(i % 2 != 0f ? "#3c502c" : "#273618"), 0.4f);
        }
        Grain(g, new[] { "#3a4e2a", "#28371d" }, 300, 0.12f, 137);
        } },
        ["funGreenhouseCarrot"] = new Spec { Unit = 0.5f, Draw = g => {
        var rnd = new Rng(3574);
        Fill(g, "#a4551c");
        // geschaetzt (frontPlants-Sprite)
        for (float i = 0f; i < 6f; i += 1f)
        {
            g.FillEllipse(rnd.Next() * g.W, rnd.Next() * g.H, 2f + rnd.Next() * 3f, 2f + rnd.Next() * 3f, C("#c06a24"), 1f);
        }
        Grain(g, new[] { "#b06020", "#8e4818" }, 250, 0.12f, 3574);
        } },
        ["funGreenhouseMetal"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#2c2634");
        // from #352e3f at (16.90,-13.90)
        Line(g, 0f, 2f, g.W, 2f, "#3c3446", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#221d2a", 2f, 1f);
        Grain(g, new[] { "#332c3c", "#262030" }, 350, 0.12f, 3927);
        } },
        ["funGreenhouseMonitor"] = new Spec { Unit = 0.7f, Draw = g => {
        Fill(g, "#3c4656");
        // from #546b80 at (12.80,-15.60)
        // der Messwert als Schirmflaeche
        Rect(g, g.W * 0.12f, g.H * 0.14f, g.W * 0.76f, g.H * 0.5f, "#546b80", 1f);
        // helle Zeilen
        for (float y = g.H * 0.2f; y < g.H * 0.58f; y += 4f)
        {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   g.fillRect(w * 0.16, y, w * 0.68, 1.5)
        Fill(g, "#8a8a8a");
        Grain(g, new[] { "#8a8a8a" }, 400, 0.06f, 4704);
        }
        Line(g, g.W * 0.12f, g.H * 0.14f, g.W * 0.12f+g.W * 0.76f, g.H * 0.14f, "#20242c", 3f, 1f);
        Line(g, g.W * 0.12f, g.H * 0.14f+g.H * 0.5f, g.W * 0.12f+g.W * 0.76f, g.H * 0.14f+g.H * 0.5f, "#20242c", 3f, 1f);
        Line(g, g.W * 0.12f, g.H * 0.14f, g.W * 0.12f, g.H * 0.14f+g.H * 0.5f, "#20242c", 3f, 1f);
        Line(g, g.W * 0.12f+g.W * 0.76f, g.H * 0.14f, g.W * 0.12f+g.W * 0.76f, g.H * 0.14f+g.H * 0.5f, "#20242c", 3f, 1f);
        Grain(g, new[] { "#424c5c", "#343e4c" }, 250, 0.10f, 4704);
        } },
        ["funGreenhouseTrunk"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#242028");
        // geschaetzt (NavConsole/MonitorTree-Sprite)
        for (float y = 3f; y < g.H; y += 8f)
        {
        Line(g, 0f, y, g.W, y, "#17141a", 2f, 1f);
        }
        Grain(g, new[] { "#2b262e", "#1c181f" }, 300, 0.12f, 8044);
        } },
        ["funGreenhouseJungleSE"] = new Spec { Unit = 2.2f, Draw = g => {
        var rnd = new Rng(3105);
        Fill(g, "#492333");
        // gemischt aus #4e2a3b/#461a30/#492738/#3c152c, -1 Stufe
        Grain(g, new[] { "#54293c", "#3a1a2b", "#5e3244", "#2f1424" }, 1100, 0.20f, 3105);
        for (float i = 0f; i < 14f; i += 1f)
        {
            // verlaufene Braunroste (Bodensaeume)
            float s = 8f + rnd.Next() * 22f;
            Rect(g, rnd.Next() * g.W, rnd.Next() * g.H, s, s * (0.4f + rnd.Next() * 0.5f), i % 2 != 0f ? "#54301c" : "#42240f", 0.16f);
        }
        } },
        // ---------------------------------------------------------------
        // aus surfaces_fungle_kitchen.js
        // ---------------------------------------------------------------
        ["funKitchenFloor"] = new Spec { Unit = 1.10f, Detail = 1, Draw = g => {
        Fill(g, "#dcc09a");
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#c2a67e", 2f, 0.5f);
        // Fugenkreuz in der Einheitsmitte
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#c2a67e", 2f, 0.5f);
        Line(g, 0f, 1f, g.W, 1f, "#d0b48c", 1f, 0.30f);
        Line(g, 1f, 0f, 1f, g.H, "#d0b48c", 1f, 0.30f);
        // jede zweite Platte einen Hauch dunkler
        Rect(g, 2f, 2f, g.W / 2f - 4f, g.H / 2f - 4f, "#d5b991", 1f);
        Grain(g, new[] { "#e2c7a1", "#d3b78f" }, 420, 0.09f, 5221);
        } },
        ["funKitchenWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#d3b58c");
        for (float x = 0f; x < g.W; x += 34f)
        {
        Line(g, x, 0f, x, g.H, "#bd9f76", 2f, 1f);
        }
        Line(g, 0f, 3f, g.W, 3f, "#e2c79e", 2f, 1f);
        // Perle unter der Kappe
        // Sockelstreifen
        Rect(g, 0f, g.H * 0.80f, g.W, g.H * 0.20f, "#b2946b", 1f);
        Line(g, 0f, g.H * 0.80f, g.W, g.H * 0.80f, "#967952", 2f, 1f);
        Grain(g, new[] { "#dcc096", "#c9ac82" }, 360, 0.09f, 1836);
        } },
        ["funKitchenWallOut"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#b3946c");
        for (float x = 0f; x < g.W; x += 30f)
        {
        Line(g, x, 0f, x, g.H, "#987b54", 2f, 1f);
        }
        for (float y = 0f; y < g.H; y += 26f)
        {
        Line(g, 0f, y, g.W, y, "#a3855e", 1f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#c2a379", 1f, 1f);
        Grain(g, new[] { "#bb9c73", "#a68962" }, 340, 0.10f, 1591);
        } },
        ["funKitchenLower"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#6e5138");
        for (float x = 0f; x < g.W; x += 26f)
        {
        Line(g, x, 0f, x, g.H, "#58402b", 2f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#7d5f43", 1f, 1f);
        Rect(g, 0f, g.H * 0.86f, g.W, g.H * 0.14f, "#4a3624", 1f);
        Grain(g, new[] { "#776044", "#61472f" }, 380, 0.11f, 5526);
        } },
        ["funKitchenHull"] = new Spec { Unit = 1.20f, Detail = 1, Draw = g => {
        Fill(g, "#3f3430");
        for (float x = 0f; x < g.W; x += 40f)
        {
        Line(g, x, 0f, x, g.H, "#332a27", 2f, 1f);
        }
        for (float y = 0f; y < g.H; y += 28f)
        {
        Line(g, 0f, y, g.W, y, "#332a27", 1f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#4c403b", 1f, 1f);
        Grain(g, new[] { "#463a35", "#362c29" }, 320, 0.10f, 232);
        } },
        ["funKitchenCeiling"] = new Spec { Unit = 1.20f, Detail = 1, Draw = g => {
        Fill(g, "#5a4430");
        for (float y = 0f; y < g.H; y += 22f)
        {
        Line(g, 0f, y, g.W, y, "#493624", 2f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#68503a", 1f, 1f);
        Grain(g, new[] { "#61492f", "#503b28" }, 300, 0.10f, 6052);
        } },
        ["funKitchenCabinet"] = new Spec { Unit = 0.62f, Draw = g => {
        Fill(g, "#8a6f4d");
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#6f5738", 3f, 0.6f);
        // mittlere Frontfuge
        Line(g, 0f, 4f, g.W, 4f, "#9d815c", 2f, 0.6f);
        // helle Oberkante der Frontreihe
        // durchgehender Sockel
        Rect(g, 0f, g.H * 0.84f, g.W, g.H * 0.16f, "#54432c", 1f);
        Line(g, 0f, g.H * 0.84f, g.W, g.H * 0.84f, "#42341f", 2f, 1f);
        // Griffleiste oben auf jeder Fronthaelfte
        Rect(g, g.W * 0.25f - 6f, g.H * 0.12f, 12f, 3f, "#c9ad83", 1f);
        Rect(g, g.W * 0.75f - 6f, g.H * 0.12f, 12f, 3f, "#c9ad83", 1f);
        Grain(g, new[] { "#947853", "#7d6443" }, 300, 0.08f, 2869);
        } },
        ["funKitchenTop"] = new Spec { Unit = 0.55f, Draw = g => {
        Fill(g, "#caa06a");
        for (float y = 3f; y < g.H; y += 7f)
        {
        Line(g, 0f, y, g.W, y, "#d5ad77", 1f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#dbb684", 1f, 0.5f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#b18a56", 1f, 0.5f);
        Grain(g, new[] { "#d2a973", "#bf9560" }, 260, 0.07f, 2015);
        } },
        ["funKitchenMachine"] = new Spec { Unit = 0.80f, Detail = 1, Draw = g => {
        Fill(g, "#5f6e66");
        for (float y = 2f; y < g.H; y += 6f)
        {
        Line(g, 0f, y, g.W, y, "#6a7a71", 1f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#71817a", 1f, 0.6f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#4d5a53", 1f, 0.6f);
        // Wartungsklappe
        Rect(g, g.W * 0.18f, g.H * 0.30f, g.W * 0.64f, g.H * 0.34f, "#52615a", 1f);
        Line(g, g.W * 0.18f, g.H * 0.30f, g.W * 0.82f, g.H * 0.64f, "#46544d", 2f, 1f);
        Grain(g, new[] { "#68776f", "#55635b" }, 340, 0.08f, 9140);
        } },
        ["funKitchenGrill"] = new Spec { Unit = 0.60f, Draw = g => {
        Fill(g, "#3c3a38");
        for (float x = 0f; x < g.W; x += 12f)
        {
        Line(g, x, 0f, x, g.H, "#2e2c2b", 2f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#4a4845", 1f, 1f);
        // helle Oberkante
        // dunkler Fuss
        Rect(g, 0f, g.H * 0.88f, g.W, g.H * 0.12f, "#242220", 1f);
        Grain(g, new[] { "#444240", "#333130" }, 300, 0.09f, 9683);
        } },
        ["funKitchenBurner"] = new Spec { Unit = 0.30f, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   grd.addColorStop(0, '#ffd27a')
        //   grd.addColorStop(0.5, '#ff7f2a')
        //   grd.addColorStop(1, '#b52f08')
        Fill(g, "#c23c0e");
        Grain(g, new[] { "#c23c0e" }, 400, 0.06f, 4019);
        } },
        ["funKitchenDoor"] = new Spec { Unit = 1.00f, Detail = 1, Draw = g => {
        Fill(g, "#c1955a");
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#9c7542", 3f, 1f);
        // Mittelfuge der beiden Fluegel
        Line(g, 2f, 0f, 2f, g.H, "#d3a76b", 2f, 0.5f);
        Line(g, g.W - 3f, 0f, g.W - 3f, g.H, "#a87f49", 2f, 0.5f);
        Line(g, 0f, 2f, g.W, 2f, "#d3a76b", 2f, 1f);
        Line(g, 0f, g.H - 3f, g.W, g.H - 3f, "#966f3d", 2f, 1f);
        // Sichtritze paarweise, je Fluegel eine
        Rect(g, g.W * 0.30f - 3f, g.H * 0.42f, 6f, g.H * 0.16f, "#6b4d28", 1f);
        Rect(g, g.W * 0.70f - 3f, g.H * 0.42f, 6f, g.H * 0.16f, "#6b4d28", 1f);
        Grain(g, new[] { "#cb9f63", "#b48a51" }, 260, 0.08f, 117);
        } },
        ["funKitchenSand"] = new Spec { Unit = 2.20f, Draw = g => {
        var rnd = new Rng(5580);
        Fill(g, "#dfc4a0");
        Grain(g, new[] { "#e8cfab", "#d2b48d", "#cbb188" }, 900, 0.12f, 5580);
        for (float i = 0f; i < 26f; i += 1f)
        {
            Rect(g, rnd.Next() * g.W, rnd.Next() * g.H, 2f + rnd.Next() * 3f, 2f + rnd.Next() * 2f, "#f0dcc0", 0.18f);
        }
        } },
        ["funKitchenPath"] = new Spec { Unit = 1.60f, Draw = g => {
        Fill(g, "#ad7440");
        for (float y = 0f; y < g.H; y += 9f)
        {
        Line(g, 0f, y, g.W, y, "#9c6736", 1f, 1f);
        }
        Grain(g, new[] { "#b87c46", "#9f6939" }, 700, 0.13f, 2147);
        } },
        ["funKitchenJungle"] = new Spec { Unit = 2.00f, Draw = g => {
        var rnd = new Rng(242);
        Fill(g, "#35142a");
        Grain(g, new[] { "#2b1022", "#3f1831", "#452038" }, 1100, 0.16f, 242);
        for (float i = 0f; i < 30f; i += 1f)
        {
            float r = 2f + rnd.Next() * 5f;
            g.FillEllipse(rnd.Next() * g.W, rnd.Next() * g.H, r, r, C("#501937"), 0.22f);
        }
        for (float i = 0f; i < 18f; i += 1f)
        {
            Rect(g, rnd.Next() * g.W, rnd.Next() * g.H, 3f + rnd.Next() * 4f, 2f + rnd.Next() * 3f, "#1a0e19", 0.14f);
        }
        } },
        ["funKitchenFels"] = new Spec { Unit = 1.50f, Draw = g => {
        var rnd = new Rng(6420);
        Fill(g, "#140b14");
        Grain(g, new[] { "#1d111d", "#0f070f", "#241624" }, 800, 0.18f, 6420);
        for (float i = 0f; i < 12f; i += 1f)
        {
            Rect(g, rnd.Next() * g.W, rnd.Next() * g.H, 2f + rnd.Next() * 4f, 2f + rnd.Next() * 3f, "#3a2438", 0.10f);
        }
        } },
        // ---------------------------------------------------------------
        // aus surfaces_fungle_laboratory.js
        // ---------------------------------------------------------------
        ["funLabJungle"] = new Spec { Unit = 1.3f, Detail = 1, Draw = g => {
        Fill(g, "#401430");
        Grain(g, new[] { "#2c0d22", "#57203f", "#4a1a33", "#34102a" }, 900, 0.16f, 9722);
        } },
        ["funLabRock"] = new Spec { Unit = 1.1f, Detail = 1, Draw = g => {
        Fill(g, "#4c434c");
        Grain(g, new[] { "#3a333c", "#665963", "#54424e", "#443b45" }, 800, 0.18f, 13);
        } },
        ["funLabMoss"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        Fill(g, "#20241f");
        Grain(g, new[] { "#2c332b", "#181c17", "#252b24" }, 700, 0.20f, 4214);
        } },
        ["funLabMachine"] = new Spec { Unit = 0.42f, Draw = g => {
        Fill(g, "#69708a");
        for (float x = 0f; x <= g.W; x += MathF.Max(6f, g.W / 4f))
        {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   g.fillRect(x | 0, 0, 2, h)
        Fill(g, "#8a8a8a");
        Grain(g, new[] { "#8a8a8a" }, 400, 0.06f, 2067);
        }
        Grain(g, new[] { "#717893", "#5f6580" }, 250, 0.10f, 2067);
        } },
        ["funLabStem"] = new Spec { Unit = 0.4f, Draw = g => {
        Fill(g, "#a89d8e");
        Grain(g, new[] { "#b8ad9e", "#978c7d" }, 200, 0.14f, 1751);
        } },
        ["funLabCap"] = new Spec { Unit = 0.5f, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   grd.addColorStop(0, '#4d8f59')
        //   grd.addColorStop(0.55, '#33693e')
        //   grd.addColorStop(1, '#27512f')
        Fill(g, "#2f6539");
        Grain(g, new[] { "#2f6539" }, 400, 0.06f, 9901);
        } },
        ["funLabFloor"] = new Spec { Unit = 0.66f, Draw = g => {
        Fill(g, "#aebab9");
        // from #c8d3d2 at (-5.50,-8.80)
        // grout, from the same tile field
        for (float x = 0f; x <= g.W; x += g.W / 2f)
        {
            Line(g, x, 0f, x, g.H, "#9aa7a6", 2f, 1f);
        }
        for (float y = 0f; y <= g.H; y += g.H / 2f)
        {
            Line(g, 0f, y, g.W, y, "#9aa7a6", 2f, 1f);
        }
        Grain(g, new[] { "#b8c4c2", "#a2aeae" }, 350, 0.10f, 6754);
        } },
        ["funLabWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#55525b");
        // mid of #58545d at (-2.80,-7.40) and
        // #6b6e77 at (-6.50,-7.50), one step down
        // seams
        float n = 2f;
        for (float i = 1f; i < n; i += 1f)
        {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   g.fillRect((w * i / n) | 0, 0, 2, h)
        Fill(g, "#8a8a8a");
        Grain(g, new[] { "#8a8a8a" }, 400, 0.06f, 9425);
        }
        Rect(g, 0f, 0f, g.W, 2f, "#474450", 1f);
        Rect(g, 0f, g.H - 3f, g.W, 3f, "#474450", 1f);
        // faint upper-band sheen
        Rect(g, 0f, 0f, g.W, g.H * 0.35f, "#615e68", 0.18f);
        Grain(g, new[] { "#5d5a63", "#4c4952" }, 300, 0.10f, 9425);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_fungle_lookout.js
        // ---------------------------------------------------------------
        ["funLookoutDeck"] = new Spec { Unit = 2.2f, Draw = g => {
        var rnd = new Rng(100);
        Fill(g, "#2c403c");
        for (float i = 0f; i < 8f; i += 1f)
        {
            // weiche Steinflecken statt Plattengrenzen
            g.FillEllipse(rnd.Next() * g.W, rnd.Next() * g.H, 6f + rnd.Next() * 14f, 6f + rnd.Next() * 14f, C(i % 2 != 0f ? "#22332f" : "#314f47"), 0.14f);
        }
        Grain(g, new[] { "#243632", "#334a45", "#20302c" }, 1100, 0.16f, 100);
        } },
        ["funLookoutPlateau"] = new Spec { Unit = 2.6f, Draw = g => {
        Fill(g, "#374f49");
        Grain(g, new[] { "#2f4540", "#405c54", "#2a3d38" }, 1300, 0.15f, 2174);
        } },
        ["funLookoutSand"] = new Spec { Unit = 2.2f, Draw = g => {
        Fill(g, "#a06a3c");
        Grain(g, new[] { "#8f5d34", "#ad7645", "#855731" }, 1200, 0.16f, 4855);
        } },
        ["funLookoutSandPath"] = new Spec { Unit = 1.8f, Draw = g => {
        Fill(g, "#ab7240");
        Grain(g, new[] { "#9c6739", "#b87c49" }, 700, 0.13f, 3096);
        } },
        ["funLookoutRockEdge"] = new Spec { Unit = 2.4f, Draw = g => {
        Fill(g, "#2b423e");
        Grain(g, new[] { "#233733", "#334c47" }, 1000, 0.15f, 6221);
        } },
        ["funLookoutRock"] = new Spec { Unit = 1.6f, Draw = g => {
        Fill(g, "#170d16");
        Grain(g, new[] { "#120a11", "#1f121e", "#241523" }, 900, 0.18f, 6212);
        } },
        ["funLookoutBone"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#324a45");
        Grain(g, new[] { "#2b403c", "#3a544e" }, 700, 0.12f, 6796);
        float band = g.H / 3f;
        for (float y = 0f; y < g.H; y += band)
        {
            Rect(g, 0f, y + band - 3f, g.W, 3f, "#273b37", 1f);
            Rect(g, 0f, y + 2f, g.W, 2f, "#3d5852", 1f);
        }
        } },
        ["funLookoutBoneDark"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#1e2422");
        Grain(g, new[] { "#181d1b", "#252c29" }, 500, 0.12f, 1886);
        } },
        ["funLookoutWallIn"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#3e353e");
        // Strate aus der #552e3e-Familie
        Rect(g, 0f, g.H * 0.16f, g.W, g.H * 0.13f, "#4a303e", 0.55f);
        // dunkle Zwischenschicht
        Rect(g, 0f, g.H * 0.44f, g.W, g.H * 0.18f, "#312a33", 0.55f);
        // helle Bank Richtung #4c434c
        Rect(g, 0f, g.H * 0.74f, g.W, g.H * 0.09f, "#474049", 0.55f);
        Line(g, 0f, g.H * 0.16f, g.W, g.H * 0.16f, "#2b232c", 1f, 1f);
        Line(g, 0f, g.H * 0.29f, g.W, g.H * 0.29f, "#4f3a48", 1f, 1f);
        Line(g, 0f, g.H * 0.44f, g.W, g.H * 0.44f, "#282028", 1f, 1f);
        Line(g, 0f, g.H * 0.62f, g.W, g.H * 0.62f, "#453d46", 1f, 1f);
        Line(g, 0f, g.H * 0.74f, g.W, g.H * 0.74f, "#2b232c", 1f, 1f);
        Grain(g, new[] { "#463d46", "#342c35" }, 700, 0.14f, 8492);
        } },
        ["funLookoutDoor"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#3f5a54");
        Grain(g, new[] { "#36504a", "#48645d" }, 500, 0.12f, 2642);
        Rect(g, 0f, g.H / 2f - 1f, g.W, 2f, "#2b403c", 1f);
        } },
        ["funLookoutGrowth"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#8a4269");
        Grain(g, new[] { "#7a3859", "#9a4f78" }, 600, 0.15f, 5763);
        } },
        ["funLookoutJaw"] = new Spec { Unit = 1.3f, Detail = 1, Draw = g => {
        Fill(g, "#663b47");
        Grain(g, new[] { "#59323d", "#734451" }, 600, 0.14f, 3311);
        } },
        ["funLookoutTooth"] = new Spec { Unit = 1.1f, Detail = 1, Draw = g => {
        Fill(g, "#b3a88c");
        Grain(g, new[] { "#a3987c", "#c1b69a" }, 500, 0.12f, 5139);
        } },
        ["funLookoutCrystal"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#7a1330");
        Grain(g, new[] { "#660f27", "#932040" }, 600, 0.16f, 6751);
        Rect(g, 0f, 0f, g.W * 0.18f, g.H, "#b02a4c", 1f);
        Rect(g, g.W * 0.55f, 0f, g.W * 0.12f, g.H, "#b02a4c", 1f);
        } },
        ["funLookoutMetal"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#39343c");
        // Strate aus der #552e3e-Familie
        Rect(g, 0f, g.H * 0.30f, g.W, g.H * 0.12f, "#443039", 0.5f);
        Rect(g, 0f, g.H * 0.58f, g.W, g.H * 0.16f, "#2e2a31", 0.5f);
        // helle Bank Richtung #4c434c
        Rect(g, 0f, g.H * 0.82f, g.W, g.H * 0.07f, "#423d44", 0.5f);
        Line(g, 0f, g.H * 0.30f, g.W, g.H * 0.30f, "#272329", 1f, 1f);
        Line(g, 0f, g.H * 0.58f, g.W, g.H * 0.58f, "#262228", 1f, 1f);
        Grain(g, new[] { "#3f3941", "#2d282f" }, 600, 0.14f, 2883);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_fungle_meetingroom.js
        // ---------------------------------------------------------------
        ["funMeetingPath"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        Fill(g, "#b47743");
        for (float x = 0f; x < g.W; x += 7f)
        {
        Line(g, x, 0f, x, g.H, "#a06a3c", 2f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#c4854e", 2f, 1f);
        Grain(g, new[] { "#bd7f4a", "#a86e3e" }, 500, 0.14f, 1921);
        } },
        ["funMeetingMound"] = new Spec { Unit = 9.0f, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   { for (const r of [0.95, 0.72, 0.5])
        Fill(g, "#d0b694");
        Grain(g, new[] { "#d0b694" }, 400, 0.06f, 4918);
        } },
        ["funMeetingWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#3b3433");
        for (float y = 0f; y <= g.H; y += g.H / 3f)
        {
            Line(g, 0f, y, g.W, y, "#2c2626", 2f, 1f);
        }
        for (float i = 0f; i < 4f; i += 1f)
        {
            float x = g.W * (0.2f + 0.25f * i);
            Line(g, x, (i % 2f) * g.H / 3f, x, (i % 2f) * g.H / 3f + g.H / 3f, "#2c2626", 2f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#4c4440", 2f, 1f);
        // helle Oberkante der Zeichnung
        Grain(g, new[] { "#443c3a", "#332d2c" }, 400, 0.12f, 9092);
        } },
        ["funMeetingWallCap"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#4c4544");
        Grain(g, new[] { "#564e4c", "#423b3a" }, 400, 0.14f, 8086);
        } },
        ["funMeetingJungleBank"] = new Spec { Unit = 1.3f, Detail = 1, Draw = g => {
        Fill(g, "#481a2b");
        Grain(g, new[] { "#54203a", "#3c1424", "#5c2842" }, 700, 0.16f, 5436);
        } },
        ["funMeetingRock"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        Fill(g, "#ac8a5c");
        Grain(g, new[] { "#bc9a6a", "#98764c", "#c4a274" }, 600, 0.18f, 9072);
        Line(g, 0f, g.H * 0.35f, g.W * 0.5f, g.H * 0.3f, "#8f7048", 2f, 1f);
        Line(g, g.W * 0.5f, g.H * 0.65f, g.W, g.H * 0.55f, "#8f7048", 2f, 1f);
        } },
        ["funMeetingMetal"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#5c5b68");
        Line(g, g.W * 0.15f, g.H * 0.2f, g.W * 0.8f, g.H * 0.75f, "#4a4956", 2f, 1f);
        Line(g, g.W * 0.6f, g.H * 0.15f, g.W * 0.9f, g.H * 0.5f, "#6c6b7a", 1f, 1f);
        Grain(g, new[] { "#64636f", "#504f5c" }, 300, 0.12f, 8907);
        } },
        ["funMeetingSlab"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#2a2830");
        Line(g, 0f, 1f, g.W, 1f, "#3e3c46", 2f, 1f);
        Grain(g, new[] { "#322f3a", "#24222a" }, 300, 0.14f, 6320);
        } },
        ["funMeetingPlatform"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#425f48");
        Grain(g, new[] { "#4c6b52", "#38523e" }, 350, 0.14f, 5515);
        } },
        ["funMeetingCrate"] = new Spec { Unit = 0.6f, Draw = g => {
        Fill(g, "#426e96");
        for (float x = 0f; x < g.W; x += 10f)
        {
        Line(g, x, 0f, x, g.H, "#386084", 2f, 1f);
        }
        for (float y = 0f; y < g.H; y += 10f)
        {
        Line(g, 0f, y, g.W, y, "#386084", 2f, 1f);
        }
        } },
        ["funMeetingCabinet"] = new Spec { Unit = 0.7f, Draw = g => {
        Fill(g, "#6e422b");
        for (float i = 1f; i < 4f; i += 1f)
        {
        Line(g, 0f, g.H * i / 4f, g.W, g.H * i / 4f, "#54331f", 2f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#82523a", 2f, 1f);
        Grain(g, new[] { "#784a32", "#613a24" }, 300, 0.12f, 7476);
        } },
        ["funMeetingConch"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#c892a2");
        for (float i = 0f; i < 4f; i += 1f)
        {
            g.StrokeEllipse(g.W * 0.35f, g.H * 0.55f, g.H * (0.12f + i * 0.11f), g.H * (0.12f + i * 0.11f), 2f, C("#b07a8c"), 1f);
        }
        Grain(g, new[] { "#d4a0b0", "#ba8494" }, 200, 0.12f, 1755);
        } },
        ["funMeetingFloor"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#ddc2a2");
        Grain(g, new[] { "#ecd6b4", "#e3c6a2", "#d5b996", "#cbaf8c" }, 700, 0.13f, 8308);
        // Wellen-Andeutung, KEINE Fugenlinien
        for (float i = 0f; i < 2f; i += 1f)
        {
            g.StrokeEllipse(g.W * (0.35f + 0.3f * i), g.H * (0.32f + 0.38f * i), g.W * (0.4f + 0.15f * i), g.W * (0.4f + 0.15f * i), MathF.Max(4f, g.W * 0.07f), C("#f2ddb9"), 0.08f);
        }
        } },
        // ---------------------------------------------------------------
        // aus surfaces_fungle_miningpit.js
        // ---------------------------------------------------------------
        ["funMiningpitFloor"] = new Spec { Unit = 2.0f, Draw = g => {
        var rnd = new Rng(1639);
        Fill(g, "#b98a4e");
        // from #d7a259 at (12.00,8.00)
        for (float y = 5f; y < g.H; y += 13f)
        {
        Line(g, 0f, y, g.W, y, "#a8763e", 2f, 1f);
        }
        // Schrammen (#c8854b)
        for (float i = 0f; i < 6f; i += 1f)
        {
            // dunkle Trittflecken
            Rect(g, rnd.Next() * g.W, rnd.Next() * g.H, 8f + rnd.Next() * 20f, 3f + rnd.Next() * 5f, "#9c6c38", 0.20f);
        }
        Grain(g, new[] { "#c4975c", "#a87c44", "#d1a468" }, 800, 0.14f, 1639);
        } },
        ["funMiningpitPlateau"] = new Spec { Unit = 2.2f, Draw = g => {
        var rnd = new Rng(1243);
        Fill(g, "#3a544c");
        // from #44635b at (14.00,5.00)
        Grain(g, new[] { "#44635b", "#31473f", "#3c584f" }, 1000, 0.20f, 1243);
        for (float i = 0f; i < 10f; i += 1f)
        {
            // dunkle Flecken (Moos/Stein durch)
            Rect(g, rnd.Next() * g.W, rnd.Next() * g.H, 6f + rnd.Next() * 16f, 4f + rnd.Next() * 8f, "#263832", 0.14f);
        }
        } },
        ["funMiningpitRock"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        var rnd = new Rng(3804);
        Fill(g, "#9c5c12");
        // from #b96e15 at (10.20,6.10)
        for (float i = 0f; i < 16f; i += 1f)
        {
            // Steinpackung
            g.FillEllipse(rnd.Next() * g.W, rnd.Next() * g.H, 3f + rnd.Next() * 7f, 3f + rnd.Next() * 7f, C(i % 3 == 0 ? "#a86e2e" : (i % 3 == 1 ? "#8e520e" : "#b0763a")), 0.55f);
        }
        for (float i = 0f; i < 8f; i += 1f)
        {
            // Fugen-Schatten
            g.StrokeEllipse(rnd.Next() * g.W, rnd.Next() * g.H, 4f + rnd.Next() * 6f, 4f + rnd.Next() * 6f, 1.5f, C("#6e3e0a"), 0.3f);
        }
        Grain(g, new[] { "#a86a20", "#8a500e" }, 500, 0.14f, 3804);
        } },
        ["funMiningpitRockDark"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        var rnd = new Rng(3139);
        Fill(g, "#4e4960");
        // from #605b6d at (13.15,8.45)
        for (float i = 0f; i < 9f; i += 1f)
        {
            // Facetten / Bruchflaechen
            float fx = rnd.Next() * g.W, fy = rnd.Next() * g.H, s = 6f + rnd.Next() * 14f;
            g.FillQuad(fx, fy, fx + s, fy + s * 0.5f, fx + s * 0.2f, fy + s, fx + s * 0.2f, fy + s, C(i % 2 != 0f ? "#5a5470" : "#3e3950"), 0.4f);
        }
        for (float i = 0f; i < 5f; i += 1f)
        {
        Line(g, rnd.Next() * g.W, rnd.Next() * g.H, rnd.Next() * g.W, rnd.Next() * g.H, "#322e44", 1.5f, 1f);
        }
        // Risse
        Grain(g, new[] { "#564f68", "#443e58" }, 400, 0.14f, 3139);
        } },
        ["funMiningpitCliffFace"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        var rnd = new Rng(1689);
        Fill(g, "#9c5c12");
        // from #b96e15 at (13.00,-3.00)
        for (float y = 6f; y < g.H; y += 14f)
        {
            // Straten
            // from #c8854b at (13.00,-2.20)
            Rect(g, 0f, y, g.W, 5f, "#b0763a", 0.5f);
            Rect(g, 0f, y + 5f, g.W, 2f, "#7a460c", 0.5f);
        }
        for (float i = 0f; i < 10f; i += 1f)
        {
        Line(g, rnd.Next() * g.W, 0f, rnd.Next() * g.W, g.H, "#6e3e0a", 1.5f, 1f);
        }
        // Runsrillen
        Grain(g, new[] { "#a8681e", "#8a500e" }, 600, 0.16f, 1689);
        } },
        ["funMiningpitCliffBack"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        var rnd = new Rng(2510);
        Fill(g, "#8a4c0a");
        // from #b36110 at (12.00,12.60)
        for (float i = 0f; i < 8f; i += 1f)
        {
            // dunkle Schattenfleckn
            Rect(g, rnd.Next() * g.W, rnd.Next() * g.H, 10f + rnd.Next() * 24f, 6f + rnd.Next() * 12f, "#5e3206", 0.4f);
        }
        Grain(g, new[] { "#7a440a", "#9a560e" }, 500, 0.18f, 2510);
        } },
        ["funMiningpitDoorLeaf"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#b8894c");
        // from #d7a259 at (24.07,11.62)
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#6e4e24", 3f, 1f);
        // Blattfuge
        Line(g, 2f, 0f, 2f, g.H, "#caa05e", 2f, 1f);
        Line(g, g.W - 2f, 0f, g.W - 2f, g.H, "#8e6634", 2f, 1f);
        for (float y = 6f; y < g.H; y += 10f)
        {
        Line(g, 4f, y, g.W - 4f, y, "#966c38", 1.5f, 1f);
        }
        Grain(g, new[] { "#c2955a", "#a87c44" }, 300, 0.12f, 6530);
        } },
        ["funMiningpitWood"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#6e523e");
        // from #83624a at (10.80,8.90)
        for (float y = 3f; y < g.H; y += 7f)
        {
        Line(g, 0f, y, g.W, y, "#5a4232", 1.5f, 1f);
        }
        // Maserung
        Line(g, 1f, 0f, 1f, g.H, "#82624a", 2f, 1f);
        // helle Kante (Messwert)
        Grain(g, new[] { "#7a5c46", "#604634" }, 350, 0.14f, 5409);
        } },
        ["funMiningpitWoodDark"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#2a1e20");
        // from #312325 at (12.70,6.30)
        for (float y = 2f; y < g.H; y += 6f)
        {
        Line(g, 0f, y, g.W, y, "#1e1517", 1.5f, 1f);
        }
        Grain(g, new[] { "#322428", "#241a1c" }, 300, 0.14f, 4530);
        } },
        ["funMiningpitMachineDark"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#1c0f0e");
        // from #201110 at (10.90,8.00)
        Line(g, 2f, 0f, 2f, g.H, "#33201e", 2f, 1f);
        Line(g, 0f, 2f, g.W, 2f, "#33201e", 2f, 1f);
        for (float y = 8f; y < g.H; y += 12f)
        {
        Line(g, 0f, y, g.W, y, "#120808", 2f, 1f);
        }
        // Blechfugen
        Grain(g, new[] { "#241412", "#140a09" }, 350, 0.16f, 2153);
        } },
        ["funMiningpitShadowPit"] = new Spec { Unit = 2.0f, Draw = g => {
        Fill(g, "#97744a");
        // geschaetzt: #d7a259 an (12.0,8.0) abgedunkelt
        Grain(g, new[] { "#8a683f", "#a37f52" }, 500, 0.18f, 4519);
        } },
        ["funMiningpitShadowGreen"] = new Spec { Unit = 2.2f, Draw = g => {
        Fill(g, "#2c3f38");
        // geschaetzt: #44635b an (14.0,5.0) abgedunkelt
        Grain(g, new[] { "#253630", "#33483f" }, 500, 0.18f, 2667);
        } },
        ["funMiningpitGlowVein"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#2a0f0c");
        // geschaetzt (Sprite-Rand)
        // geschaetzt (Sprite-Kern)
        for (float i = 0f; i < 5f; i += 1f)
        {
            float x = g.W * (0.12f + 0.18f * i);
            Rect(g, x, g.H * 0.1f, 2.5f, g.H * 0.8f, "#ff5a48", 1f);
            // Strahlen
        }
        for (float i = 0f; i < 5f; i += 1f)
        {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   g.fillRect(w * (0.14 + 0.18 * i), h * 0.2, 1, h * 0.6)
        Fill(g, "#8a8a8a");
        Grain(g, new[] { "#8a8a8a" }, 400, 0.06f, 2022);
        }
        } },
        ["funMiningpitSpinels"] = new Spec { Unit = 0.6f, Draw = g => {
        var rnd = new Rng(4625);
        Fill(g, "#3c1214");
        // from #491717 at (14.35,9.10)
        for (float i = 0f; i < 12f; i += 1f)
        {
            // Nieren
            g.FillEllipse(rnd.Next() * g.W, rnd.Next() * g.H, 2f + rnd.Next() * 3.5f, 2f + rnd.Next() * 3.5f, C(i % 2 != 0f ? "#52191c" : "#2e0d10"), 0.6f);
        }
        // Glanzpunkte (Sprite)
        for (float i = 0f; i < 6f; i += 1f)
        {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   g.fillRect(rnd.Next() * w, rnd.Next() * h, 1.5, 1.5)
        Fill(g, "#8a8a8a");
        Grain(g, new[] { "#8a8a8a" }, 400, 0.06f, 4625);
        }
        } },
        ["funMiningpitCartIron"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#3c4440");
        // geschaetzt (Loren-Sprite)
        Line(g, 0f, 2f, g.W, 2f, "#4e5751", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#2c322f", 2f, 1f);
        for (float x = 8f; x < g.W; x += 12f)
        {
        Line(g, x, 4f, x, g.H - 4f, "#2a302d", 1.5f, 1f);
        }
        Grain(g, new[] { "#444c48", "#343c38" }, 300, 0.14f, 7770);
        } },
        ["funMiningpitOreDark"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#33261e");
        // geschaetzt (MineOres-Sprite Karre)
        Line(g, 0f, 2f, g.W, 2f, "#453428", 2f, 1f);
        for (float y = 8f; y < g.H; y += 10f)
        {
        Line(g, 0f, y, g.W, y, "#241a14", 2f, 1f);
        }
        Grain(g, new[] { "#3b2c22", "#2b201a" }, 300, 0.14f, 2258);
        } },
        ["funMiningpitOreGold"] = new Spec { Unit = 0.6f, Draw = g => {
        var rnd = new Rng(8698);
        Fill(g, "#8e6e24");
        // geschaetzt (Nuggit-Farbe)
        for (float i = 0f; i < 14f; i += 1f)
        {
            // Nuggite
            g.FillEllipse(rnd.Next() * g.W, rnd.Next() * g.H, 2f + rnd.Next() * 3f, 2f + rnd.Next() * 3f, C(i % 2 != 0f ? "#a8822c" : "#755a1e"), 0.7f);
        }
        // Glanz
        for (float i = 0f; i < 6f; i += 1f)
        {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   g.fillRect(rnd.Next() * w, rnd.Next() * h, 1.5, 1.5)
        Fill(g, "#8a8a8a");
        Grain(g, new[] { "#8a8a8a" }, 400, 0.06f, 8698);
        }
        } },
        ["funMiningpitCrystalRed"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        var rnd = new Rng(9884);
        Fill(g, "#c22f42");
        // from #bf372a at (15.30,2.60)
        for (float i = 0f; i < 8f; i += 1f)
        {
            // Facetten
            // from (15.00,2.60) / (14.30,2.40) abgedunkelt
            float fx = rnd.Next() * g.W, fy = rnd.Next() * g.H, s = 5f + rnd.Next() * 12f;
            g.FillQuad(fx, fy, fx + s * 0.4f, fy + s, fx + s, fy + s * 0.3f, fx + s, fy + s * 0.3f, C(i % 2 != 0f ? "#f47f99" : "#8e1e30"), 0.5f);
        }
        // helle Kante (Messwert)
        Line(g, 1f, 1f, 1f+g.W - 2f, 1f, "#f47f99", 2f, 1f);
        Line(g, 1f, 1f+g.H - 2f, 1f+g.W - 2f, 1f+g.H - 2f, "#f47f99", 2f, 1f);
        Line(g, 1f, 1f, 1f, 1f+g.H - 2f, "#f47f99", 2f, 1f);
        Line(g, 1f+g.W - 2f, 1f, 1f+g.W - 2f, 1f+g.H - 2f, "#f47f99", 2f, 1f);
        Grain(g, new[] { "#d04a58", "#a02436" }, 300, 0.14f, 9884);
        } },
        ["funMiningpitBoulderGreen"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        var rnd = new Rng(1714);
        Fill(g, "#546654");
        // from #677c69 at (15.30,7.90)
        for (float i = 0f; i < 10f; i += 1f)
        {
            // Flechten-Flecken
            g.FillEllipse(rnd.Next() * g.W, rnd.Next() * g.H, 3f + rnd.Next() * 6f, 3f + rnd.Next() * 6f, C(i % 2 != 0f ? "#677c69" : "#42523f"), 0.45f);
        }
        Grain(g, new[] { "#5e7260", "#48584a" }, 400, 0.14f, 1714);
        } },
        ["funMiningpitCrateGrey"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        Fill(g, "#4e5860");
        // geschaetzt
        Line(g, 1f, 1f, 1f+g.W - 2f, 1f, "#39424a", 2f, 1f);
        Line(g, 1f, 1f+g.H - 2f, 1f+g.W - 2f, 1f+g.H - 2f, "#39424a", 2f, 1f);
        Line(g, 1f, 1f, 1f, 1f+g.H - 2f, "#39424a", 2f, 1f);
        Line(g, 1f+g.W - 2f, 1f, 1f+g.W - 2f, 1f+g.H - 2f, "#39424a", 2f, 1f);
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#39424a", 2f, 1f);
        Grain(g, new[] { "#58626a", "#434c54" }, 250, 0.12f, 7324);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_fungle_reactor.js
        // ---------------------------------------------------------------
        ["funReactorFloor"] = new Spec { Unit = 1.3f, Detail = 1, Draw = g => {
        Fill(g, "#472635");
        // aus #542e3e @(21,-7)
        Grain(g, new[] { "#54303f", "#3c1f2c" }, 800, 0.13f, 9704);
        for (float y = 0f; y < g.H; y += g.H / 2f)
        {
        Line(g, 0f, y, g.W, y, "#341b28", 2f, 1f);
        }
        for (float x = 0f; x < g.W; x += g.W / 2f)
        {
        Line(g, x, 0f, x, g.H, "#341b28", 2f, 1f);
        }
        } },
        ["funReactorWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#6b3c4a");
        // aus #7c4656 @(19.25,-7)
        Line(g, 0f, 2f, g.W, 2f, "#7d4756", 2f, 1f);
        // hellere Oberkante wie gezeichnet
        Line(g, 0f, g.H - 3f, g.W, g.H - 3f, "#52303b", 2f, 1f);
        Grain(g, new[] { "#784553", "#5d3541" }, 500, 0.12f, 3211);
        } },
        ["funReactorJungle"] = new Spec { Unit = 1.4f, Detail = 1, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   jungleSkin(g, w, h, '#442334', ['#502a3d', '#3a1c2b', '#5730
        Fill(g, "#442334");
        Grain(g, new[] { "#442334" }, 400, 0.06f, 7695);
        } },
        ["funReactorJungleDark"] = new Spec { Unit = 1.4f, Detail = 1, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   jungleSkin(g, w, h, '#2a1223', ['#33172a', '#220e1d', '#3b1c
        Fill(g, "#2a1223");
        Grain(g, new[] { "#2a1223" }, 400, 0.06f, 2941);
        } },
        ["funReactorFels"] = new Spec { Unit = 1.5f, Draw = g => {
        var rnd = new Rng(1914);
        Fill(g, "#150b14");
        // aus #1a0e19
        Grain(g, new[] { "#241225", "#100710" }, 600, 0.15f, 1914);
        for (float i = 0f; i < 10f; i += 1f)
        {
            Line(g, rnd.Next() * g.W, rnd.Next() * g.H, rnd.Next() * g.W, rnd.Next() * g.H, "#2c1630", 2f, 0.35f);
        }
        } },
        ["funReactorCliff"] = new Spec { Unit = 1.5f, Draw = g => {
        Fill(g, "#22201f");
        // aus #272220 @(20,-5.6)
        Line(g, 0f, 3f, g.W, 3f, "#96603a", 4f, 1f);
        // aus #ac6e3e @(17.06,-5.39)
        for (float y = 12f; y < g.H; y += 18f)
        {
        Line(g, 0f, y, g.W, y, "#191716", 2f, 1f);
        }
        for (float x = 6f; x < g.W; x += 30f)
        {
        Line(g, x, 6f, x, g.H, "#191716", 2f, 1f);
        }
        Grain(g, new[] { "#2c2827", "#181514" }, 500, 0.14f, 3856);
        } },
        ["funReactorMachine"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#54565f");
        // aus #606271 @(19.45,-6)
        for (float y = 0f; y < g.H; y += 20f)
        {
        Line(g, 0f, y, g.W, y, "#43454d", 2f, 1f);
        }
        for (float x = 0f; x < g.W; x += 26f)
        {
        Line(g, x, 0f, x, g.H, "#43454d", 2f, 1f);
        }
        for (float y = 8f; y < g.H; y += 20f)
        {
            for (float x = 5f; x < g.W; x += 26f)
            {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   g.fillRect(x, y, 2, 2)
        Fill(g, "#8a8a8a");
        Grain(g, new[] { "#8a8a8a" }, 400, 0.06f, 4653);
            }
        }
        Line(g, 0f, 1f, g.W, 1f, "#6d707a", 2f, 1f);
        Grain(g, new[] { "#5c5f68", "#4a4c54" }, 400, 0.11f, 4653);
        } },
        ["funReactorMachineDark"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#383a41");
        // aus #464453
        Line(g, 0f, 1f, g.W, 1f, "#4b4d55", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#282a30", 2f, 1f);
        Grain(g, new[] { "#414349", "#303237" }, 350, 0.12f, 4449);
        } },
        ["funReactorCoreGlow"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   g.bezierCurveTo(w * 0.3, y - 5, w * 0.6, y + 5, w, y)
        Fill(g, "#0d3552");
        Grain(g, new[] { "#0d3552" }, 400, 0.06f, 8692);
        } },
        ["funReactorMushCap"] = new Spec { Unit = 0.6f, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   grd.addColorStop(0, '#6fcfc7')
        //   grd.addColorStop(0.55, '#3fa49f')
        //   grd.addColorStop(1, '#2b7674')
        Fill(g, "#3fa49f");
        Grain(g, new[] { "#3fa49f" }, 400, 0.06f, 271);
        } },
        ["funReactorStairTop"] = new Spec { Unit = 0.45f, Draw = g => {
        Fill(g, "#120a11");
        for (float y = g.H * 0.30f; y < g.H; y += g.H * 0.24f)
        {
        Line(g, 0f, y, g.W, y, "#0a050a", 2f, 1f);
        }
        // Abnutzung an der Trittkante (sued)
        Rect(g, 0f, g.H * 0.06f, g.W, MathF.Max(2f, g.H * 0.10f), "#3a2129", 1f);
        Rect(g, 0f, g.H * 0.17f, g.W, MathF.Max(1f, g.H * 0.05f), "#241318", 1f);
        Grain(g, new[] { "#1d1016", "#0d070c" }, 350, 0.14f, 7508);
        } },
        ["funReactorStairEdge"] = new Spec { Unit = 0.45f, Draw = g => {
        Fill(g, "#7d5638");
        // aus #ac6e3e @(17.06,-5.39)
        Line(g, 0f, 1f, g.W, 1f, "#96683f", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#5d3f28", 2f, 1f);
        Grain(g, new[] { "#8a603d", "#6b492f" }, 250, 0.13f, 3375);
        } },
        ["funReactorCrystal"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        var rnd = new Rng(4135);
        Fill(g, "#7f4257");
        // aus #9b5068 @(15.91,-7.29)
        for (float i = 0f; i < 8f; i += 1f)
        {
            float x = rnd.Next() * g.W;
            Line(g, x, 0f, x + (rnd.Next() - 0.5f) * g.W * 0.6f, g.H, i % 2 != 0f ? "#9b5068" : "#66334a", 3f, 0.7f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#b26479", 2f, 1f);
        Grain(g, new[] { "#8d4a60", "#6d384c" }, 300, 0.14f, 4135);
        } },
        ["funReactorPlate"] = new Spec { Unit = 1.1f, Detail = 1, Draw = g => {
        Fill(g, "#92687f");
        // aus #ac7991 @(17.47,-13.67)
        Line(g, 2f, 2f, 2f+g.W - 4f, 2f, "#7a5266", 2f, 1f);
        Line(g, 2f, 2f+g.H - 4f, 2f+g.W - 4f, 2f+g.H - 4f, "#7a5266", 2f, 1f);
        Line(g, 2f, 2f, 2f, 2f+g.H - 4f, "#7a5266", 2f, 1f);
        Line(g, 2f+g.W - 4f, 2f, 2f+g.W - 4f, 2f+g.H - 4f, "#7a5266", 2f, 1f);
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#7a5266", 2f, 1f);
        Grain(g, new[] { "#a07489", "#835c71", "#6d4a5e" }, 450, 0.16f, 5769);
        } },
        ["funReactorRail"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#603041");
        // aus #753b50 @(16.39,-9.21)
        Line(g, 0f, 1f, g.W, 1f, "#7d4056", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#4a2434", 2f, 1f);
        Grain(g, new[] { "#6d3849", "#512938" }, 250, 0.12f, 4846);
        } },
        ["funReactorDoor"] = new Spec { Unit = 1.1f, Detail = 1, Draw = g => {
        Fill(g, "#bd8c4e");
        // aus #d7a259 (comms-Tuerpanel)
        Line(g, 3f, 3f, 3f+g.W - 6f, 3f, "#8f6435", 4f, 1f);
        Line(g, 3f, 3f+g.H - 6f, 3f+g.W - 6f, 3f+g.H - 6f, "#8f6435", 4f, 1f);
        Line(g, 3f, 3f, 3f, 3f+g.H - 6f, "#8f6435", 4f, 1f);
        Line(g, 3f+g.W - 6f, 3f, 3f+g.W - 6f, 3f+g.H - 6f, "#8f6435", 4f, 1f);
        Line(g, g.W / 2f, 4f, g.W / 2f, g.H - 4f, "#8f6435", 3f, 1f);
        Rect(g, g.W * 0.42f, g.H * 0.08f, g.W * 0.16f, g.H * 0.05f, "#57e6c8", 1f);
        Grain(g, new[] { "#c99a59", "#a87c42" }, 350, 0.12f, 3326);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_fungle_recroom.js
        // ---------------------------------------------------------------
        ["funRecSandWet"] = new Spec { Unit = 1.3f, Detail = 1, Draw = g => {
        Fill(g, "#d0af88");
        // geschaetzt off the wet band at (-24.2, 0.0)
        Grain(g, new[] { "#dcc0a0", "#c0a078" }, 800, 0.16f, 4371);
        for (float y = 4f; y < g.H; y += 14f)
        {
        Line(g, 0f, y, g.W, y, "#b89a72", 2f, 1f);
        }
        } },
        ["funRecPath"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        var rnd = new Rng(9122);
        Fill(g, "#b5763f");
        // from #c8854b
        Grain(g, new[] { "#c2854e", "#a56a36" }, 700, 0.15f, 9122);
        // pressed-in pebbles
        for (float i = 0f; i < 24f; i += 1f)
        {
            Rect(g, rnd.Next() * g.W, rnd.Next() * g.H, 2f, 2f, "#9a6232", 0.18f);
        }
        } },
        ["funRecMoss"] = new Spec { Unit = 1.1f, Detail = 1, Draw = g => {
        var rnd = new Rng(3694);
        Fill(g, "#5d7d66");
        // from #6a8c72
        for (float i = 0f; i < 90f; i += 1f)
        {
            // from #779884 / darker mottle
            Rect(g, rnd.Next() * g.W, rnd.Next() * g.H, 3f + rnd.Next() * 8f, 2f + rnd.Next() * 5f, i % 2 != 0f ? "#6b8f76" : "#51705a", 0.35f);
        }
        Grain(g, new[] { "#68896f", "#4d6b55" }, 500, 0.12f, 3694);
        } },
        ["funRecWood"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#7a5648");
        // from #8b6454
        for (float x = 0f; x < g.W; x += 22f)
        {
        Line(g, x, 0f, x, g.H, "#63443a", 2f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#8a6552", 2f, 1f);
        Grain(g, new[] { "#835e4c", "#6b4a3e" }, 400, 0.12f, 9406);
        } },
        ["funRecWoodDark"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#3d3835");
        // from #49423f
        for (float x = 0f; x < g.W; x += 26f)
        {
        Line(g, x, 0f, x, g.H, "#2e2a28", 2f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#4a443f", 2f, 1f);
        Grain(g, new[] { "#45403b", "#332e2c" }, 400, 0.12f, 2288);
        } },
        ["funRecBarFace"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#8a9899");
        // from #9aa8a9
        for (float x = 0f; x < g.W; x += 18f)
        {
        Line(g, x, 0f, x, g.H, "#768486", 2f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#9aa8a8", 2f, 1f);
        Grain(g, new[] { "#93a1a1", "#7d8b8d" }, 350, 0.10f, 9486);
        } },
        ["funRecBarCount"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#98a6a7");
        // from #9aa8a9 (top reads lighter in the zoom)
        for (float y = 6f; y < g.H; y += 15f)
        {
        Line(g, 0f, y, g.W, y, "#879596", 2f, 1f);
        }
        Grain(g, new[] { "#a2b0b0", "#8a9899" }, 350, 0.10f, 9260);
        } },
        ["funRecRoof"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#8395a0");
        // geschaetzt from the roof band at (-16.50, 1.40)
        for (float x = 0f; x < g.W; x += 24f)
        {
        Line(g, x, 0f, x, g.H, "#6f818c", 2f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#94a6b1", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#647681", 2f, 1f);
        Grain(g, new[] { "#8d9fab", "#758794" }, 350, 0.10f, 5235);
        } },
        ["funRecPoolFelt"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#34506c");
        // geschaetzt off the table centre (-16.6,-2.5)
        Grain(g, new[] { "#3c5a78", "#2c465e" }, 400, 0.12f, 9690);
        } },
        ["funRecPoolRim"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#5f7d98");
        // geschaetzt off the table rim (-16.2,-2.3)
        Line(g, 0f, 2f, g.W, 2f, "#708ea8", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#4e6a84", 2f, 1f);
        Grain(g, new[] { "#6a88a2", "#54708a" }, 350, 0.10f, 4265);
        } },
        ["funRecPurple"] = new Spec { Unit = 0.6f, Draw = g => {
        Fill(g, "#7a4064");
        // geschaetzt off the crate sprite
        Line(g, 3f, 3f, 3f+g.W - 6f, 3f, "#4a2440", 5f, 1f);
        Line(g, 3f, 3f+g.H - 6f, 3f+g.W - 6f, 3f+g.H - 6f, "#4a2440", 5f, 1f);
        Line(g, 3f, 3f, 3f, 3f+g.H - 6f, "#4a2440", 5f, 1f);
        Line(g, 3f+g.W - 6f, 3f, 3f+g.W - 6f, 3f+g.H - 6f, "#4a2440", 5f, 1f);
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#4a2440", 4f, 1f);
        Grain(g, new[] { "#87496f", "#6b3856" }, 400, 0.12f, 1001);
        } },
        ["funRecFlamingo"] = new Spec { Unit = 0.6f, Draw = g => {
        Fill(g, "#cc5050");
        // geschaetzt off the float sprite
        Grain(g, new[] { "#d85c5a", "#bc4646" }, 350, 0.12f, 9237);
        } },
        ["funRecDebris"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        Fill(g, "#7a8997");
        // from #8897a7
        for (float y = 8f; y < g.H; y += 20f)
        {
        Line(g, 0f, y, g.W, y, "#667482", 2f, 1f);
        }
        for (float x = 0f; x < g.W; x += 26f)
        {
        Line(g, x, 0f, x, g.H, "#667482", 2f, 1f);
        }
        Grain(g, new[] { "#84929f", "#6c7a88" }, 400, 0.12f, 2360);
        } },
        ["funRecCastle"] = new Spec { Unit = 0.6f, Draw = g => {
        Fill(g, "#e0c6a0");
        // from #f8deb9
        Grain(g, new[] { "#ead0aa", "#d2b890" }, 500, 0.15f, 1509);
        } },
        ["funRecVent"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#5a2418");
        // from #672b1c
        Line(g, 0f, 1f, g.W, 1f, "#6b2f1f", 2f, 1f);
        Grain(g, new[] { "#652a1c", "#4c1d13" }, 300, 0.12f, 7824);
        } },
        ["funRecTrash"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#2f5c36");
        // geschaetzt (sprite green, atlas shade #264128)
        Line(g, 0f, 2f, g.W, 2f, "#3d7044", 2f, 1f);
        for (float y = 8f; y < g.H; y += 12f)
        {
        Line(g, 0f, y, g.W, y, "#26482c", 1f, 1f);
        }
        Grain(g, new[] { "#38663e", "#284e2e" }, 300, 0.12f, 7589);
        } },
        ["funRecBench"] = new Spec { Unit = 0.7f, Draw = g => {
        Fill(g, "#2a1a28");
        // from #311f2f
        Line(g, 0f, 2f, g.W, 2f, "#3a2638", 2f, 1f);
        Grain(g, new[] { "#322031", "#221420" }, 300, 0.12f, 2948);
        } },
        ["funRecRock"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#5b4750");
        // from #67525b
        Grain(g, new[] { "#66505a", "#4d3c44", "#51414a" }, 500, 0.16f, 628);
        } },
        ["funRecSand"] = new Spec { Unit = 1.3f, Detail = 1, Draw = g => {
        Fill(g, "#e3c6a2");
        // from #f5d8b4 at (-19.80, 0.50)
        Grain(g, new[] { "#edd2ac", "#d5b892", "#e0c096" }, 700, 0.14f, 7202);
        // faint wave ripples, as the zoom shows
        for (float y = 8f; y < g.H; y += 26f)
        {
        Line(g, 0f, y, g.W, y, "#c9a87e", 2f, 1f);
        }
        } },
        // ---------------------------------------------------------------
        // aus surfaces_fungle_sleepingquarters.js
        // ---------------------------------------------------------------
        ["funDormFloor"] = new Spec { Unit = 1.15f, Detail = 1, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   sandBase(g, w, h)
        Fill(g, "#cdb290");
        Grain(g, new[] { "#cdb290" }, 400, 0.06f, 1751);
        } },
        ["funDormSand"] = new Spec { Unit = 1.3f, Detail = 1, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   sandBase(g, w, h)
        Fill(g, "#bfa284");
        Grain(g, new[] { "#bfa284" }, 400, 0.06f, 1231);
        } },
        ["funDormPath"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#b47844");
        // aus #c8854b
        for (float y = 4f; y < g.H; y += 9f)
        {
        Line(g, 0f, y, g.W, y, "#a06a3a", 2f, 1f);
        }
        Grain(g, new[] { "#c08550", "#a06a3a", "#8f5c30" }, 600, 0.12f, 2132);
        } },
        ["funDormJungle"] = new Spec { Unit = 1.6f, Draw = g => {
        var rnd = new Rng(2792);
        Fill(g, "#200d1e");
        // aus #240e21
        for (float i = 0f; i < 26f; i += 1f)
        {
            float s = 8f + rnd.Next() * 26f;
            Rect(g, rnd.Next() * g.W, rnd.Next() * g.H, s, s * (0.4f + rnd.Next() * 0.6f), i % 3 != 0f ? "#2c1128" : "#3a1630", 0.5f);
        }
        Grain(g, new[] { "#4a1c38", "#180a18", "#55234a" }, 700, 0.14f, 2792);
        } },
        ["funDormCeil"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#bab3a7");
        Line(g, 1f, 1f, 1f+g.W - 2f, 1f, "#a89f90", 2f, 1f);
        Line(g, 1f, 1f+g.H - 2f, 1f+g.W - 2f, 1f+g.H - 2f, "#a89f90", 2f, 1f);
        Line(g, 1f, 1f, 1f, 1f+g.H - 2f, "#a89f90", 2f, 1f);
        Line(g, 1f+g.W - 2f, 1f, 1f+g.W - 2f, 1f+g.H - 2f, "#a89f90", 2f, 1f);
        Line(g, 0f, g.H / 2f, g.W, g.H / 2f, "#b1a99c", 2f, 1f);
        Grain(g, new[] { "#c4bcb0", "#aca494" }, 400, 0.10f, 8148);
        } },
        ["funDormBed"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#3a8ba7");
        // aus #419ab9
        Line(g, 3f, 3f, 3f+g.W - 6f, 3f, "#2f7188", 4f, 1f);
        Line(g, 3f, 3f+g.H - 6f, 3f+g.W - 6f, 3f+g.H - 6f, "#2f7188", 4f, 1f);
        Line(g, 3f, 3f, 3f, 3f+g.H - 6f, "#2f7188", 4f, 1f);
        Line(g, 3f+g.W - 6f, 3f, 3f+g.W - 6f, 3f+g.H - 6f, "#2f7188", 4f, 1f);
        Line(g, g.W * 0.5f, 4f, g.W * 0.5f, g.H - 4f, "#347f98", 3f, 1f);
        Grain(g, new[] { "#4395b2", "#327e97" }, 300, 0.10f, 3641);
        } },
        ["funDormPillow"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#c8c8cf");
        // aus #dedee6
        Line(g, 2f, g.H - 3f, g.W - 2f, g.H - 3f, "#b4b4bd", 3f, 1f);
        Grain(g, new[] { "#d4d4db", "#bbbbc4" }, 250, 0.10f, 3696);
        } },
        ["funDormBlanket"] = new Spec { Unit = 0.7f, Draw = g => {
        var rnd = new Rng(301);
        Fill(g, "#c5c8d0");
        // aus #dbdee6
        for (float i = 0f; i < 7f; i += 1f)
        {
            float y = 3f + rnd.Next() * (g.H - 6f);
            Line(g, 2f, y, g.W - 2f, y + (rnd.Next() * 6f - 3f), "#aeb2bd", 2f, 0.5f);
        }
        Grain(g, new[] { "#d2d5dd", "#b7bac5" }, 300, 0.10f, 301);
        } },
        ["funDormPouf"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#6e4329");
        Line(g, 2f, g.H * 0.35f, g.W - 2f, g.H * 0.35f, "#5d3822", 3f, 1f);
        Line(g, 2f, g.H * 0.7f, g.W - 2f, g.H * 0.7f, "#5d3822", 2f, 1f);
        Grain(g, new[] { "#7a4c2f", "#5f3a24" }, 250, 0.12f, 8434);
        } },
        ["funDormCrate"] = new Spec { Unit = 0.7f, Draw = g => {
        Fill(g, "#394033");
        // aus #3f4739
        Line(g, 3f, 3f, 3f+g.W - 6f, 3f, "#2c332b", 5f, 1f);
        Line(g, 3f, 3f+g.H - 6f, 3f+g.W - 6f, 3f+g.H - 6f, "#2c332b", 5f, 1f);
        Line(g, 3f, 3f, 3f, 3f+g.H - 6f, "#2c332b", 5f, 1f);
        Line(g, 3f+g.W - 6f, 3f, 3f+g.W - 6f, 3f+g.H - 6f, "#2c332b", 5f, 1f);
        Line(g, 4f, g.H - 5f, g.W - 4f, 4f, "#2c332b", 3f, 1f);
        for (float i = 0f; i < 4f; i += 1f)
        {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   g.fillRect(w * 0.18, h * (0.30 + i * 0.12), w * 0.28, 2)
        Fill(g, "#8a8a8a");
        Grain(g, new[] { "#8a8a8a" }, 400, 0.06f, 3612);
        }
        Grain(g, new[] { "#434b3c", "#31382e" }, 350, 0.12f, 3612);
        } },
        ["funDormDrum"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#37384d");
        // aus #3d3e56
        Line(g, 0f, g.H * 0.3f, g.W, g.H * 0.3f, "#2c2d3e", 3f, 1f);
        Line(g, 0f, g.H * 0.72f, g.W, g.H * 0.72f, "#2c2d3e", 3f, 1f);
        Line(g, g.W * 0.5f, 0f, g.W * 0.5f, g.H, "#41425a", 2f, 1f);
        Grain(g, new[] { "#3f4058", "#2e2f41" }, 250, 0.10f, 7088);
        } },
        ["funDormBag"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#34432f");
        Line(g, 2f, g.H * 0.45f, g.W - 2f, g.H * 0.5f, "#2a3626", 3f, 1f);
        Grain(g, new[] { "#3c4d36", "#2c3928" }, 250, 0.12f, 7406);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_fungle_storage.js
        // ---------------------------------------------------------------
        ["funStorageFloor"] = new Spec { Unit = 2.2f, Draw = g => {
        Fill(g, "#d9bc97");
        // Messwert #f5d8b4, eine Stufe dunkler
        // verlaufene Laufstreifen (die Zeichnung zeigt faint horizontale Spuren)
        for (float i = 0f; i < 5f; i += 1f)
        {
            float y = (0.12f + 0.19f * i) * g.H;
            Rect(g, 0f, y, g.W, g.H * 0.045f, i % 2 != 0f ? "#c4a67f" : "#e6cda9", 0.10f);
        }
        Grain(g, new[] { "#c9ab84", "#e8cfa9", "#cfb28b" }, 700, 0.12f, 2976);
        } },
        ["funStorageSand"] = new Spec { Unit = 2.2f, Draw = g => {
        Fill(g, "#dcc19e");
        // Messwert #f5d8b4, eine Stufe dunkler
        Grain(g, new[] { "#cdb189", "#e9d0ab", "#c3a67e" }, 900, 0.13f, 7242);
        } },
        ["funStoragePath"] = new Spec { Unit = 2.0f, Draw = g => {
        var rnd = new Rng(9093);
        Fill(g, "#a56a3c");
        // Messwert #c8854b, eine Stufe dunkler
        Grain(g, new[] { "#94602f", "#b57744", "#8a5527" }, 1100, 0.16f, 9093);
        for (float i = 0f; i < 26f; i += 1f)
        {
            // kleine Kieselpunkte
            float s = 1f + rnd.Next() * 2.5f;
            Rect(g, rnd.Next() * g.W, rnd.Next() * g.H, s, s, i % 2 != 0f ? "#7d4e22" : "#c08549", 0.18f);
        }
        } },
        ["funStorageStoneWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#6d787d");
        // Messwert #859095, eine Stufe dunkler
        // zwei Lagen grosser, leicht versetzter Blockzuege mit dunklen Fugen
        float rows = 2f;
        float rh = g.H / rows;
        for (float r = 0f; r < rows; r += 1f)
        {
            float bw = g.W / 2.2f;
            float off = (r % 2f) * bw * 0.5f;
            for (float b = -1f; b < 3f; b += 1f)
            {
                float x = b * bw + off;
                // Blockflaeche minimal variiert
                Rect(g, x + 1.5f, r * rh + 1.5f, bw - 3f, rh - 3f, (b + r) % 2 != 0f ? "#71807f" : "#687478", 1f);
                // Fugen
                Rect(g, x, r * rh, bw, 2f, "#3a3230", 1f);
                Rect(g, x, r * rh, 2f, rh, "#3a3230", 1f);
            }
        }
        Grain(g, new[] { "#5d686c", "#79878a", "#525c60" }, 500, 0.12f, 4155);
        } },
        ["funStoragePanelWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#545863");
        // Messwert #656977, eine Stufe dunkler
        // vertikale Panelfugen + eine horizontale Latte
        for (float i = 1f; i < 3f; i += 1f)
        {
            Rect(g, (i * g.W) / 3f - 1f, 0f, 2f, g.H, "#42454f", 1f);
        }
        Rect(g, 0f, g.H * 0.52f, g.W, g.H * 0.05f, "#4a4d58", 1f);
        // Nieten an den Fugen
        for (float i = 0f; i < 3f; i += 1f)
        {
            Rect(g, (i * g.W) / 3f + 3f, g.H * 0.2f, 2f, 2f, "#6a6e7a", 1f);
            Rect(g, (i * g.W) / 3f + 3f, g.H * 0.78f, 2f, 2f, "#6a6e7a", 1f);
        }
        Grain(g, new[] { "#4c505b", "#5d626e" }, 350, 0.10f, 5156);
        } },
        ["funStorageFrontWall"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#b9bac6");
        // Messwert #dcdce6, eine Stufe dunkler
        Rect(g, 0f, 0f, g.W, 2f, "#a3a4b2", 1f);
        Rect(g, 0f, g.H - 2f, g.W, 2f, "#a3a4b2", 1f);
        Rect(g, g.W * 0.46f, 0f, 2f, g.H, "#a3a4b2", 1f);
        // das rote Dekor (Rohr mit zwei Halterungen), wie in der Zeichnung
        Rect(g, g.W * 0.16f, g.H * 0.08f, g.W * 0.075f, g.H * 0.84f, "#8e2f24", 1f);
        Rect(g, g.W * 0.12f, g.H * 0.22f, g.W * 0.16f, g.H * 0.06f, "#6e2119", 1f);
        Rect(g, g.W * 0.12f, g.H * 0.62f, g.W * 0.16f, g.H * 0.06f, "#6e2119", 1f);
        Grain(g, new[] { "#acaebc", "#c4c5d1" }, 300, 0.10f, 5005);
        } },
        ["funStorageShelfFace"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   { for (const b of boards)
        Fill(g, "#2e2d2b");
        Grain(g, new[] { "#2e2d2b" }, 400, 0.06f, 2503);
        } },
        ["funStorageShelfTop"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#1c130d");
        // Messwert #140503-Familie, aufgehellt
        for (float i = 1f; i < 4f; i += 1f)
        {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   g.fillRect((i * w) / 4, 0, 1.5, h)
        Fill(g, "#8a8a8a");
        Grain(g, new[] { "#8a8a8a" }, 400, 0.06f, 7639);
        }
        Grain(g, new[] { "#241811", "#120b07" }, 300, 0.12f, 7639);
        } },
        ["funStorageNorthShelf"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   const tools = [[0.10, 0.55], [0.34, 0.72], [0.60, 0.50], [0.
        //   { for (const [tx, tl] of tools)
        Fill(g, "#1d140f");
        Grain(g, new[] { "#1d140f" }, 400, 0.06f, 1571);
        } },
        ["funStorageEastBarrels"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   { for (const [cx, r] of [[0.30, 0.30], [0.72, 0.26]])
        Fill(g, "#475c58");
        Grain(g, new[] { "#475c58" }, 400, 0.06f, 7513);
        } },
        ["funStorageRock"] = new Spec { Unit = 1.6f, Draw = g => {
        var rnd = new Rng(4511);
        Fill(g, "#b3854a");
        // Messwert #d7a259-Familie, dunkler
        // Flecken + Schattennester, damit der Bausatz-Fels nicht wie Beton wirkt
        for (float i = 0f; i < 14f; i += 1f)
        {
            float s = g.W * (0.08f + rnd.Next() * 0.16f);
            Rect(g, rnd.Next() * g.W, rnd.Next() * g.H, s, s * (0.5f + rnd.Next() * 0.6f), i % 3 != 0f ? "#a3763f" : "#c29257", 0.5f);
        }
        Grain(g, new[] { "#8f6432", "#c99a5e", "#6e4c24" }, 700, 0.16f, 4511);
        } },
        ["funStorageDebris"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        // PORT: nicht vollstaendig uebersetzbar, auf die Grundfarbe reduziert.
        // Von Hand nachzubauen sind:
        //   g.fillRect(rnd.Next() * w * 0.9, rnd.Next() * h * 0.9,
        Fill(g, "#c2a17b");
        Grain(g, new[] { "#c2a17b" }, 400, 0.06f, 4878);
        } },
        ["funStorageDoor"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#322924");
        // Messwert #3d3230, eine Stufe dunkler
        // Mittelfuge + zwei Rippen + Nietenreihen
        Rect(g, g.W * 0.49f, 0f, g.W * 0.02f, g.H, "#221b17", 1f);
        Rect(g, 0f, g.H * 0.30f, g.W, g.H * 0.045f, "#221b17", 1f);
        Rect(g, 0f, g.H * 0.66f, g.W, g.H * 0.045f, "#221b17", 1f);
        for (float i = 0f; i < 4f; i += 1f)
        {
            Rect(g, g.W * (0.10f + 0.2f * i), g.H * 0.10f, 2f, 2f, "#4c4038", 1f);
            Rect(g, g.W * (0.10f + 0.2f * i), g.H * 0.86f, 2f, 2f, "#4c4038", 1f);
        }
        Grain(g, new[] { "#2b2320", "#3c322c" }, 250, 0.10f, 8168);
        } },
        ["funStorageMetal"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#4e4238");
        // Messwert #81503d, eine Stufe dunkler
        for (float i = 0f; i < 10f; i += 1f)
        {
            // gebürstete Längsstreifen
            Rect(g, 0f, (i / 10f) * g.H, g.W, g.H / 22f, i % 2 != 0f ? "#3c332b" : "#5e5044", 0.25f);
        }
        Grain(g, new[] { "#443a30", "#5a4d40" }, 250, 0.10f, 1534);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_fungle_terrain.js
        // ---------------------------------------------------------------
        ["funTerrainWater"] = new Spec { Unit = 4.0f, Draw = g => {
        Fill(g, "#33162f");
        // gemessen #3a1a36
        for (float y = 0f; y < g.H; y += 11f)
        {
        Line(g, 0f, y, g.W, y, "#3d1d38", 2f, 1f);
        }
        for (float y = 5f; y < g.H; y += 23f)
        {
        Line(g, 0f, y, g.W, y, "#2a1226", 3f, 1f);
        }
        // Ein paar hellere Kaemme, damit die Flaeche nicht als Streifenmuster erstarrt.
        for (float y = 14f; y < g.H; y += 37f)
        {
        Line(g, 0f, y, g.W, y, "#4a2745", 1f, 1f);
        }
        Grain(g, new[] { "#2e1429", "#3f1e3a" }, 700, 0.10f, 9777);
        } },
        // Das Terrain wurde im Prototyp neu erzeugt (surfaces_fungle_terrain.js, 2026-08-29/30):
        // sieben Materialien statt zwei. Uebersetzt wie der Rest dieser Datei, Statement fuer
        // Statement. `funTerrainSand` war bis dahin das goldene Meer unter falschem Namen und ist
        // jetzt der Strand, den der Name meint.
        ["funTerrainSand"] = new Spec { Unit = 2.6f, Draw = g => {
        Fill(g, "#dcc09c");
        // gemessen #f5d8b4
        Grain(g, new[] { "#e6cba8", "#cfb28e", "#eed7b6" }, 1400, 0.18f, 9197);
        for (float y = 0f; y < g.H; y += 29f)
        {
        Line(g, 0f, y, g.W, y + 3f, "#cdb08a", 2f, 1f);
        }
        for (float y = 17f; y < g.H; y += 41f)
        {
        Line(g, 0f, y, g.W, y - 2f, "#e8cfad", 1f, 1f);
        }
        } },
        // DAS MEER - ein Nachtmeer (User-Entscheidung 2026-08-30: "gelbes Wasser???"). Der Atlas
        // misst #f3bb54, das Abendlicht AUF dem Wasser in der Draufsicht; aus Augenhoehe las
        // dieselbe Farbe als Sandwueste bis zum Horizont. Tiefes Blaugruen, die Sonne nur als
        // vereinzelte warme Glanzstriche. Grosse Einheit, weil es die groesste Flaeche der Karte ist.
        ["funTerrainSea"] = new Spec { Unit = 5.0f, Draw = g => {
        Fill(g, "#1e3b47");
        for (float y = 6f; y < g.H; y += 37f)
        {
        Line(g, 0f, y, g.W, y - 1f, "#183340", 3f, 1f);
        }
        for (float y = 21f; y < g.H; y += 53f)
        {
        Line(g, 0f, y, g.W, y + 1f, "#264a58", 2f, 1f);
        }
        var rnd = new Rng(4711);
        for (int i = 0; i < 90; i++)
        {
            float x = rnd.Next() * g.W, y = rnd.Next() * g.H, l = 4f + rnd.Next() * 10f;
            Line(g, x, y, x + l, y, "#3f7080", 2f, 0.55f);
            Line(g, x + 1f, y + 2f, x + l - 1f, y + 2f, "#16303b", 1f, 0.55f);
        }
        // Die Sonnenspur: wenige warme Glanzstriche, sparsam.
        for (int i = 0; i < 22; i++)
        {
            float x = rnd.Next() * g.W, y = rnd.Next() * g.H, l = 3f + rnd.Next() * 8f;
            Line(g, x, y, x + l, y, "#d9a447", 1f, 0.5f);
        }
        Grain(g, new[] { "#1a3541", "#244551" }, 700, 0.08f, 4712);
        } },
        // WEG: das Ocker der Pfade und der Feuerstelle.
        ["funTerrainPath"] = new Spec { Unit = 2.2f, Draw = g => {
        Fill(g, "#b47542");
        // gemessen #c8854b
        Grain(g, new[] { "#c08150", "#a56a3a", "#c98c58" }, 1200, 0.18f, 3301);
        for (float y = 9f; y < g.H; y += 31f)
        {
        Line(g, 0f, y, g.W, y + 2f, "#a3673a", 2f, 1f);
        }
        } },
        // JUNGLEBODEN: das dunkle Weinrot unter den Pilzen, mit Sporenflecken.
        ["funTerrainJungle"] = new Spec { Unit = 2.4f, Draw = g => {
        Fill(g, "#2e1128");
        // gemessen #240e21..#501937
        Grain(g, new[] { "#3d1834", "#22091d", "#4a1c3d" }, 1600, 0.22f, 911);
        var rnd = new Rng(911);
        for (int i = 0; i < 40; i++)
        {
            float x = rnd.Next() * g.W, y = rnd.Next() * g.H, r = 2f + rnd.Next() * 6f;
            g.FillEllipse(x, y, r, r, C(i % 4 != 0 ? "#5a2246" : "#8a3a62"), 1f);
        }
        } },
        // PLATEAU: das dunkle Gruen der Hochflaeche.
        ["funTerrainPlateau"] = new Spec { Unit = 2.4f, Draw = g => {
        Fill(g, "#202623");
        // gemessen #262c29
        Grain(g, new[] { "#2a322d", "#1a1f1c", "#2f3a32" }, 1400, 0.2f, 2207);
        for (float y = 7f; y < g.H; y += 23f)
        {
        Line(g, 0f, y, g.W, y + 1f, "#1c221f", 1f, 1f);
        }
        } },
        // KLIPPE: das Orange der Felswaende, waagerecht geschichtet.
        ["funTerrainCliff"] = new Spec { Unit = 1.6f, Draw = g => {
        Fill(g, "#a3611a");
        // gemessen #b96e15
        for (float y = 0f; y < g.H; y += 9f)
        {
        Line(g, 0f, y, g.W, y + 1f, "#8e5316", 2f, 1f);
        }
        for (float y = 4f; y < g.H; y += 27f)
        {
        Line(g, 0f, y, g.W, y - 1f, "#b8722a", 1f, 1f);
        }
        Grain(g, new[] { "#94571a", "#b06a22" }, 900, 0.14f, 1601);
        } },
        // FELS: die fast schwarzen Brocken.
        ["funTerrainRock"] = new Spec { Unit = 1.2f, Draw = g => {
        Fill(g, "#170c16");
        // gemessen #1a0e19
        Grain(g, new[] { "#22121f", "#0f070e" }, 900, 0.18f, 1201);
        } },
        // ---------------------------------------------------------------
        // aus surfaces_fungle_upperengine.js
        // ---------------------------------------------------------------
        ["funUpperengineRock"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#170d16");
        // from #1a0e19
        Grain(g, new[] { "#221320", "#120a11", "#281826" }, 800, 0.16f, 8384);
        } },
        ["funUpperengineRockTop"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        var rnd = new Rng(7164);
        Fill(g, "#212624");
        // from #262c29 at (18.00,4.62)
        for (float i = 0f; i < 50f; i += 1f)
        {
            Rect(g, rnd.Next() * g.W, rnd.Next() * g.H, 2f + rnd.Next() * 5f, 1f + rnd.Next() * 3f, "#2c3330", 0.4f);
        }
        Grain(g, new[] { "#28302c", "#1b211e" }, 500, 0.13f, 7164);
        } },
        ["funUpperengineRockFace"] = new Spec { Unit = 1.3f, Detail = 1, Draw = g => {
        Fill(g, "#300d20");
        // from #3a1028 at (18.00,-1.00)
        // Schichtstossen des Kliffprofils
        for (float y = 6f; y < g.H; y += 14f)
        {
        Line(g, 0f, y, g.W, y + 2f, "#22081a", 3f, 1f);
        }
        Grain(g, new[] { "#38122a", "#260a1c" }, 600, 0.15f, 7369);
        } },
        ["funUpperenginePlinth"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#585a68");
        // from #656777 at (20.50,4.40)
        Line(g, 0f, 2f, g.W, 2f, "#6a6c7a", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#464854", 2f, 1f);
        Grain(g, new[] { "#5f6170", "#4d4f5c" }, 400, 0.12f, 5937);
        } },
        ["funUpperengineHousing"] = new Spec { Unit = 1.45f, Detail = 1, Draw = g => {
        Fill(g, "#304744");
        // from #39524f
        for (float x = 12f; x < g.W; x += 30f)
        {
        Line(g, x, 0f, x, g.H, "#263a37", 2f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#3d5a56", 2f, 1f);
        Grain(g, new[] { "#36504c", "#293e3b" }, 450, 0.12f, 1567);
        } },
        ["funUpperengineHousingTop"] = new Spec { Unit = 1.3f, Detail = 1, Draw = g => {
        Fill(g, "#3c4944");
        // from #46554f
        Line(g, 0f, 2f, g.W, 2f, "#4b5a53", 2f, 1f);
        Grain(g, new[] { "#43524b", "#333f3a" }, 400, 0.12f, 4213);
        } },
        ["funUpperengineMaroon"] = new Spec { Unit = 1.2f, Detail = 1, Draw = g => {
        Fill(g, "#641d2d");
        // from #762336
        Line(g, 0f, 2f, g.W, 2f, "#7c2739", 2f, 1f);
        Grain(g, new[] { "#6e2132", "#571826" }, 400, 0.12f, 5084);
        } },
        ["funUpperengineBrass"] = new Spec { Unit = 0.9f, Detail = 1, Draw = g => {
        Fill(g, "#bd8c4c");
        // from #d7a259
        Line(g, 0f, 1f, g.W, 1f, "#d09a56", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#9d723c", 2f, 1f);
        Grain(g, new[] { "#c69450", "#ab7f43" }, 350, 0.11f, 8411);
        } },
        ["funUpperengineDrum"] = new Spec { Unit = 1.1f, Detail = 1, Draw = g => {
        Fill(g, "#585a66");
        // from #666776
        for (float x = 14f; x < g.W; x += 34f)
        {
        Line(g, x, 0f, x, g.H, "#484a56", 3f, 1f);
        }
        Grain(g, new[] { "#606270", "#4c4e5a" }, 380, 0.11f, 1616);
        } },
        ["funUpperenginePipe"] = new Spec { Unit = 0.8f, Detail = 1, Draw = g => {
        Fill(g, "#a05e0e");
        // from #bb6d11
        for (float y = 3f; y < g.H; y += 8f)
        {
        Line(g, 0f, y, g.W, y, "#8a4f0b", 2f, 1f);
        }
        Grain(g, new[] { "#ac650f", "#8d540c" }, 320, 0.12f, 5748);
        } },
        ["funUpperenginePlate"] = new Spec { Unit = 1.0f, Detail = 1, Draw = g => {
        Fill(g, "#575964");
        // from #656777
        for (float y = 6f; y < g.H; y += 14f)
        {
            for (float x = ((y / 14f) % 2f != 0f) ? 8f : 16f; x < g.W; x += 16f)
            {
                Line(g, x, y, x + 6f, y + 6f, "#494b56", 2f, 1f);
                Line(g, x + 6f, y, x, y + 6f, "#494b56", 2f, 1f);
            }
        }
        Grain(g, new[] { "#5f616c", "#4c4e59" }, 300, 0.10f, 2396);
        } },
        ["funUpperengineCrystal"] = new Spec { Unit = 0.7f, Draw = g => {
        Fill(g, "#1a4741");
        // geschaetzt (kein Atlas-Texel)
        // helle Facettenstreifen
        for (float x = 2f; x < g.W; x += 10f)
        {
        Line(g, x, 0f, x - 3f, g.H, "#2f7a70", 2f, 1f);
        }
        Grain(g, new[] { "#205249", "#143a34" }, 250, 0.12f, 622);
        } },
        ["funUpperengineRail"] = new Spec { Unit = 0.5f, Draw = g => {
        Fill(g, "#608287");
        // from #6f9298
        Line(g, 0f, 1f, g.W, 1f, "#74939a", 2f, 1f);
        Line(g, 0f, g.H - 2f, g.W, g.H - 2f, "#4d6a70", 2f, 1f);
        Grain(g, new[] { "#6a8b91", "#54747a" }, 280, 0.10f, 701);
        } },
        ["funUpperengineTread"] = new Spec { Unit = 0.53f, Draw = g => {
        Fill(g, "#434a4e");
        // from #4d4f4b
        // dunkle Querlamellen des Gitters
        float lam = MathF.Max(4f, MathF.Round(g.H / 5f));
        for (float y = lam; y < g.H; y += lam)
        {
        Line(g, 2f, y, g.W - 2f, y, "#2c3438", 2f, 1f);
        }
        Line(g, 0f, 2f, g.W, 2f, "#78938f", 3f, 1f);
        // from #85948e, die helle Trittkante
        Grain(g, new[] { "#4b5257", "#3a4145" }, 250, 0.10f, 1835);
        } },
        ["funUpperengineWange"] = new Spec { Unit = 1.1f, Detail = 1, Draw = g => {
        Fill(g, "#585a66");
        // from #666776
        for (float y = 4f; y < g.H; y += 10f)
        {
        Line(g, 0f, y, g.W, y, "#484a56", 3f, 1f);
        }
        Line(g, 0f, 1f, g.W, 1f, "#6a6c7a", 2f, 1f);
        // die Faltung oben
        Grain(g, new[] { "#5f6170", "#4d4f5c" }, 300, 0.11f, 3136);
        } },
        ["funUpperengineCrate"] = new Spec { Unit = 0.6f, Draw = g => {
        Fill(g, "#232321");
        // from #2a2a28
        Line(g, 3f, 3f, 3f+g.W - 6f, 3f, "#161614", 5f, 1f);
        Line(g, 3f, 3f+g.H - 6f, 3f+g.W - 6f, 3f+g.H - 6f, "#161614", 5f, 1f);
        Line(g, 3f, 3f, 3f, 3f+g.H - 6f, "#161614", 5f, 1f);
        Line(g, 3f+g.W - 6f, 3f, 3f+g.W - 6f, 3f+g.H - 6f, "#161614", 5f, 1f);
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#161614", 4f, 1f);
        Grain(g, new[] { "#2b2b28", "#1b1b19" }, 380, 0.12f, 895);
        } },
        ["funUpperengineCrateTan"] = new Spec { Unit = 0.6f, Draw = g => {
        Fill(g, "#ad7440");
        // from #c8854b (21.60,2.25)
        Line(g, 3f, 3f, 3f+g.W - 6f, 3f, "#7d5327", 5f, 1f);
        Line(g, 3f, 3f+g.H - 6f, 3f+g.W - 6f, 3f+g.H - 6f, "#7d5327", 5f, 1f);
        Line(g, 3f, 3f, 3f, 3f+g.H - 6f, "#7d5327", 5f, 1f);
        Line(g, 3f+g.W - 6f, 3f, 3f+g.W - 6f, 3f+g.H - 6f, "#7d5327", 5f, 1f);
        Line(g, g.W / 2f, 0f, g.W / 2f, g.H, "#7d5327", 4f, 1f);
        Grain(g, new[] { "#b87d47", "#9c6736" }, 380, 0.12f, 5474);
        } },
        ["funUpperengineGround"] = new Spec { Unit = 1.5f, Draw = g => {
        var rnd = new Rng(4481);
        Fill(g, "#3a564e");
        // from #44635b at (21.00, 1.00)
        for (float i = 0f; i < 130f; i += 1f)
        {
            // the dark #262c29 pockets (small cliffs)
            float s = 2f + rnd.Next() * 7f;
            Rect(g, rnd.Next() * g.W, rnd.Next() * g.H, s, s * 0.6f, "#20261f", 0.28f);
        }
        Grain(g, new[] { "#42604f", "#33493f", "#48655b" }, 700, 0.12f, 4481);
        } },
        ["funUpperenginePath"] = new Spec { Unit = 1.4f, Detail = 1, Draw = g => {
        var rnd = new Rng(769);
        Fill(g, "#ad7440");
        // from #c8854b at (20.00, 8.50)
        Grain(g, new[] { "#b87d47", "#9c6736", "#c08a52" }, 800, 0.14f, 769);
        for (float i = 0f; i < 40f; i += 1f)
        {
            // trampled lighter patches
            Rect(g, rnd.Next() * g.W, rnd.Next() * g.H, 3f + rnd.Next() * 8f, 2f + rnd.Next() * 4f, "#c08a52", 0.18f);
        }
        } },
        ["funUpperengineDirt"] = new Spec { Unit = 1.1f, Detail = 1, Draw = g => {
        Fill(g, "#a05f12");
        // from #b96e15 at (19.20, 5.50)
        Grain(g, new[] { "#b06a15", "#8d5210", "#bb7420" }, 700, 0.16f, 5253);
        } },

        /// Gelb-schwarzer Warnbalken ueber der Labortuer (Review-Runde 4).
        /// PORT: der Prototyp zeichnet Parallelogramme (Canvas-Pfad); hier stehen sie als
        /// senkrechte Balken - aus zwei Metern derselbe Balken.
        ["funLabHazard"] = new Spec { Unit = 0.5f, Detail = 1, Draw = g => {
        Fill(g, "#c9a227");
        for (float x = 0f; x < g.W; x += g.H * 0.9f)
        {
        Rect(g, x, 0f, g.H * 0.45f, g.H, "#1f1f22", 1f);
        }
        } },
    };
}
