// ============================================================================
// generate-codir-pptx.js
// Génère la présentation CODIR ELTON pour validation V1.0/V1.1 SunuPaie
// Palette ELTON Navy/Orange, ~28 slides, 1-2h de présentation
// ============================================================================

const pptxgen = require("pptxgenjs");

// ─── Palette ELTON OFFICIELLE (Charte Mars 2024) ───────────────────────
// Couleurs primaires (charte officielle) :
//   Bleu  : #1A73B5 (RVB 25, 114, 181 / CMJN 86, 37, 0, 29)
//   Rouge : #E5003D (RVB 230, 0, 62  / CMJN 0, 100, 73, 10)
// Couleurs secondaires :
//   Turquoise : #6EE0DE / Vert : #02AD60 / Jaune : #F9D719
const BLUE = "1A73B5";        // Bleu primaire ELTON
const BLUE_DARK = "0F5288";   // Variante foncée pour contraste/profondeur
const BLUE_DARKEST = "094069";// Pour les fonds slides "hero"
const RED = "E5003D";         // Rouge primaire ELTON
const RED_DARK = "B30030";    // Pour hover/accents intenses
const RED_SOFT = "FFD4DD";    // Pour fonds doux
const TURQUOISE = "6EE0DE";   // Couleur secondaire (info)
const GREEN = "02AD60";       // Couleur secondaire (succès)
const YELLOW = "F9D719";      // Couleur secondaire (alerte)
const LIGHT = "F0F4F8";       // Fond clair (équivalent gris pâle de la charte)
const TEXT = "1A73B5";        // Texte = bleu ELTON sur fond clair
const TEXT_DARK = "1F2937";   // Texte sombre lisible sur clair
const TEXT_SOFT = "5C6679";   // Texte secondaire (descriptions)
const TEXT_LIGHT = "DCE5EE";  // Texte clair sur fond bleu
const WHITE = "FFFFFF";

// Aliases legacy pour compatibilité avec l'existant (NAVY → BLUE) :
const NAVY = BLUE;
const NAVY_LIGHT = BLUE_DARK;
const NAVY_DARK = BLUE_DARKEST;
const ORANGE = RED;           // Accent ELTON = rouge (pas d'orange dans la charte)
const ORANGE_SOFT = RED_SOFT;
const CREAM = LIGHT;

// Typographies ELTON officielles : Ubuntu (titres) + Poppins (corps).
// Si Ubuntu/Poppins absentes du poste, fallback Calibri (Windows par défaut).
const FONT_TITLE = "Ubuntu";        // Titres (Ubuntu Bold)
const FONT_HEADER_BOLD = "Ubuntu";  // Sous-titres (Ubuntu Medium)
const FONT_BODY = "Poppins";        // Corps de texte (Poppins Regular)

// ─── Setup présentation ─────────────────────────────────────────────────
const pres = new pptxgen();
pres.layout = "LAYOUT_16x9"; // 10" x 5.625"
pres.author = "Abdoulaye Dieng";
pres.title = "SunuPaie ELTON - Présentation CODIR Mai 2026";
pres.company = "ELTON Oil Company";

// ─── Helpers ────────────────────────────────────────────────────────────
function addFooter(slide, pageNum, total) {
  slide.addShape(pres.shapes.RECTANGLE, {
    x: 0, y: 5.35, w: 10, h: 0.275, fill: { color: NAVY }, line: { color: NAVY }
  });
  slide.addText("SunuPaie ELTON · CODIR Mai 2026", {
    x: 0.3, y: 5.35, w: 5, h: 0.275, fontSize: 9, fontFace: FONT_BODY,
    color: TEXT_LIGHT, valign: "middle", margin: 0
  });
  slide.addText(`${pageNum} / ${total}`, {
    x: 8.7, y: 5.35, w: 1, h: 0.275, fontSize: 9, fontFace: FONT_BODY,
    color: TEXT_LIGHT, align: "right", valign: "middle", margin: 0
  });
}

function addTitleBar(slide, title, subtitle) {
  // Bandeau navy en haut
  slide.addShape(pres.shapes.RECTANGLE, {
    x: 0, y: 0, w: 10, h: 0.85, fill: { color: NAVY }, line: { color: NAVY }
  });
  // Accent orange
  slide.addShape(pres.shapes.RECTANGLE, {
    x: 0, y: 0.85, w: 10, h: 0.05, fill: { color: ORANGE }, line: { color: ORANGE }
  });
  slide.addText(title, {
    x: 0.4, y: 0.1, w: 9.2, h: 0.45, fontSize: 22, fontFace: FONT_HEADER_BOLD,
    bold: true, color: WHITE, valign: "middle", margin: 0
  });
  if (subtitle) {
    slide.addText(subtitle, {
      x: 0.4, y: 0.5, w: 9.2, h: 0.35, fontSize: 12, fontFace: FONT_BODY,
      color: ORANGE_SOFT, valign: "middle", margin: 0
    });
  }
}

function makeShadow() {
  return { type: "outer", color: "000000", blur: 8, offset: 2, angle: 90, opacity: 0.18 };
}

const TOTAL_SLIDES = 31;
let pageNum = 0;

// ============================================================================
// SLIDE 1 — TITRE
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: NAVY_DARK };

  // Bande orange à gauche
  s.addShape(pres.shapes.RECTANGLE, {
    x: 0, y: 0, w: 0.4, h: 5.625, fill: { color: ORANGE }, line: { color: ORANGE }
  });

  // Logo zone (placeholder)
  s.addText("ELTON", {
    x: 0.8, y: 0.4, w: 4, h: 0.5, fontSize: 18, fontFace: FONT_BODY,
    bold: true, color: ORANGE, charSpacing: 8, margin: 0
  });
  s.addText("Oil Company", {
    x: 0.8, y: 0.9, w: 4, h: 0.3, fontSize: 11, fontFace: FONT_BODY,
    color: TEXT_LIGHT, italic: true, charSpacing: 4, margin: 0
  });

  // Titre principal
  s.addText("SunuPaie", {
    x: 0.8, y: 1.8, w: 9, h: 1.0, fontSize: 56, fontFace: FONT_HEADER_BOLD,
    bold: true, color: WHITE, margin: 0
  });
  s.addText("Solution intégrée GRH-Paie", {
    x: 0.8, y: 2.85, w: 9, h: 0.5, fontSize: 22, fontFace: FONT_BODY,
    color: ORANGE, margin: 0
  });

  // Sous-titre objectif
  s.addText("Présentation CODIR — Validation mise en production V1.0 / V1.1", {
    x: 0.8, y: 3.7, w: 9, h: 0.4, fontSize: 16, fontFace: FONT_BODY,
    italic: true, color: TEXT_LIGHT, margin: 0
  });

  // Footer date / présentateur
  s.addShape(pres.shapes.RECTANGLE, {
    x: 0.8, y: 4.6, w: 6, h: 0.05, fill: { color: ORANGE }, line: { color: ORANGE }
  });
  s.addText([
    { text: "Mai 2026", options: { bold: true, color: WHITE } },
    { text: "  ·  ", options: { color: ORANGE } },
    { text: "Présenté par Abdoulaye DIENG", options: { color: TEXT_LIGHT } }
  ], {
    x: 0.8, y: 4.75, w: 8, h: 0.4, fontSize: 14, fontFace: FONT_BODY, margin: 0
  });
}

// ============================================================================
// SLIDE 2 — SOMMAIRE
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Sommaire", "Plan de la présentation (1h30 + Q&A)");

  const sections = [
    { num: "1", title: "Contexte & vue d'ensemble", desc: "Pourquoi ce projet, périmètre, architecture", time: "10 min" },
    { num: "2", title: "Modules métier", desc: "Paie, RH, Intérimaires, Imports", time: "30 min" },
    { num: "3", title: "Décisionnel & Dashboards", desc: "6 tableaux de bord RH analytiques", time: "15 min" },
    { num: "4", title: "Sécurité & Conformité", desc: "RBAC, audit, droit social Sénégal", time: "10 min" },
    { num: "5", title: "Statut livraison & Bénéfices", desc: "V1.0, V1.1, gains chiffrés", time: "10 min" },
    { num: "6", title: "Mise en production", desc: "Plan, risques, roadmap, demande de validation", time: "10 min" },
  ];

  let y = 1.2;
  sections.forEach(sec => {
    // Numéro orange en cercle
    s.addShape(pres.shapes.OVAL, {
      x: 0.5, y: y + 0.05, w: 0.55, h: 0.55, fill: { color: ORANGE }, line: { color: ORANGE }
    });
    s.addText(sec.num, {
      x: 0.5, y: y + 0.05, w: 0.55, h: 0.55, fontSize: 20, fontFace: FONT_HEADER_BOLD,
      bold: true, color: WHITE, align: "center", valign: "middle", margin: 0
    });
    // Titre + desc
    s.addText(sec.title, {
      x: 1.3, y: y, w: 6.5, h: 0.35, fontSize: 16, fontFace: FONT_HEADER_BOLD,
      bold: true, color: NAVY, valign: "middle", margin: 0
    });
    s.addText(sec.desc, {
      x: 1.3, y: y + 0.32, w: 6.5, h: 0.3, fontSize: 11, fontFace: FONT_BODY,
      color: TEXT_SOFT, italic: true, valign: "middle", margin: 0
    });
    // Time badge
    s.addShape(pres.shapes.RECTANGLE, {
      x: 8.2, y: y + 0.1, w: 1.3, h: 0.45, fill: { color: NAVY }, line: { color: NAVY }
    });
    s.addText(sec.time, {
      x: 8.2, y: y + 0.1, w: 1.3, h: 0.45, fontSize: 11, fontFace: FONT_BODY,
      color: WHITE, bold: true, align: "center", valign: "middle", margin: 0
    });
    y += 0.7;
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 3 — CONTEXTE & ENJEUX
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Contexte & enjeux", "Fafadie Paie en place — SunuPaie étend la couverture vers le RH complet");

  // Problème (gauche)
  s.addShape(pres.shapes.RECTANGLE, {
    x: 0.4, y: 1.2, w: 4.5, h: 3.9, fill: { color: WHITE }, line: { color: "DDDDDD" },
    shadow: makeShadow()
  });
  s.addShape(pres.shapes.RECTANGLE, {
    x: 0.4, y: 1.2, w: 0.08, h: 3.9, fill: { color: RED }, line: { color: RED }
  });
  s.addText("📊  AUJOURD'HUI  —  Fafadie Paie en place", {
    x: 0.6, y: 1.35, w: 4.2, h: 0.4, fontSize: 14, fontFace: FONT_HEADER_BOLD,
    bold: true, color: RED, margin: 0
  });
  s.addText([
    { text: "Fafadie Paie : génération et impression des bulletins OK", options: { bullet: true, breakLine: true } },
    { text: "Calculs paie SN (IPRES, CSS, IR, TRIMF) maitrisés", options: { bullet: true, breakLine: true } },
    { text: "MAIS aucune dimension RH élaborée :", options: { bullet: true, breakLine: true } },
    { text: "Pas de gestion des contrats (CDI/CDD/Stage)", options: { bullet: true, indentLevel: 1, breakLine: true } },
    { text: "Pas de gestion congés / missions / formations / évaluations", options: { bullet: true, indentLevel: 1, breakLine: true } },
    { text: "Pas de gestion prêts / avances avec échéanciers", options: { bullet: true, indentLevel: 1, breakLine: true } },
    { text: "Pas de gestion intérimaires / mouvements", options: { bullet: true, indentLevel: 1, breakLine: true } },
    { text: "Pas de pilotage : aucun dashboard RH", options: { bullet: true, indentLevel: 1, breakLine: true } },
    { text: "Pas d'audit ni de traçabilité des actions sensibles", options: { bullet: true, indentLevel: 1 } }
  ], {
    x: 0.6, y: 1.85, w: 4.2, h: 3.2, fontSize: 10, fontFace: FONT_BODY,
    color: TEXT, paraSpaceAfter: 3, margin: 0
  });

  // Solution (droite)
  s.addShape(pres.shapes.RECTANGLE, {
    x: 5.1, y: 1.2, w: 4.5, h: 3.9, fill: { color: WHITE }, line: { color: "DDDDDD" },
    shadow: makeShadow()
  });
  s.addShape(pres.shapes.RECTANGLE, {
    x: 5.1, y: 1.2, w: 0.08, h: 3.9, fill: { color: GREEN }, line: { color: GREEN }
  });
  s.addText("🚀  DEMAIN  —  SunuPaie (extension Fafadie + RH complet)", {
    x: 5.3, y: 1.35, w: 4.2, h: 0.4, fontSize: 14, fontFace: FONT_HEADER_BOLD,
    bold: true, color: GREEN, margin: 0
  });
  s.addText([
    { text: "Conserve l'acquis Fafadie : bulletins + déclarations", options: { bullet: true, breakLine: true } },
    { text: "AJOUTE la dimension RH complète :", options: { bullet: true, breakLine: true } },
    { text: "Salariés / Contrats / Avancements / Offboarding", options: { bullet: true, indentLevel: 1, breakLine: true } },
    { text: "Congés / Missions / Formations / Évaluations", options: { bullet: true, indentLevel: 1, breakLine: true } },
    { text: "Prêts & avances avec échéanciers + heures sup", options: { bullet: true, indentLevel: 1, breakLine: true } },
    { text: "Intérimaires (Sites / Unités multi-affectation)", options: { bullet: true, indentLevel: 1, breakLine: true } },
    { text: "Disciplinaire (Code Travail SN) + STC légal", options: { bullet: true, indentLevel: 1, breakLine: true } },
    { text: "6 dashboards + audit complet + RBAC + espace salarié", options: { bullet: true, indentLevel: 1, breakLine: true } },
    { text: "Comptabilité (export auto bulletins → écritures)", options: { bullet: true, indentLevel: 1 } }
  ], {
    x: 5.3, y: 1.85, w: 4.2, h: 3.2, fontSize: 10, fontFace: FONT_BODY,
    color: TEXT, paraSpaceAfter: 3, margin: 0
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 4 — PÉRIMÈTRE FONCTIONNEL
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Périmètre fonctionnel", "7 grands modules · 90+ entités · 60+ écrans");

  const modules = [
    { icon: "💰", title: "Paie", desc: "Bulletins · Périodes · Déclarations IPRES/CSS/VRS/1024 · Livre L.120 · Prêts & avances · Heures sup", color: RED },
    { icon: "👥", title: "RH cœur", desc: "Salariés · Contrats CDI/CDD/Stage · Avancements · Disciplinaire · Offboarding & STC · Dossiers", color: BLUE },
    { icon: "📋", title: "RH workflows", desc: "Congés · Missions/déplacements · Formations · Évaluations annuelles · Demandes attestations", color: BLUE_DARK },
    { icon: "👷", title: "Intérimaires V1.1", desc: "Sites (Stations/Siège/Dépôts) · Unités hiérarchiques · Multi-affectation · Mouvements · Sociétés", color: BLUE },
    { icon: "📊", title: "Décisionnel", desc: "6 dashboards RH analytiques · Exports Excel/PDF · Filtres dynamiques · Cache 5 min · Audit complet", color: BLUE_DARK },
    { icon: "💼", title: "Comptabilité", desc: "Plan comptable · Écritures auto depuis bulletins · Export comptable · Réconciliation", color: RED },
    { icon: "⚙️", title: "Paramétrage & Imports", desc: "Rubriques · Barèmes IR/TRIMF · Templates Word · SMTP/Graph · Imports Excel salariés/RIB/conjoints", color: BLUE },
  ];

  let y = 0.95;
  modules.forEach(m => {
    s.addShape(pres.shapes.RECTANGLE, {
      x: 0.4, y, w: 9.2, h: 0.58, fill: { color: WHITE }, line: { color: "EEEEEE" }
    });
    s.addShape(pres.shapes.RECTANGLE, {
      x: 0.4, y, w: 0.08, h: 0.58, fill: { color: m.color }, line: { color: m.color }
    });
    s.addText(m.icon, {
      x: 0.55, y: y + 0.04, w: 0.55, h: 0.5, fontSize: 22, align: "center", valign: "middle", margin: 0
    });
    s.addText(m.title, {
      x: 1.2, y: y + 0.04, w: 2.3, h: 0.5, fontSize: 13, fontFace: FONT_HEADER_BOLD,
      bold: true, color: NAVY, valign: "middle", margin: 0
    });
    s.addText(m.desc, {
      x: 3.55, y: y + 0.04, w: 5.95, h: 0.5, fontSize: 9, fontFace: FONT_BODY,
      color: TEXT_SOFT, valign: "middle", margin: 0
    });
    y += 0.62;
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 5 — ARCHITECTURE MACRO
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Architecture macro", "Stack technique et flux applicatif");

  // 4 layers verticaux
  const layers = [
    { y: 1.2, h: 0.7, color: ORANGE, title: "👥 Utilisateurs", desc: "DG · DRH · DAF · Comptable · Manager · Salarié · Intérimaire", textColor: WHITE },
    { y: 2.0, h: 0.7, color: NAVY, title: "🌐 Présentation Web", desc: "Blazor Server (DevExpress XAF 25.1) · Responsive · Accessible navigateur", textColor: WHITE },
    { y: 2.8, h: 0.7, color: NAVY_LIGHT, title: "⚙️ Logique métier (Module)", desc: "Services C# · Workflows · Calculs paie · DTO Dashboards · ImportServices", textColor: WHITE },
    { y: 3.6, h: 0.7, color: NAVY_DARK, title: "💾 Persistance", desc: "SQL Server 2019+ via XPO ORM · 90+ entités · GCRecord soft-delete · Audit trail", textColor: WHITE },
  ];

  layers.forEach(l => {
    s.addShape(pres.shapes.RECTANGLE, {
      x: 1, y: l.y, w: 8, h: l.h, fill: { color: l.color }, line: { color: l.color }, shadow: makeShadow()
    });
    s.addText(l.title, {
      x: 1.2, y: l.y, w: 3, h: l.h, fontSize: 14, fontFace: FONT_HEADER_BOLD,
      bold: true, color: l.textColor, valign: "middle", margin: 0
    });
    s.addText(l.desc, {
      x: 4.2, y: l.y, w: 4.6, h: l.h, fontSize: 11, fontFace: FONT_BODY,
      color: l.textColor, valign: "middle", margin: 0
    });
  });

  // Flèches verticales
  for (let i = 0; i < 3; i++) {
    s.addShape(pres.shapes.LINE, {
      x: 5, y: 1.9 + i * 0.8, w: 0, h: 0.1,
      line: { color: ORANGE, width: 3, endArrowType: "triangle" }
    });
  }

  // Note bas
  s.addText("⚡ Performance : cache mémoire 5 min sur les KPI · TTL configurable · IDataProtector pour les secrets", {
    x: 1, y: 4.55, w: 8, h: 0.4, fontSize: 10, fontFace: FONT_BODY,
    color: TEXT_SOFT, italic: true, align: "center", margin: 0
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 6 — STATS CLÉS
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: NAVY_DARK };
  // Pas de titlebar standard car slide stat
  s.addText("L'application en chiffres", {
    x: 0.5, y: 0.4, w: 9, h: 0.6, fontSize: 28, fontFace: FONT_HEADER_BOLD,
    bold: true, color: WHITE, margin: 0
  });
  s.addShape(pres.shapes.RECTANGLE, {
    x: 0.5, y: 0.95, w: 1.5, h: 0.05, fill: { color: ORANGE }, line: { color: ORANGE }
  });

  const stats = [
    { num: "90+", label: "entités métier", desc: "Salariés, contrats, bulletins, congés…" },
    { num: "60+", label: "écrans XAF", desc: "List + detail views configurés" },
    { num: "6", label: "dashboards RH", desc: "Analytiques avec exports" },
    { num: "19", label: "pages d'aide", desc: "Step-by-step + cas d'usage ELTON" },
    { num: "200+", label: "permissions RBAC", desc: "4 rôles · granulaire par entité" },
    { num: "5", label: "workflows", desc: "Congés · Missions · Formations · Évaluations · Avancements" },
    { num: "4", label: "déclarations sociales", desc: "IPRES · CSS · VRS · État 1024 (DGID)" },
    { num: "100%", label: "actions tracées", desc: "Journal d'audit immutable" },
  ];

  let row = 0, col = 0;
  stats.forEach((stat, idx) => {
    const x = 0.5 + col * 2.4;
    const y = 1.3 + row * 1.95;
    s.addShape(pres.shapes.RECTANGLE, {
      x, y, w: 2.2, h: 1.75, fill: { color: NAVY_LIGHT }, line: { color: ORANGE, width: 2 }
    });
    s.addText(stat.num, {
      x, y: y + 0.1, w: 2.2, h: 0.7, fontSize: 36, fontFace: FONT_HEADER_BOLD,
      bold: true, color: ORANGE, align: "center", valign: "middle", margin: 0
    });
    s.addText(stat.label, {
      x, y: y + 0.85, w: 2.2, h: 0.35, fontSize: 13, fontFace: FONT_HEADER_BOLD,
      bold: true, color: WHITE, align: "center", valign: "middle", margin: 0
    });
    s.addText(stat.desc, {
      x: x + 0.1, y: y + 1.2, w: 2.0, h: 0.5, fontSize: 9, fontFace: FONT_BODY,
      color: TEXT_LIGHT, italic: true, align: "center", valign: "middle", margin: 0
    });
    col++;
    if (col === 4) { col = 0; row++; }
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 7 — MODULE PAIE 1/2 (Bulletins, Périodes, Déclarations)
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Module Paie · Cycle mensuel", "Bulletins, périodes, déclarations sociales");

  // Workflow visuel
  const steps = [
    { label: "Période ouverte", icon: "📅" },
    { label: "Bulletins générés", icon: "📄" },
    { label: "Recalcul cotisations", icon: "🔄" },
    { label: "Validation", icon: "✅" },
    { label: "PDF + Email", icon: "📧" },
    { label: "Clôture", icon: "🔒" },
  ];
  const stepW = 1.4, stepH = 0.85, gap = 0.1;
  let x0 = 0.5;
  steps.forEach((step, i) => {
    s.addShape(pres.shapes.RECTANGLE, {
      x: x0, y: 1.2, w: stepW, h: stepH, fill: { color: NAVY }, line: { color: NAVY }
    });
    s.addText(step.icon, {
      x: x0, y: 1.25, w: stepW, h: 0.4, fontSize: 18, align: "center", valign: "middle", margin: 0
    });
    s.addText(step.label, {
      x: x0, y: 1.65, w: stepW, h: 0.35, fontSize: 9, fontFace: FONT_BODY,
      color: WHITE, bold: true, align: "center", valign: "middle", margin: 0
    });
    if (i < steps.length - 1) {
      s.addText("▶", {
        x: x0 + stepW, y: 1.2, w: gap, h: stepH, fontSize: 14,
        color: ORANGE, align: "center", valign: "middle", margin: 0
      });
    }
    x0 += stepW + gap;
  });

  // 2 colonnes
  s.addText("📋 Calculs intégrés (droit social Sénégal)", {
    x: 0.5, y: 2.4, w: 4.5, h: 0.3, fontSize: 13, fontFace: FONT_HEADER_BOLD,
    bold: true, color: ORANGE, margin: 0
  });
  s.addText([
    { text: "IPRES RG (5,60% / 8,40% — plafond 432 000)", options: { bullet: true, breakLine: true } },
    { text: "IPRES RC cadres (2,40% / 3,60% — plafond 1 296 000)", options: { bullet: true, breakLine: true } },
    { text: "CSS AT (Accident travail variable)", options: { bullet: true, breakLine: true } },
    { text: "CSS AF (Allocation familiale variable)", options: { bullet: true, breakLine: true } },
    { text: "IR progressif (DGID) avec abattement 30% / 900 000 FCFA", options: { bullet: true, breakLine: true } },
    { text: "TRIMF mensuel + annuel", options: { bullet: true, breakLine: true } },
    { text: "CFCE (base brut fiscal)", options: { bullet: true } }
  ], {
    x: 0.5, y: 2.75, w: 4.5, h: 2.3, fontSize: 11, fontFace: FONT_BODY,
    color: TEXT, paraSpaceAfter: 3, margin: 0
  });

  // Déclarations
  s.addText("📊 Déclarations exportables (Excel)", {
    x: 5.2, y: 2.4, w: 4.5, h: 0.3, fontSize: 13, fontFace: FONT_HEADER_BOLD,
    bold: true, color: ORANGE, margin: 0
  });
  s.addText([
    { text: "Bordereau IPRES (mensuel)", options: { bullet: true, breakLine: true } },
    { text: "Bordereau CSS (mensuel)", options: { bullet: true, breakLine: true } },
    { text: "État VRS — Versement Retenue à la Source (DGID, mensuel)", options: { bullet: true, breakLine: true } },
    { text: "Déclaration 1024 (DGID, annuelle)", options: { bullet: true, breakLine: true } },
    { text: "Livre de Paie obligatoire (art. L.120, conservation 5 ans)", options: { bullet: true, breakLine: true } },
    { text: "PDF bulletin individuel + envoi email automatique", options: { bullet: true, breakLine: true } },
    { text: "Génération en masse 200+ bulletins en 1 clic", options: { bullet: true } }
  ], {
    x: 5.2, y: 2.75, w: 4.5, h: 2.3, fontSize: 11, fontFace: FONT_BODY,
    color: TEXT, paraSpaceAfter: 3, margin: 0
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 8 — MODULE PAIE 2/2 (Prêts, HS, Avance, Régul)
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Module Paie · Fonctionnalités avancées", "Prêts, heures sup, régularisations");

  // 3 cartes
  const cards = [
    {
      x: 0.4, color: ORANGE, icon: "💰", title: "Prêts & avances",
      bullets: [
        "Échéancier auto (principal constant ou annuité)",
        "Taux annuel configurable",
        "Retenue auto sur bulletin chaque mois",
        "Suspension / reprise / clôture",
        "Avance ponctuelle = taux 0%"
      ]
    },
    {
      x: 3.55, color: NAVY, icon: "⏰", title: "Heures supplémentaires",
      bullets: [
        "Activable via Paramètres de paie",
        "4 taux légaux SN (15/40/60/100%)",
        "Calcul auto à partir du SB / 173,33",
        "Intégrées au Brut Fiscal & Social",
        "Cotisations recalculées avec HS"
      ]
    },
    {
      x: 6.7, color: NAVY_LIGHT, icon: "📐", title: "Régularisations",
      bullets: [
        "Cumul annuel IR / TRIMF / IPRES",
        "Régul fin d'année automatique",
        "Régul mois de départ (offboarding)",
        "Réduction familiale par parts",
        "Simulation avant validation"
      ]
    },
  ];

  cards.forEach(c => {
    s.addShape(pres.shapes.RECTANGLE, {
      x: c.x, y: 1.2, w: 2.95, h: 3.85, fill: { color: WHITE }, line: { color: "DDDDDD" },
      shadow: makeShadow()
    });
    s.addShape(pres.shapes.RECTANGLE, {
      x: c.x, y: 1.2, w: 2.95, h: 0.6, fill: { color: c.color }, line: { color: c.color }
    });
    s.addText(c.icon, {
      x: c.x + 0.15, y: 1.25, w: 0.5, h: 0.5, fontSize: 22, valign: "middle", margin: 0
    });
    s.addText(c.title, {
      x: c.x + 0.7, y: 1.2, w: 2.2, h: 0.6, fontSize: 14, fontFace: FONT_HEADER_BOLD,
      bold: true, color: WHITE, valign: "middle", margin: 0
    });
    s.addText(
      c.bullets.map((b, i) => ({
        text: b,
        options: { bullet: true, breakLine: i < c.bullets.length - 1 }
      })),
      {
        x: c.x + 0.2, y: 1.95, w: 2.65, h: 3.0, fontSize: 11, fontFace: FONT_BODY,
        color: TEXT, paraSpaceAfter: 6, margin: 0
      }
    );
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 9 — MODULE RH 1/3 (Salariés, Contrats)
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Module RH · Gestion du personnel", "Salariés, contrats, dossiers");

  // Sub-blocks 2x2
  const blocks = [
    {
      x: 0.4, y: 1.15, color: NAVY, icon: "👤", title: "Fiches salariés",
      desc: "Identité complète · Contact urgence · Pièces d'identité (CNI, passeport, IPRES) · Famille (conjoints, enfants) · Comptes bancaires multi-RIB · Pièces jointes scannées"
    },
    {
      x: 5.0, y: 1.15, color: ORANGE, icon: "📋", title: "Contrats de travail",
      desc: "CDI / CDD / Stage · Période d'essai · Avenants · Templates Word personnalisables · Génération PDF auto · Historique tous les contrats du salarié"
    },
    {
      x: 0.4, y: 3.2, color: NAVY_LIGHT, icon: "📁", title: "Dossiers salariés",
      desc: "Centralisation pièces RH · Type document configurable · Stockage chiffré · Recherche full-text · Permissions par rôle · Audit consultations"
    },
    {
      x: 5.0, y: 3.2, color: NAVY_DARK, icon: "🔄", title: "Avancements & promotions",
      desc: "Workflow complet : Salarié → N+1 → N+2 → DAF → RH applique · Changement échelon / fonction / catégorie · Date d'effet auto sur bulletins futurs · Historique tracé"
    },
  ];

  blocks.forEach(b => {
    s.addShape(pres.shapes.RECTANGLE, {
      x: b.x, y: b.y, w: 4.55, h: 1.95, fill: { color: WHITE }, line: { color: "DDDDDD" },
      shadow: makeShadow()
    });
    s.addShape(pres.shapes.RECTANGLE, {
      x: b.x, y: b.y, w: 4.55, h: 0.5, fill: { color: b.color }, line: { color: b.color }
    });
    s.addText(b.icon, {
      x: b.x + 0.15, y: b.y + 0.05, w: 0.4, h: 0.4, fontSize: 18, valign: "middle", margin: 0
    });
    s.addText(b.title, {
      x: b.x + 0.6, y: b.y, w: 3.85, h: 0.5, fontSize: 13, fontFace: FONT_HEADER_BOLD,
      bold: true, color: WHITE, valign: "middle", margin: 0
    });
    s.addText(b.desc, {
      x: b.x + 0.2, y: b.y + 0.6, w: 4.15, h: 1.3, fontSize: 11, fontFace: FONT_BODY,
      color: TEXT, valign: "top", margin: 0
    });
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 10 — MODULE RH 2/3 (Congés, Missions, Formations, Évaluations)
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Module RH · Workflows opérationnels", "Congés · Missions · Formations · Évaluations");

  const wfs = [
    {
      title: "🏖️ Congés & absences",
      flow: "Salarié → N+1 → N+2 → RH",
      details: "6 familles : Annuel, Maladie, Maternité, Événement, Sans solde, Récup · Soldes auto · Justificatifs"
    },
    {
      title: "✈️ Missions & déplacements",
      flow: "Salarié → N+1 → Asst.RH → RH → DAF → Comptable",
      details: "Frais prévisionnels vs réels · État de frais PDF · Google Maps distances · Templates ordres mission"
    },
    {
      title: "🎓 Formations",
      flow: "RH → Plan annuel → Sessions → Inscriptions → Présences → Attestations",
      details: "Évaluation à froid J+30 / J+90 · Capacité, liste d'attente · Certificats PDF · Bilan annuel par axe"
    },
    {
      title: "📝 Évaluations annuelles",
      flow: "RH (campagne) → N+1 (notes) → Salarié (auto-éval) → N+2 → RH (clôture)",
      details: "Grille critères pondérés · Score global auto · Objectifs SMART N+1 · Signature électronique"
    }
  ];

  let y = 1.15;
  wfs.forEach((w, i) => {
    s.addShape(pres.shapes.RECTANGLE, {
      x: 0.4, y, w: 9.2, h: 0.9, fill: { color: WHITE }, line: { color: "DDDDDD" }, shadow: makeShadow()
    });
    s.addShape(pres.shapes.RECTANGLE, {
      x: 0.4, y, w: 0.08, h: 0.9, fill: { color: ORANGE }, line: { color: ORANGE }
    });
    s.addText(w.title, {
      x: 0.6, y: y + 0.05, w: 3, h: 0.4, fontSize: 14, fontFace: FONT_HEADER_BOLD,
      bold: true, color: NAVY, valign: "middle", margin: 0
    });
    s.addText(w.flow, {
      x: 0.6, y: y + 0.45, w: 5, h: 0.4, fontSize: 10, fontFace: FONT_BODY,
      color: ORANGE, italic: true, bold: true, valign: "middle", margin: 0
    });
    s.addText(w.details, {
      x: 5.7, y: y + 0.05, w: 3.7, h: 0.8, fontSize: 9, fontFace: FONT_BODY,
      color: TEXT_SOFT, valign: "middle", margin: 0
    });
    y += 1.0;
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 11 — MODULE RH 3/3 (Disciplinaire, Offboarding, Heures sup)
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Module RH · Vie du contrat", "Disciplinaire & Offboarding (STC)");

  // Disciplinaire
  s.addShape(pres.shapes.RECTANGLE, {
    x: 0.4, y: 1.2, w: 4.5, h: 3.9, fill: { color: WHITE }, line: { color: "DDDDDD" }, shadow: makeShadow()
  });
  s.addShape(pres.shapes.RECTANGLE, {
    x: 0.4, y: 1.2, w: 4.5, h: 0.6, fill: { color: RED }, line: { color: RED }
  });
  s.addText("⚖️  Procédure disciplinaire", {
    x: 0.55, y: 1.2, w: 4.2, h: 0.6, fontSize: 14, fontFace: FONT_HEADER_BOLD,
    bold: true, color: WHITE, valign: "middle", margin: 0
  });
  s.addText("Conforme Code du Travail Sénégal", {
    x: 0.55, y: 1.85, w: 4.2, h: 0.3, fontSize: 10, fontFace: FONT_BODY,
    italic: true, color: NAVY, margin: 0
  });
  s.addText([
    { text: "1. Ouverture dossier (faute Simple/Grave/Lourde)", options: { bullet: true, breakLine: true } },
    { text: "2. Notification écrite au salarié", options: { bullet: true, breakLine: true } },
    { text: "3. Audition contradictoire (obligatoire)", options: { bullet: true, breakLine: true } },
    { text: "4. PV d'audition documenté", options: { bullet: true, breakLine: true } },
    { text: "5. Sanction motivée (max 8j mise à pied)", options: { bullet: true, breakLine: true } },
    { text: "6. Clôture & archivage", options: { bullet: true } }
  ], {
    x: 0.55, y: 2.25, w: 4.2, h: 2.7, fontSize: 11, fontFace: FONT_BODY,
    color: TEXT, paraSpaceAfter: 5, margin: 0
  });

  // Offboarding
  s.addShape(pres.shapes.RECTANGLE, {
    x: 5.1, y: 1.2, w: 4.5, h: 3.9, fill: { color: WHITE }, line: { color: "DDDDDD" }, shadow: makeShadow()
  });
  s.addShape(pres.shapes.RECTANGLE, {
    x: 5.1, y: 1.2, w: 4.5, h: 0.6, fill: { color: NAVY }, line: { color: NAVY }
  });
  s.addText("👋  Offboarding & STC", {
    x: 5.25, y: 1.2, w: 4.2, h: 0.6, fontSize: 14, fontFace: FONT_HEADER_BOLD,
    bold: true, color: WHITE, valign: "middle", margin: 0
  });
  s.addText("Solde de tout compte calculé selon droit SN", {
    x: 5.25, y: 1.85, w: 4.2, h: 0.3, fontSize: 10, fontFace: FONT_BODY,
    italic: true, color: NAVY, margin: 0
  });
  s.addText([
    { text: "Indemnité congés non pris (× SB / 26)", options: { bullet: true, breakLine: true } },
    { text: "Indemnité préavis (si licenciement / RC)", options: { bullet: true, breakLine: true } },
    { text: "Indemnité licenciement (paliers 25/30/40%)", options: { bullet: true, breakLine: true } },
    { text: "Prorata salarial du mois", options: { bullet: true, breakLine: true } },
    { text: "Workflow RH → DAF → Clôture", options: { bullet: true, breakLine: true } },
    { text: "Désactivation salarié + DateSortie auto", options: { bullet: true } }
  ], {
    x: 5.25, y: 2.25, w: 4.2, h: 2.7, fontSize: 11, fontFace: FONT_BODY,
    color: TEXT, paraSpaceAfter: 5, margin: 0
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 12 — MODULE INTÉRIMAIRES V1.1
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Module Intérimaires V1.1", "Refonte modèle hiérarchique (mai 2026)");

  // Hiérarchie visuelle
  s.addText("Modèle V1.1 — Hiérarchie organisationnelle", {
    x: 0.4, y: 1.1, w: 9, h: 0.4, fontSize: 14, fontFace: FONT_HEADER_BOLD,
    bold: true, color: ORANGE, margin: 0
  });

  // Niveau 1 : Site
  s.addShape(pres.shapes.RECTANGLE, {
    x: 1, y: 1.6, w: 8, h: 0.55, fill: { color: NAVY }, line: { color: NAVY }
  });
  s.addText("📍  SITE  (TypeSite enum)", {
    x: 1.1, y: 1.6, w: 4, h: 0.55, fontSize: 13, fontFace: FONT_HEADER_BOLD,
    bold: true, color: WHITE, valign: "middle", margin: 0
  });
  s.addText("🏪 Station service · 🏢 Siège · 📦 Dépôt · 📍 Autre", {
    x: 5, y: 1.6, w: 3.9, h: 0.55, fontSize: 11, fontFace: FONT_BODY,
    color: ORANGE_SOFT, italic: true, valign: "middle", margin: 0
  });

  // Niveau 2 : UniteOrganisationnelle
  s.addShape(pres.shapes.RECTANGLE, {
    x: 1.5, y: 2.4, w: 7, h: 0.55, fill: { color: NAVY_LIGHT }, line: { color: NAVY_LIGHT }
  });
  s.addText("🏛️  UNITÉ ORGANISATIONNELLE  (récursive Parent/Enfants)", {
    x: 1.6, y: 2.4, w: 6.8, h: 0.55, fontSize: 12, fontFace: FONT_HEADER_BOLD,
    bold: true, color: WHITE, valign: "middle", margin: 0
  });

  // Niveau 3 : sous-types
  const subTypes = [
    { x: 1.5, color: ORANGE, label: "🔵 BU\n(stations)" },
    { x: 3.4, color: NAVY, label: "🟢 Département\n(Siège)" },
    { x: 5.3, color: NAVY_LIGHT, label: "🟡 Segment\n(commercial)" },
    { x: 7.2, color: NAVY_DARK, label: "⚪ Autre" },
  ];
  subTypes.forEach(st => {
    s.addShape(pres.shapes.RECTANGLE, {
      x: st.x, y: 3.2, w: 1.8, h: 0.85, fill: { color: st.color }, line: { color: st.color }
    });
    s.addText(st.label, {
      x: st.x, y: 3.2, w: 1.8, h: 0.85, fontSize: 10, fontFace: FONT_BODY,
      color: WHITE, bold: true, align: "center", valign: "middle", margin: 0
    });
  });

  // Note multi-affectation
  s.addShape(pres.shapes.RECTANGLE, {
    x: 0.4, y: 4.4, w: 9.2, h: 0.7, fill: { color: ORANGE_SOFT }, line: { color: ORANGE }
  });
  s.addText([
    { text: "✨ Multi-affectation N-N : ", options: { bold: true, color: NAVY } },
    { text: "un intérimaire peut être affecté à PLUSIEURS unités simultanément (sans % de temps). ", options: { color: NAVY } },
    { text: "Mouvements traçables entre sites.", options: { italic: true, color: NAVY } }
  ], {
    x: 0.55, y: 4.4, w: 9, h: 0.7, fontSize: 11, fontFace: FONT_BODY,
    valign: "middle", margin: 0
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 13 — IMPORTS EN MASSE
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Imports en masse Excel", "Migration et mises à jour rapides");

  s.addText("Workflow d'import (idempotent · zéro doublon)", {
    x: 0.4, y: 1.1, w: 9, h: 0.4, fontSize: 14, fontFace: FONT_HEADER_BOLD,
    bold: true, color: ORANGE, margin: 0
  });

  // Workflow horizontal
  const wfSteps = [
    { x: 0.5, label: "📥 Télécharger\nmodèle Excel" },
    { x: 2.4, label: "✏️ Remplir\ndonnées" },
    { x: 4.3, label: "📤 Importer\n.xlsx" },
    { x: 6.2, label: "✅ Validation\nauto" },
    { x: 8.1, label: "📊 Rapport\nx créés / y ignorés" }
  ];
  wfSteps.forEach((st, i) => {
    s.addShape(pres.shapes.RECTANGLE, {
      x: st.x, y: 1.6, w: 1.7, h: 0.85, fill: { color: NAVY }, line: { color: NAVY }
    });
    s.addText(st.label, {
      x: st.x, y: 1.6, w: 1.7, h: 0.85, fontSize: 10, fontFace: FONT_BODY,
      color: WHITE, bold: true, align: "center", valign: "middle", margin: 0
    });
    if (i < 4) {
      s.addText("→", {
        x: st.x + 1.7, y: 1.6, w: 0.2, h: 0.85, fontSize: 18,
        color: ORANGE, bold: true, align: "center", valign: "middle", margin: 0
      });
    }
  });

  // 3 imports disponibles
  const imports = [
    {
      x: 0.4, color: ORANGE, title: "👥 Salariés",
      desc: "Matricule · Nom/Prénom · État civil · CNI · Échelon · SB/Logt · Département · Fonction · Email/Tel"
    },
    {
      x: 3.55, color: NAVY, title: "👫 Conjoints",
      desc: "Polygamie supportée · DateMariage · DateFinUnion · Statut Actif/Inactif · Impact parts fiscales auto"
    },
    {
      x: 6.7, color: NAVY_LIGHT, title: "🏦 Comptes bancaires (RIB)",
      desc: "Multi-comptes · Modes Reliquat / MontantFixe / Pourcentage · Validation 100% · BIC/SWIFT optionnel"
    }
  ];
  imports.forEach(imp => {
    s.addShape(pres.shapes.RECTANGLE, {
      x: imp.x, y: 2.85, w: 2.95, h: 2.2, fill: { color: WHITE }, line: { color: "DDDDDD" }, shadow: makeShadow()
    });
    s.addShape(pres.shapes.RECTANGLE, {
      x: imp.x, y: 2.85, w: 2.95, h: 0.5, fill: { color: imp.color }, line: { color: imp.color }
    });
    s.addText(imp.title, {
      x: imp.x + 0.15, y: 2.85, w: 2.7, h: 0.5, fontSize: 13, fontFace: FONT_HEADER_BOLD,
      bold: true, color: WHITE, valign: "middle", margin: 0
    });
    s.addText(imp.desc, {
      x: imp.x + 0.2, y: 3.5, w: 2.65, h: 1.5, fontSize: 11, fontFace: FONT_BODY,
      color: TEXT, valign: "top", margin: 0
    });
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 17 — MODULE COMPTABILITÉ (NOUVEAU)
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Module Comptabilité", "Génération automatique des écritures depuis la paie");

  // Workflow visuel : Bulletin → Écriture → Plan comptable
  const wfSteps = [
    { x: 0.5, label: "📄 Bulletin\nvalidé" },
    { x: 2.4, label: "🔄 Génération\nauto" },
    { x: 4.3, label: "📒 Écritures\ncomptables" },
    { x: 6.2, label: "📊 Plan\ncomptable" },
    { x: 8.1, label: "📤 Export\nlogiciel compta" }
  ];
  wfSteps.forEach((st, i) => {
    s.addShape(pres.shapes.RECTANGLE, {
      x: st.x, y: 1.2, w: 1.7, h: 0.85, fill: { color: NAVY }, line: { color: NAVY }
    });
    s.addText(st.label, {
      x: st.x, y: 1.2, w: 1.7, h: 0.85, fontSize: 10, fontFace: FONT_BODY,
      color: WHITE, bold: true, align: "center", valign: "middle", margin: 0
    });
    if (i < 4) {
      s.addText("→", {
        x: st.x + 1.7, y: 1.2, w: 0.2, h: 0.85, fontSize: 18,
        color: ORANGE, bold: true, align: "center", valign: "middle", margin: 0
      });
    }
  });

  // 2 colonnes : Plan comptable / Export
  s.addText("📒 Plan comptable & Écritures", {
    x: 0.5, y: 2.4, w: 4.5, h: 0.3, fontSize: 13, fontFace: FONT_HEADER_BOLD,
    bold: true, color: ORANGE, margin: 0
  });
  s.addText([
    { text: "Plan comptable OHADA / SYSCOHADA", options: { bullet: true, breakLine: true } },
    { text: "Comptes paramétrables par rubrique de paie", options: { bullet: true, breakLine: true } },
    { text: "Mapping automatique : RubriqueCompte / Override", options: { bullet: true, breakLine: true } },
    { text: "Génération écritures à la validation bulletin", options: { bullet: true, breakLine: true } },
    { text: "Numérotation continue par exercice", options: { bullet: true, breakLine: true } },
    { text: "Réconciliation cumul mensuel / annuel", options: { bullet: true, breakLine: true } },
    { text: "Soft-delete (GCRecord) pour audit conformité", options: { bullet: true } }
  ], {
    x: 0.5, y: 2.75, w: 4.5, h: 2.3, fontSize: 11, fontFace: FONT_BODY,
    color: TEXT, paraSpaceAfter: 3, margin: 0
  });

  s.addText("📤 Exports comptables", {
    x: 5.2, y: 2.4, w: 4.5, h: 0.3, fontSize: 13, fontFace: FONT_HEADER_BOLD,
    bold: true, color: ORANGE, margin: 0
  });
  s.addText([
    { text: "Export Excel par période (toutes écritures)", options: { bullet: true, breakLine: true } },
    { text: "Export par lot (batch périodes / compte)", options: { bullet: true, breakLine: true } },
    { text: "Format compatible Sage / Ciel / EBP", options: { bullet: true, breakLine: true } },
    { text: "Génération en masse avec compteur d'erreurs", options: { bullet: true, breakLine: true } },
    { text: "Audit : qui a exporté quoi quand", options: { bullet: true, breakLine: true } },
    { text: "Rejeu possible si erreur détectée", options: { bullet: true, breakLine: true } },
    { text: "Permissions DAF/Comptable séparées", options: { bullet: true } }
  ], {
    x: 5.2, y: 2.75, w: 4.5, h: 2.3, fontSize: 11, fontFace: FONT_BODY,
    color: TEXT, paraSpaceAfter: 3, margin: 0
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 18 — ESPACE SALARIÉ + ALERTES RH (NOUVEAU, sans Rapport CEO déprécié)
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Espace Salarié & Alertes RH", "Self-service collaborateurs + notifications automatiques");

  // 2 cartes
  // Espace salarié (gauche)
  s.addShape(pres.shapes.RECTANGLE, {
    x: 0.4, y: 1.2, w: 4.5, h: 3.9, fill: { color: WHITE }, line: { color: "DDDDDD" },
    shadow: makeShadow()
  });
  s.addShape(pres.shapes.RECTANGLE, {
    x: 0.4, y: 1.2, w: 4.5, h: 0.6, fill: { color: NAVY }, line: { color: NAVY }
  });
  s.addText("👤  Espace Salarié (self-service)", {
    x: 0.55, y: 1.2, w: 4.2, h: 0.6, fontSize: 14, fontFace: FONT_HEADER_BOLD,
    bold: true, color: WHITE, valign: "middle", margin: 0
  });
  s.addText("Chaque salarié accède à ses données 24/7", {
    x: 0.55, y: 1.85, w: 4.2, h: 0.3, fontSize: 10, fontFace: FONT_BODY,
    italic: true, color: NAVY, margin: 0
  });
  s.addText([
    { text: "Mes bulletins de paie (téléchargement PDF chiffré)", options: { bullet: true, breakLine: true } },
    { text: "Mes congés (demande, soldes, historique)", options: { bullet: true, breakLine: true } },
    { text: "Mes déplacements (saisie + frais réels)", options: { bullet: true, breakLine: true } },
    { text: "Mes attestations (travail, salaire, congés)", options: { bullet: true, breakLine: true } },
    { text: "Mon entretien annuel (auto-évaluation)", options: { bullet: true, breakLine: true } },
    { text: "Mes formations (inscription, attestations)", options: { bullet: true, breakLine: true } },
    { text: "Mes documents personnels (sécurisé)", options: { bullet: true } }
  ], {
    x: 0.55, y: 2.25, w: 4.2, h: 2.7, fontSize: 11, fontFace: FONT_BODY,
    color: TEXT, paraSpaceAfter: 4, margin: 0
  });

  // Alertes & Notifications (droite)
  s.addShape(pres.shapes.RECTANGLE, {
    x: 5.1, y: 1.2, w: 4.5, h: 3.9, fill: { color: WHITE }, line: { color: "DDDDDD" },
    shadow: makeShadow()
  });
  s.addShape(pres.shapes.RECTANGLE, {
    x: 5.1, y: 1.2, w: 4.5, h: 0.6, fill: { color: ORANGE }, line: { color: ORANGE }
  });
  s.addText("🔔  Alertes & Notifications RH", {
    x: 5.25, y: 1.2, w: 4.2, h: 0.6, fontSize: 14, fontFace: FONT_HEADER_BOLD,
    bold: true, color: WHITE, valign: "middle", margin: 0
  });
  s.addText("Hosted services .NET tournant en arrière-plan", {
    x: 5.25, y: 1.85, w: 4.2, h: 0.3, fontSize: 10, fontFace: FONT_BODY,
    italic: true, color: NAVY, margin: 0
  });
  s.addText([
    { text: "Attestation en attente RH > N jours (relance auto)", options: { bullet: true, breakLine: true } },
    { text: "Dossier intérimaire approchant l\'expiration (CNI/contrat)", options: { bullet: true, breakLine: true } },
    { text: "Évaluation à froid formation (J+30 puis J+90)", options: { bullet: true, breakLine: true } },
    { text: "Alertes intérimaires (fin de mission proche)", options: { bullet: true, breakLine: true } },
    { text: "Notifications XAF par module (workflow)", options: { bullet: true, breakLine: true } },
    { text: "Configuration heure d\'envoi + emails RH dans Paramètres", options: { bullet: true, breakLine: true } },
    { text: "Pour le pilotage exécutif → 6 dashboards RH (cf. § Décisionnel)", options: { bullet: true } }
  ], {
    x: 5.25, y: 2.25, w: 4.2, h: 2.7, fontSize: 11, fontFace: FONT_BODY,
    color: TEXT, paraSpaceAfter: 4, margin: 0
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 19 — PARAMÉTRAGE & RÉFÉRENTIELS (NOUVEAU)
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Paramétrage & Référentiels", "Tout ce qui rend l'app adaptable sans redéploiement");

  // Sub-blocks 2x2
  const blocks = [
    {
      x: 0.4, y: 1.15, color: NAVY, icon: "💼", title: "Rubriques & Barèmes",
      desc: "Rubriques paie (canonique, taux salarial/patronal) · Barèmes IR/TRIMF mis à jour annuellement (lois finances) · Réduction familiale par parts · Cotisations IPRES/CSS configurables"
    },
    {
      x: 5.0, y: 1.15, color: ORANGE, icon: "🏗️", title: "Structure organisationnelle",
      desc: "Sites + TypeSite (Station/Siège/Dépôt) · Unités hiérarchiques · Départements · Fonctions · Échelons · Catégories · Conventions collectives · Villes Sénégal référentiel"
    },
    {
      x: 0.4, y: 3.2, color: BLUE_DARK, icon: "📝", title: "Templates Word (.docx)",
      desc: "Attestations (travail, salaire, congés) · Certificats emploi · Cessation paiement · Ordres de mission · État de frais · Contrats CDI/CDD/Stage · Entretien annuel · Bilan social · Variables {{...}} dynamiques"
    },
    {
      x: 5.0, y: 3.2, color: NAVY_DARK, icon: "📧", title: "Communication & Intégrations",
      desc: "SMTP / Microsoft Graph (Office 365) · Test email intégré · Adresses RH / DAF / Comptable configurables · Google Maps API (distances missions) · GeoNames (villes) · Power BI embedding"
    },
  ];

  blocks.forEach(b => {
    s.addShape(pres.shapes.RECTANGLE, {
      x: b.x, y: b.y, w: 4.55, h: 1.95, fill: { color: WHITE }, line: { color: "DDDDDD" },
      shadow: makeShadow()
    });
    s.addShape(pres.shapes.RECTANGLE, {
      x: b.x, y: b.y, w: 4.55, h: 0.5, fill: { color: b.color }, line: { color: b.color }
    });
    s.addText(b.icon, {
      x: b.x + 0.15, y: b.y + 0.05, w: 0.4, h: 0.4, fontSize: 18, valign: "middle", margin: 0
    });
    s.addText(b.title, {
      x: b.x + 0.6, y: b.y, w: 3.85, h: 0.5, fontSize: 13, fontFace: FONT_HEADER_BOLD,
      bold: true, color: WHITE, valign: "middle", margin: 0
    });
    s.addText(b.desc, {
      x: b.x + 0.2, y: b.y + 0.6, w: 4.15, h: 1.3, fontSize: 9, fontFace: FONT_BODY,
      color: TEXT, valign: "top", margin: 0
    });
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 17 — DASHBOARDS APERÇU
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Tableaux de bord RH", "6 dashboards analytiques pour le pilotage");

  const dashboards = [
    { x: 0.4, y: 1.15, icon: "📊", title: "1. Effectif détaillé", desc: "Pyramide des âges × catégorie pro · Distinction H/F · Évolution 3 ans" },
    { x: 5.0, y: 1.15, icon: "📈", title: "2. Analyse Effectif", desc: "Effectif global/moyen · Évolution 8 ans · Turnover · Démographie" },
    { x: 0.4, y: 2.5, icon: "🔄", title: "3. Mouvements", desc: "Arrivées / Départs · Par mois, motif, site, catégorie · Solde net" },
    { x: 5.0, y: 2.5, icon: "💰", title: "4. Rémunération", desc: "Masse salariale · Coût employeur · Égalité H/F segment & catégorie" },
    { x: 0.4, y: 3.85, icon: "🏖️", title: "5. Suivi Absences", desc: "Taux d'absentéisme · Top 10 absents · Famille de congé · Évolution" },
    { x: 5.0, y: 3.85, icon: "📋", title: "6. Bilan Social Mensuel", desc: "12 mois × 8 indicateurs DTSS · Synthèse annuelle · Charges" }
  ];

  dashboards.forEach(d => {
    s.addShape(pres.shapes.RECTANGLE, {
      x: d.x, y: d.y, w: 4.55, h: 1.25, fill: { color: WHITE }, line: { color: "DDDDDD" }, shadow: makeShadow()
    });
    s.addShape(pres.shapes.RECTANGLE, {
      x: d.x, y: d.y, w: 0.08, h: 1.25, fill: { color: ORANGE }, line: { color: ORANGE }
    });
    s.addText(d.icon, {
      x: d.x + 0.2, y: d.y + 0.1, w: 0.8, h: 1.05, fontSize: 32, align: "center", valign: "middle", margin: 0
    });
    s.addText(d.title, {
      x: d.x + 1.0, y: d.y + 0.1, w: 3.4, h: 0.4, fontSize: 13, fontFace: FONT_HEADER_BOLD,
      bold: true, color: NAVY, valign: "middle", margin: 0
    });
    s.addText(d.desc, {
      x: d.x + 1.0, y: d.y + 0.5, w: 3.4, h: 0.7, fontSize: 10, fontFace: FONT_BODY,
      color: TEXT_SOFT, valign: "top", margin: 0
    });
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 18 — DASHBOARD EXEMPLE (Effectif chart)
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Dashboard exemple — Effectif détaillé", "Pyramide des âges Hommes/Femmes × catégorie");

  // Chart placeholder (utiliser un vrai chart)
  s.addChart(
    pres.charts.BAR,
    [
      { name: "Hommes", labels: ["<25", "25-34", "35-44", "45-54", "55+"], values: [-12, -28, -42, -25, -8] },
      { name: "Femmes", labels: ["<25", "25-34", "35-44", "45-54", "55+"], values: [8, 22, 18, 12, 5] }
    ],
    {
      x: 0.4, y: 1.2, w: 6.5, h: 3.7, barDir: "bar",
      chartColors: [NAVY, ORANGE], showLegend: true, legendPos: "b",
      catAxisLabelColor: TEXT_SOFT, valAxisLabelColor: TEXT_SOFT,
      valGridLine: { color: "E2E8F0", size: 0.5 },
      barGrouping: "stacked",
      showTitle: false,
      chartArea: { fill: { color: WHITE }, roundedCorners: false }
    }
  );

  // KPI à droite
  const kpis = [
    { label: "Effectif total", value: "247", sub: "actifs au 31/12" },
    { label: "Âge moyen", value: "38 ans", sub: "Hommes 39 · Femmes 35" },
    { label: "% Femmes", value: "26%", sub: "+ 3 pts vs 2025" },
    { label: "Ancienneté moy.", value: "7,2 ans", sub: "stable" }
  ];
  let y = 1.2;
  kpis.forEach(k => {
    s.addShape(pres.shapes.RECTANGLE, {
      x: 7.1, y, w: 2.5, h: 0.85, fill: { color: WHITE }, line: { color: "DDDDDD" }, shadow: makeShadow()
    });
    s.addShape(pres.shapes.RECTANGLE, {
      x: 7.1, y, w: 0.08, h: 0.85, fill: { color: ORANGE }, line: { color: ORANGE }
    });
    s.addText(k.label, {
      x: 7.25, y: y + 0.05, w: 2.3, h: 0.3, fontSize: 10, fontFace: FONT_BODY,
      color: TEXT_SOFT, valign: "middle", margin: 0
    });
    s.addText(k.value, {
      x: 7.25, y: y + 0.27, w: 2.3, h: 0.4, fontSize: 22, fontFace: FONT_HEADER_BOLD,
      bold: true, color: NAVY, valign: "middle", margin: 0
    });
    s.addText(k.sub, {
      x: 7.25, y: y + 0.62, w: 2.3, h: 0.2, fontSize: 8, fontFace: FONT_BODY,
      color: TEXT_SOFT, italic: true, valign: "middle", margin: 0
    });
    y += 0.93;
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 19 — EXPORTS & ACTIONS DASHBOARDS
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Exports & actions sur dashboards", "Excel, PDF, refresh, aide intégrée");

  // 4 cartes actions
  const actions = [
    { x: 0.4, color: ORANGE, icon: "📊", title: "Export Excel", desc: "Onglet Synthèse + détaillés · DataBars colorés · Prêt copier-coller" },
    { x: 2.85, color: NAVY, icon: "📄", title: "Export PDF", desc: "A4 paysage · En-tête ELTON · 1 page imprimable · QuestPDF" },
    { x: 5.3, color: NAVY_LIGHT, icon: "↻", title: "Refresh manuel", desc: "Vide le cache TTL 5 min · Recharge depuis BDD · Bouton header" },
    { x: 7.75, color: NAVY_DARK, icon: "❓", title: "Aide intégrée", desc: "Page HTML dédiée par dashboard · Cas d'usage · Glossaire" }
  ];
  actions.forEach(a => {
    s.addShape(pres.shapes.RECTANGLE, {
      x: a.x, y: 1.2, w: 2.0, h: 2.0, fill: { color: WHITE }, line: { color: "DDDDDD" }, shadow: makeShadow()
    });
    s.addShape(pres.shapes.OVAL, {
      x: a.x + 0.65, y: 1.4, w: 0.7, h: 0.7, fill: { color: a.color }, line: { color: a.color }
    });
    s.addText(a.icon, {
      x: a.x + 0.65, y: 1.4, w: 0.7, h: 0.7, fontSize: 18, color: WHITE, align: "center", valign: "middle", margin: 0
    });
    s.addText(a.title, {
      x: a.x, y: 2.2, w: 2.0, h: 0.35, fontSize: 12, fontFace: FONT_HEADER_BOLD,
      bold: true, color: NAVY, align: "center", valign: "middle", margin: 0
    });
    s.addText(a.desc, {
      x: a.x + 0.1, y: 2.55, w: 1.8, h: 0.55, fontSize: 9, fontFace: FONT_BODY,
      color: TEXT_SOFT, align: "center", valign: "top", margin: 0
    });
  });

  // Bandeau bas : highlight
  s.addShape(pres.shapes.RECTANGLE, {
    x: 0.4, y: 3.5, w: 9.2, h: 1.5, fill: { color: NAVY }, line: { color: NAVY }
  });
  s.addText("🎯  Cas d'usage CODIR", {
    x: 0.6, y: 3.6, w: 9, h: 0.4, fontSize: 14, fontFace: FONT_HEADER_BOLD,
    bold: true, color: ORANGE, valign: "middle", margin: 0
  });
  s.addText([
    { text: "Lundi matin : ", options: { bold: true, color: WHITE } },
    { text: "le DG ouvre le Bilan Social Mensuel → 12 mois × 8 indicateurs en 3 secondes.   ", options: { color: TEXT_LIGHT } },
    { text: "Préparation Comité d'entreprise : ", options: { bold: true, color: WHITE } },
    { text: "export Excel Synthèse → coller dans Word → Powerpoint.   ", options: { color: TEXT_LIGHT } },
    { text: "Audit DTSS : ", options: { bold: true, color: WHITE } },
    { text: "PDF prêt à imprimer avec en-tête conforme.", options: { color: TEXT_LIGHT } }
  ], {
    x: 0.6, y: 4.0, w: 9, h: 1.0, fontSize: 11, fontFace: FONT_BODY,
    valign: "top", margin: 0
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 20 — RBAC SÉCURITÉ
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Sécurité · RBAC 4 rôles", "Contrôle d'accès basé sur les rôles");

  // Tableau matrice
  const tableData = [
    [
      { text: "Rôle", options: { bold: true, color: WHITE, fill: { color: NAVY }, valign: "middle" } },
      { text: "Bulletins", options: { bold: true, color: WHITE, fill: { color: NAVY }, align: "center", valign: "middle" } },
      { text: "Salariés", options: { bold: true, color: WHITE, fill: { color: NAVY }, align: "center", valign: "middle" } },
      { text: "Dashboards", options: { bold: true, color: WHITE, fill: { color: NAVY }, align: "center", valign: "middle" } },
      { text: "Paramètres", options: { bold: true, color: WHITE, fill: { color: NAVY }, align: "center", valign: "middle" } },
      { text: "Audit", options: { bold: true, color: WHITE, fill: { color: NAVY }, align: "center", valign: "middle" } }
    ],
    [
      { text: "Administrators", options: { bold: true, color: NAVY, valign: "middle" } },
      { text: "✅ CRUD", options: { color: GREEN, align: "center", valign: "middle" } },
      { text: "✅ CRUD", options: { color: GREEN, align: "center", valign: "middle" } },
      { text: "✅ Tous", options: { color: GREEN, align: "center", valign: "middle" } },
      { text: "✅ Tous", options: { color: GREEN, align: "center", valign: "middle" } },
      { text: "✅ Lecture", options: { color: GREEN, align: "center", valign: "middle" } }
    ],
    [
      { text: "RH_Manager (DG/COMEX)", options: { bold: true, color: NAVY, valign: "middle" } },
      { text: "👁 Lecture", options: { color: NAVY_LIGHT, align: "center", valign: "middle" } },
      { text: "👁 Lecture", options: { color: NAVY_LIGHT, align: "center", valign: "middle" } },
      { text: "✅ Tous", options: { color: GREEN, align: "center", valign: "middle" } },
      { text: "—", options: { color: TEXT_SOFT, align: "center", valign: "middle" } },
      { text: "👁 Lecture", options: { color: NAVY_LIGHT, align: "center", valign: "middle" } }
    ],
    [
      { text: "RH (équipe RH)", options: { bold: true, color: NAVY, valign: "middle" } },
      { text: "✅ CRUD", options: { color: GREEN, align: "center", valign: "middle" } },
      { text: "✅ CRUD", options: { color: GREEN, align: "center", valign: "middle" } },
      { text: "✅ Tous", options: { color: GREEN, align: "center", valign: "middle" } },
      { text: "👁 Lecture", options: { color: NAVY_LIGHT, align: "center", valign: "middle" } },
      { text: "—", options: { color: TEXT_SOFT, align: "center", valign: "middle" } }
    ],
    [
      { text: "DAF (direction fin.)", options: { bold: true, color: NAVY, valign: "middle" } },
      { text: "👁 Lecture + Validation", options: { color: NAVY_LIGHT, align: "center", valign: "middle" } },
      { text: "👁 Lecture", options: { color: NAVY_LIGHT, align: "center", valign: "middle" } },
      { text: "✅ Tous", options: { color: GREEN, align: "center", valign: "middle" } },
      { text: "—", options: { color: TEXT_SOFT, align: "center", valign: "middle" } },
      { text: "👁 Lecture", options: { color: NAVY_LIGHT, align: "center", valign: "middle" } }
    ],
    [
      { text: "Default / Comptable", options: { bold: true, color: TEXT_SOFT, valign: "middle" } },
      { text: "Aucun", options: { color: RED, align: "center", valign: "middle" } },
      { text: "Aucun", options: { color: RED, align: "center", valign: "middle" } },
      { text: "Aucun (redirige login)", options: { color: RED, align: "center", valign: "middle" } },
      { text: "Aucun", options: { color: RED, align: "center", valign: "middle" } },
      { text: "Aucun", options: { color: RED, align: "center", valign: "middle" } }
    ]
  ];

  s.addTable(tableData, {
    x: 0.4, y: 1.2, w: 9.2, h: 3.0,
    fontSize: 10, fontFace: FONT_BODY, color: TEXT,
    border: { pt: 0.5, color: "CCCCCC" },
    colW: [2.0, 1.6, 1.4, 1.4, 1.4, 1.4],
    rowH: 0.5
  });

  // Note bas
  s.addShape(pres.shapes.RECTANGLE, {
    x: 0.4, y: 4.4, w: 9.2, h: 0.7, fill: { color: ORANGE_SOFT }, line: { color: ORANGE }
  });
  s.addText([
    { text: "🔒 Sécurité XAF native : ", options: { bold: true, color: NAVY } },
    { text: "permissions granulaires par entité × action × propriété. ", options: { color: NAVY } },
    { text: "Authentification ASP.NET Identity + Cookies HTTPS only. Lockout après 5 tentatives.", options: { italic: true, color: NAVY } }
  ], {
    x: 0.55, y: 4.4, w: 9, h: 0.7, fontSize: 10, fontFace: FONT_BODY,
    valign: "middle", margin: 0
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 21 — AUDIT & TRAÇABILITÉ
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Audit & traçabilité", "Toutes les actions sensibles tracées (immutable)");

  // Diagramme : action → audit
  s.addShape(pres.shapes.RECTANGLE, {
    x: 0.5, y: 1.2, w: 2.5, h: 1.3, fill: { color: NAVY_LIGHT }, line: { color: NAVY_LIGHT }
  });
  s.addText("🎬\nAction métier", {
    x: 0.5, y: 1.2, w: 2.5, h: 1.3, fontSize: 14, fontFace: FONT_HEADER_BOLD,
    bold: true, color: WHITE, align: "center", valign: "middle", margin: 0
  });

  s.addText("→", {
    x: 3.0, y: 1.2, w: 0.5, h: 1.3, fontSize: 30, color: ORANGE, bold: true, align: "center", valign: "middle", margin: 0
  });

  s.addShape(pres.shapes.RECTANGLE, {
    x: 3.6, y: 1.2, w: 2.5, h: 1.3, fill: { color: ORANGE }, line: { color: ORANGE }
  });
  s.addText("⚙️\nCapture auto\n(transparente)", {
    x: 3.6, y: 1.2, w: 2.5, h: 1.3, fontSize: 12, fontFace: FONT_HEADER_BOLD,
    bold: true, color: WHITE, align: "center", valign: "middle", margin: 0
  });

  s.addText("→", {
    x: 6.1, y: 1.2, w: 0.5, h: 1.3, fontSize: 30, color: ORANGE, bold: true, align: "center", valign: "middle", margin: 0
  });

  s.addShape(pres.shapes.RECTANGLE, {
    x: 6.7, y: 1.2, w: 2.8, h: 1.3, fill: { color: NAVY }, line: { color: NAVY }
  });
  s.addText("🔒\nJournal d'audit\n(lecture seule)", {
    x: 6.7, y: 1.2, w: 2.8, h: 1.3, fontSize: 14, fontFace: FONT_HEADER_BOLD,
    bold: true, color: WHITE, align: "center", valign: "middle", margin: 0
  });

  // Détails
  s.addText("📋 Informations enregistrées", {
    x: 0.4, y: 2.85, w: 9, h: 0.35, fontSize: 13, fontFace: FONT_HEADER_BOLD,
    bold: true, color: ORANGE, margin: 0
  });

  // Mini tableau des champs
  const auditFields = [
    [{ text: "Date / heure", options: { bold: true } }, "17/04/2026 14:32:08"],
    [{ text: "Utilisateur", options: { bold: true } }, "Abdoulaye DIENG"],
    [{ text: "Entité", options: { bold: true } }, "Bulletin"],
    [{ text: "Action", options: { bold: true } }, "Valider"],
    [{ text: "Objet", options: { bold: true } }, "DIOP Moussa — 04/2026"],
    [{ text: "Ancien statut", options: { bold: true } }, "Brouillon"],
    [{ text: "Nouveau statut", options: { bold: true } }, "Validé"],
    [{ text: "Détails", options: { bold: true } }, "Net à payer : 385 000 FCFA"],
  ];

  s.addTable(auditFields, {
    x: 0.4, y: 3.25, w: 9.2, h: 1.7,
    fontSize: 10, fontFace: FONT_BODY, color: TEXT,
    border: { pt: 0.5, color: "CCCCCC" },
    colW: [2.5, 6.7], rowH: 0.2
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 22 — CONFORMITÉ DROIT SOCIAL SN
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Conformité droit social Sénégal", "Code Travail + IPRES + CSS + DGID");

  // 4 cartes de conformité
  const compliance = [
    {
      x: 0.4, y: 1.2, color: NAVY, icon: "📜", title: "Code du Travail SN",
      bullets: [
        "Article L.120 — Livre paie 5 ans",
        "Audition contradictoire disciplinaire",
        "Mise à pied max 8 jours",
        "Préavis selon ancienneté",
        "Indemnités licenciement (paliers 25/30/40%)"
      ]
    },
    {
      x: 5.0, y: 1.2, color: ORANGE, icon: "🏛️", title: "Cotisations IPRES + CSS",
      bullets: [
        "IPRES RG : 5,60% / 8,40% (plafond 432K)",
        "IPRES RC cadres : 2,40% / 3,60% (plafond 1,296M)",
        "CSS AT (variable selon risque)",
        "CSS AF (variable)",
        "Bordereaux mensuels exportés Excel"
      ]
    },
    {
      x: 0.4, y: 3.25, color: NAVY_LIGHT, icon: "💼", title: "DGID — Fiscalité",
      bullets: [
        "IR progressif (avec abattement 30% / 900K)",
        "TRIMF mensuel + annuel",
        "CFCE (base brut fiscal)",
        "État VRS mensuel exportable",
        "Déclaration 1024 annuelle (DGID)"
      ]
    },
    {
      x: 5.0, y: 3.25, color: NAVY_DARK, icon: "🔐", title: "Sécurité & RGPD",
      bullets: [
        "Mot de passe SQL chiffré (DPAPI)",
        "HTTPS obligatoire (cookies secure)",
        "Lockout après 5 tentatives",
        "Conservation entretiens : durée contrat + 5 ans",
        "Logs audit immutables"
      ]
    }
  ];

  compliance.forEach(c => {
    s.addShape(pres.shapes.RECTANGLE, {
      x: c.x, y: c.y, w: 4.55, h: 1.95, fill: { color: WHITE }, line: { color: "DDDDDD" }, shadow: makeShadow()
    });
    s.addShape(pres.shapes.RECTANGLE, {
      x: c.x, y: c.y, w: 4.55, h: 0.45, fill: { color: c.color }, line: { color: c.color }
    });
    s.addText(c.icon, {
      x: c.x + 0.15, y: c.y + 0.05, w: 0.4, h: 0.4, fontSize: 16, valign: "middle", margin: 0
    });
    s.addText(c.title, {
      x: c.x + 0.6, y: c.y, w: 3.85, h: 0.45, fontSize: 12, fontFace: FONT_HEADER_BOLD,
      bold: true, color: WHITE, valign: "middle", margin: 0
    });
    s.addText(
      c.bullets.map((b, i) => ({
        text: b,
        options: { bullet: true, breakLine: i < c.bullets.length - 1 }
      })),
      {
        x: c.x + 0.2, y: c.y + 0.55, w: 4.15, h: 1.35, fontSize: 9, fontFace: FONT_BODY,
        color: TEXT, paraSpaceAfter: 2, margin: 0
      }
    );
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 23 — STATUT V1.0
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Statut livraison · V1.0", "Module Tableaux de Bord RH (mai 2026)");

  // Liste features V1.0
  s.addText("✅  V1.0 LIVRÉE — Module décisionnel", {
    x: 0.4, y: 1.15, w: 9, h: 0.4, fontSize: 16, fontFace: FONT_HEADER_BOLD,
    bold: true, color: GREEN, margin: 0
  });

  const v10Features = [
    { x: 0.4, y: 1.7, title: "6 Dashboards", items: ["Effectif détaillé", "Analyse Effectif (8 ans glissants)", "Mouvements (arrivées/départs)", "Rémunération (égalité H/F)", "Suivi Absences", "Bilan Social Mensuel"] },
    { x: 5.0, y: 1.7, title: "Infrastructure", items: ["RBAC 4 rôles (Admin / RH_Manager / RH / DAF)", "Cache mémoire 5 min (TTL configurable)", "Exports Excel (ClosedXML) + PDF (QuestPDF)", "Pages d'aide HTML par dashboard", "Charte ELTON Navy/Orange/Rouge"] }
  ];

  v10Features.forEach(f => {
    s.addShape(pres.shapes.RECTANGLE, {
      x: f.x, y: f.y, w: 4.55, h: 3.3, fill: { color: WHITE }, line: { color: "DDDDDD" }, shadow: makeShadow()
    });
    s.addShape(pres.shapes.RECTANGLE, {
      x: f.x, y: f.y, w: 0.08, h: 3.3, fill: { color: GREEN }, line: { color: GREEN }
    });
    s.addText(f.title, {
      x: f.x + 0.2, y: f.y + 0.15, w: 4.2, h: 0.4, fontSize: 14, fontFace: FONT_HEADER_BOLD,
      bold: true, color: NAVY, margin: 0
    });
    s.addText(
      f.items.map((it, i) => ({
        text: it,
        options: { bullet: true, breakLine: i < f.items.length - 1 }
      })),
      {
        x: f.x + 0.2, y: f.y + 0.6, w: 4.2, h: 2.6, fontSize: 11, fontFace: FONT_BODY,
        color: TEXT, paraSpaceAfter: 4, margin: 0
      }
    );
  });

  // Hash commit
  s.addText("Commit final V1.0 : 8680d001 · Tagué v1.0-dashboards-rh", {
    x: 0.4, y: 5.05, w: 9, h: 0.25, fontSize: 9, fontFace: "Consolas",
    italic: true, color: TEXT_SOFT, margin: 0
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 24 — STATUT V1.1
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Statut livraison · V1.1", "Refonte Intérim + sécurité + finitions UI (mai 2026)");

  s.addText("✅  V1.1 LIVRÉE — 6 Sprints en 1 mois", {
    x: 0.4, y: 1.15, w: 9, h: 0.4, fontSize: 16, fontFace: FONT_HEADER_BOLD,
    bold: true, color: GREEN, margin: 0
  });

  const sprints = [
    { sprint: "1A", title: "Modèle V1.1 enrichi", desc: "Site + TypeSite enum (StationService/Siege/Depot/Autre) · UniteOrganisationnelle récursive · ContratInterim multi-affectation N-N", commit: "4ec43373" },
    { sprint: "1B", title: "Seed démo + Wipe", desc: "6 sites + 19 unités + 25 intérimaires + ~40 contrats + 10 mouvements (préfixe DEMO_) · Bouton XAF wipe sécurisé", commit: "consolidé" },
    { sprint: "1C", title: "Refonte 4 services dashboards", desc: "Filtres en cascade Site → Unité · % Renouvellement plafonné 100% · Filtre Site sur contrats EXTERNE", commit: "515b2531" },
    { sprint: "1D", title: "Masquage entités legacy", desc: "StationService/BU [Deprecated] · FK [Legacy] sur ContratInterim/Mouvement · Pas de seed BUType", commit: "082da74" },
    { sprint: "1E", title: "Refonte SQL + docs", desc: "03_mouvements + 04_remuneration refondus Site V1.1 · README V1.1 · MISSION_STATE clôturée", commit: "e71bd8f" },
    { sprint: "Help", title: "Refonte 19 pages d'aide", desc: "Format step-by-step · Cas d'usage ELTON concrets · FAQ · CSS partagé · Nav 2-tier", commit: "e2806c2" }
  ];

  let y = 1.65;
  sprints.forEach(sp => {
    s.addShape(pres.shapes.RECTANGLE, {
      x: 0.4, y, w: 9.2, h: 0.55, fill: { color: WHITE }, line: { color: "DDDDDD" }
    });
    s.addShape(pres.shapes.RECTANGLE, {
      x: 0.4, y, w: 0.8, h: 0.55, fill: { color: NAVY }, line: { color: NAVY }
    });
    s.addText(sp.sprint, {
      x: 0.4, y, w: 0.8, h: 0.55, fontSize: 14, fontFace: FONT_HEADER_BOLD,
      bold: true, color: ORANGE, align: "center", valign: "middle", margin: 0
    });
    s.addText(sp.title, {
      x: 1.3, y: y + 0.05, w: 2.5, h: 0.45, fontSize: 11, fontFace: FONT_HEADER_BOLD,
      bold: true, color: NAVY, valign: "middle", margin: 0
    });
    s.addText(sp.desc, {
      x: 3.9, y: y + 0.05, w: 4.7, h: 0.45, fontSize: 9, fontFace: FONT_BODY,
      color: TEXT_SOFT, valign: "middle", margin: 0
    });
    s.addText(sp.commit, {
      x: 8.7, y: y + 0.05, w: 0.85, h: 0.45, fontSize: 8, fontFace: "Consolas",
      color: ORANGE, italic: true, align: "right", valign: "middle", margin: 0
    });
    y += 0.6;
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 25 — BÉNÉFICES CHIFFRÉS
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: NAVY_DARK };

  s.addText("Bénéfices attendus", {
    x: 0.5, y: 0.4, w: 9, h: 0.6, fontSize: 28, fontFace: FONT_HEADER_BOLD,
    bold: true, color: WHITE, margin: 0
  });
  s.addShape(pres.shapes.RECTANGLE, {
    x: 0.5, y: 0.95, w: 1.5, h: 0.05, fill: { color: ORANGE }, line: { color: ORANGE }
  });
  s.addText("Estimations fondées sur les pratiques sectorielles", {
    x: 0.5, y: 1.05, w: 9, h: 0.3, fontSize: 11, fontFace: FONT_BODY,
    italic: true, color: TEXT_LIGHT, margin: 0
  });

  // Grille 2x2
  const benefits = [
    { x: 0.5, y: 1.6, num: "−70%", label: "Temps de paie mensuelle", desc: "De 4 jours à 1 jour pour 200 bulletins · Génération en masse · Export auto" },
    { x: 5.05, y: 1.6, num: "100%", label: "Conformité légale", desc: "Code Travail SN · Livre paie L.120 · Cotisations à jour · Audit DTSS prêt" },
    { x: 0.5, y: 3.4, num: "+95%", label: "Traçabilité", desc: "Toutes actions tracées · Qui/quand/quoi · Lecture seule audit · Reporting préparé" },
    { x: 5.05, y: 3.4, num: "x10", label: "Capacité de pilotage RH", desc: "6 dashboards en temps réel · Exports prêts COMEX · Alertes seuils" }
  ];

  benefits.forEach(b => {
    s.addShape(pres.shapes.RECTANGLE, {
      x: b.x, y: b.y, w: 4.45, h: 1.65, fill: { color: NAVY_LIGHT }, line: { color: ORANGE, width: 2 }
    });
    s.addText(b.num, {
      x: b.x + 0.2, y: b.y + 0.15, w: 1.7, h: 1.4, fontSize: 48, fontFace: FONT_HEADER_BOLD,
      bold: true, color: ORANGE, align: "center", valign: "middle", margin: 0
    });
    s.addText(b.label, {
      x: b.x + 1.95, y: b.y + 0.2, w: 2.4, h: 0.45, fontSize: 14, fontFace: FONT_HEADER_BOLD,
      bold: true, color: WHITE, valign: "middle", margin: 0
    });
    s.addText(b.desc, {
      x: b.x + 1.95, y: b.y + 0.7, w: 2.4, h: 0.9, fontSize: 10, fontFace: FONT_BODY,
      color: TEXT_LIGHT, valign: "top", margin: 0
    });
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 26 — PLAN DE MISE EN PRODUCTION
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Plan de mise en production", "Stratégie de bascule en 4 phases");

  const phases = [
    { num: "1", color: NAVY, title: "Préparation", duration: "Semaine 1", items: ["Migration dbconfig %ProgramData%", "Création comptes utilisateurs (RBAC)", "Import salariés via Excel", "Formation utilisateurs clés (4h × 2 sessions)"] },
    { num: "2", color: ORANGE, title: "Pilote", duration: "Semaine 2-3", items: ["Bascule équipe RH (5 personnes)", "Génération bulletins parallèle Excel + SunuPaie", "Comparaison résultats", "Ajustements paramètres rubriques / barèmes"] },
    { num: "3", color: NAVY_LIGHT, title: "Déploiement", duration: "Semaine 4", items: ["Bascule 100% équipe RH + DAF", "Communication tous salariés (espace salarié)", "Activation workflows congés/missions", "Désactivation seed démo (DEMO_*)"] },
    { num: "4", color: GREEN, title: "Stabilisation", duration: "Mois 2-3", items: ["Suivi quotidien KPI dashboards", "Hotfixes mineurs si besoin", "Onboarding nouveaux utilisateurs", "Bilan post-bascule + ROI mesuré"] }
  ];

  let y = 1.15;
  phases.forEach(ph => {
    s.addShape(pres.shapes.RECTANGLE, {
      x: 0.4, y, w: 9.2, h: 0.92, fill: { color: WHITE }, line: { color: "DDDDDD" }
    });
    s.addShape(pres.shapes.RECTANGLE, {
      x: 0.4, y, w: 0.8, h: 0.92, fill: { color: ph.color }, line: { color: ph.color }
    });
    s.addText(ph.num, {
      x: 0.4, y, w: 0.8, h: 0.92, fontSize: 28, fontFace: FONT_HEADER_BOLD,
      bold: true, color: WHITE, align: "center", valign: "middle", margin: 0
    });
    s.addText(ph.title, {
      x: 1.35, y: y + 0.05, w: 2, h: 0.4, fontSize: 14, fontFace: FONT_HEADER_BOLD,
      bold: true, color: NAVY, valign: "middle", margin: 0
    });
    s.addText(ph.duration, {
      x: 1.35, y: y + 0.45, w: 2, h: 0.4, fontSize: 10, fontFace: FONT_BODY,
      color: ORANGE, italic: true, valign: "middle", margin: 0
    });
    s.addText(ph.items.join(" · "), {
      x: 3.5, y: y + 0.05, w: 6.0, h: 0.85, fontSize: 9, fontFace: FONT_BODY,
      color: TEXT_SOFT, valign: "middle", margin: 0
    });
    y += 0.97;
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 27 — RISQUES & MITIGATIONS
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Risques & mitigations", "Anticipation des points critiques");

  const risks = [
    { level: "🔴", levelLabel: "FAIBLE", risk: "Résistance au changement (équipe RH habituée Excel)", mitigation: "Formation 8h + accompagnement 2 sem · Pages d'aide step-by-step · Pilote sur volontaires" },
    { level: "🟡", levelLabel: "MOYEN", risk: "Erreur de calcul cotisations", mitigation: "Validation parallèle 1 mois (Excel vs SunuPaie) · Comparaison montant à montant · 245 bulletins testés en démo" },
    { level: "🟡", levelLabel: "MOYEN", risk: "Indisponibilité serveur", mitigation: "Sauvegardes BDD quotidiennes · Plan de bascule manuel documenté · SLA serveur ELTON" },
    { level: "🟢", levelLabel: "MAÎTRISÉ", risk: "Faille sécurité / fuite données", mitigation: "RBAC granulaire · Mot de passe SQL chiffré DPAPI · Audit complet · HTTPS · Lockout 5 essais" },
    { level: "🟢", levelLabel: "MAÎTRISÉ", risk: "Évolution réglementaire (cotisations, IR)", mitigation: "Barèmes IR/TRIMF dans paramètres · Mise à jour sans redéploiement · Versioning historique" },
    { level: "🟢", levelLabel: "MAÎTRISÉ", risk: "Volume données (croissance)", mitigation: "Cache mémoire 5 min · Export CSV bulk · Optionnel : passage Redis si > 100 users" }
  ];

  let y = 1.2;
  risks.forEach(r => {
    s.addShape(pres.shapes.RECTANGLE, {
      x: 0.4, y, w: 9.2, h: 0.6, fill: { color: WHITE }, line: { color: "DDDDDD" }
    });
    s.addText(r.level, {
      x: 0.4, y: y + 0.05, w: 0.5, h: 0.5, fontSize: 20, align: "center", valign: "middle", margin: 0
    });
    s.addText(r.levelLabel, {
      x: 0.85, y: y + 0.05, w: 1.0, h: 0.5, fontSize: 9, fontFace: FONT_HEADER_BOLD,
      bold: true, color: NAVY, align: "center", valign: "middle", margin: 0
    });
    s.addText(r.risk, {
      x: 1.95, y: y + 0.05, w: 3.5, h: 0.5, fontSize: 10, fontFace: FONT_BODY,
      color: TEXT, valign: "middle", margin: 0
    });
    s.addText(r.mitigation, {
      x: 5.55, y: y + 0.05, w: 4.0, h: 0.5, fontSize: 9, fontFace: FONT_BODY,
      color: TEXT_SOFT, italic: true, valign: "middle", margin: 0
    });
    y += 0.65;
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 28 — ROADMAP PHASE 2
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Roadmap Phase 2", "Évolutions futures (post mise en production)");

  // Timeline horizontale
  const items = [
    { x: 0.5, color: ORANGE, title: "Q3 2026", subtitle: "Stabilisation", items: ["Hotfixes V1.1", "Optimisations performance", "Onboarding élargi"] },
    { x: 3.0, color: NAVY, title: "Q4 2026", subtitle: "Mobile & UX", items: ["Application mobile salariés", "Bulletins push notif", "Dashboard mobile DG"] },
    { x: 5.5, color: NAVY_LIGHT, title: "Q1 2027", subtitle: "Décisionnel++", items: ["Power BI embarqué", "Heatmap absences", "Export .pbix prêt"] },
    { x: 8.0, color: GREEN, title: "Q2 2027", subtitle: "Étendre", items: ["Module Recrutement", "Module Carrière", "API ouverte / intégrations"] }
  ];

  items.forEach(it => {
    s.addShape(pres.shapes.RECTANGLE, {
      x: it.x, y: 1.2, w: 1.9, h: 3.6, fill: { color: WHITE }, line: { color: "DDDDDD" }, shadow: makeShadow()
    });
    s.addShape(pres.shapes.RECTANGLE, {
      x: it.x, y: 1.2, w: 1.9, h: 0.9, fill: { color: it.color }, line: { color: it.color }
    });
    s.addText(it.title, {
      x: it.x, y: 1.25, w: 1.9, h: 0.35, fontSize: 15, fontFace: FONT_HEADER_BOLD,
      bold: true, color: WHITE, align: "center", valign: "middle", margin: 0
    });
    s.addText(it.subtitle, {
      x: it.x, y: 1.62, w: 1.9, h: 0.35, fontSize: 11, fontFace: FONT_BODY,
      italic: true, color: WHITE, align: "center", valign: "middle", margin: 0
    });
    s.addText(
      it.items.map((i, idx) => ({
        text: i,
        options: { bullet: true, breakLine: idx < it.items.length - 1 }
      })),
      {
        x: it.x + 0.15, y: 2.25, w: 1.7, h: 2.4, fontSize: 9, fontFace: FONT_BODY,
        color: TEXT, paraSpaceAfter: 6, margin: 0
      }
    );
  });

  // Note bas
  s.addText("📌 Roadmap indicative · à valider en CODIR + DSI · budgets Q3 à provisionner", {
    x: 0.4, y: 4.95, w: 9.2, h: 0.3, fontSize: 10, fontFace: FONT_BODY,
    italic: true, color: TEXT_SOFT, align: "center", margin: 0
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 29 — DEMANDE DE VALIDATION
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: NAVY_DARK };

  s.addText("Demande de validation CODIR", {
    x: 0.5, y: 0.5, w: 9, h: 0.7, fontSize: 32, fontFace: FONT_HEADER_BOLD,
    bold: true, color: WHITE, align: "center", margin: 0
  });
  s.addShape(pres.shapes.RECTANGLE, {
    x: 4, y: 1.2, w: 2, h: 0.06, fill: { color: ORANGE }, line: { color: ORANGE }
  });

  // Décisions à acter
  s.addText("Décisions attendues", {
    x: 0.5, y: 1.5, w: 9, h: 0.4, fontSize: 18, fontFace: FONT_HEADER_BOLD,
    bold: true, color: ORANGE, align: "center", margin: 0
  });

  const decisions = [
    { num: "1", text: "Validation mise en production V1.0 + V1.1" },
    { num: "2", text: "Validation plan de bascule (4 semaines)" },
    { num: "3", text: "Validation budget formation utilisateurs (8h × 2 sessions)" },
    { num: "4", text: "Validation roadmap Phase 2 indicative (Q3 2026 → Q2 2027)" }
  ];

  let y = 2.15;
  decisions.forEach(d => {
    s.addShape(pres.shapes.OVAL, {
      x: 1.0, y: y + 0.05, w: 0.6, h: 0.6, fill: { color: ORANGE }, line: { color: ORANGE }
    });
    s.addText(d.num, {
      x: 1.0, y: y + 0.05, w: 0.6, h: 0.6, fontSize: 22, fontFace: FONT_HEADER_BOLD,
      bold: true, color: WHITE, align: "center", valign: "middle", margin: 0
    });
    s.addText(d.text, {
      x: 1.85, y: y + 0.1, w: 7.5, h: 0.5, fontSize: 16, fontFace: FONT_BODY,
      color: WHITE, valign: "middle", margin: 0
    });
    y += 0.75;
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 30 — DÉMO LIVE / SCREENS
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: CREAM };
  addTitleBar(s, "Démonstration en direct", "20-30 minutes sur l'environnement Recette");

  const demoScript = [
    { time: "0-3 min", title: "Connexion & accueil", desc: "Login utilisateur RH · Tableau de bord d'accueil · Navigation principale" },
    { time: "3-8 min", title: "Cycle paie complet", desc: "Ouvrir période · Générer bulletins en masse · Recalculer · Valider · Exporter PDF/Excel · Bordereau IPRES" },
    { time: "8-13 min", title: "Workflow congés", desc: "Salarié saisit demande · N+1 valide · RH accorde · Calendrier mis à jour · Solde décrémenté" },
    { time: "13-18 min", title: "Module Intérimaires V1.1", desc: "Créer fiche · Demande recrutement · Contrat avec multi-affectation Site/Unités · Mouvement entre sites" },
    { time: "18-25 min", title: "6 Dashboards RH", desc: "Tour des 6 dashboards · Filtres · Export Excel synthèse · Bilan Social Mensuel" },
    { time: "25-30 min", title: "Sécurité & Audit", desc: "Connexion en RH_Manager (lecture seule) · Journal d'audit · Validation traçabilité" }
  ];

  let y = 1.15;
  demoScript.forEach(d => {
    s.addShape(pres.shapes.RECTANGLE, {
      x: 0.4, y, w: 9.2, h: 0.6, fill: { color: WHITE }, line: { color: "DDDDDD" }
    });
    s.addShape(pres.shapes.RECTANGLE, {
      x: 0.4, y, w: 1.3, h: 0.6, fill: { color: ORANGE }, line: { color: ORANGE }
    });
    s.addText(d.time, {
      x: 0.4, y, w: 1.3, h: 0.6, fontSize: 11, fontFace: FONT_HEADER_BOLD,
      bold: true, color: WHITE, align: "center", valign: "middle", margin: 0
    });
    s.addText(d.title, {
      x: 1.85, y: y + 0.05, w: 2.5, h: 0.5, fontSize: 12, fontFace: FONT_HEADER_BOLD,
      bold: true, color: NAVY, valign: "middle", margin: 0
    });
    s.addText(d.desc, {
      x: 4.4, y: y + 0.05, w: 5.1, h: 0.5, fontSize: 9, fontFace: FONT_BODY,
      color: TEXT_SOFT, valign: "middle", margin: 0
    });
    y += 0.65;
  });

  addFooter(s, pageNum, TOTAL_SLIDES);
}

// ============================================================================
// SLIDE 31 — Q&A FINAL
// ============================================================================
{
  pageNum++;
  const s = pres.addSlide();
  s.background = { color: NAVY_DARK };

  // Bande orange à droite
  s.addShape(pres.shapes.RECTANGLE, {
    x: 9.6, y: 0, w: 0.4, h: 5.625, fill: { color: ORANGE }, line: { color: ORANGE }
  });

  s.addText("Q&A", {
    x: 0.5, y: 1.5, w: 9, h: 1.5, fontSize: 96, fontFace: FONT_HEADER_BOLD,
    bold: true, color: WHITE, align: "center", margin: 0
  });
  s.addText("Questions · Échanges · Décision", {
    x: 0.5, y: 2.9, w: 9, h: 0.5, fontSize: 22, fontFace: FONT_BODY,
    italic: true, color: ORANGE, align: "center", margin: 0  });

  s.addShape(pres.shapes.RECTANGLE, {
    x: 4, y: 3.7, w: 2, h: 0.06, fill: { color: ORANGE }, line: { color: ORANGE }
  });

  s.addText("Merci pour votre attention", {
    x: 0.5, y: 4.0, w: 9, h: 0.4, fontSize: 16, fontFace: FONT_BODY,
    color: TEXT_LIGHT, align: "center", italic: true, margin: 0
  });

  s.addText([
    { text: "Abdoulaye DIENG", options: { bold: true, color: WHITE } },
    { text: "  ·  ", options: { color: ORANGE } },
    { text: "Direction Système d'Information", options: { color: TEXT_LIGHT } },
    { text: "  ·  ", options: { color: ORANGE } },
    { text: "ELTON Oil Company", options: { color: TEXT_LIGHT } }
  ], {
    x: 0.5, y: 4.6, w: 9, h: 0.4, fontSize: 13, fontFace: FONT_BODY,
    align: "center", margin: 0
  });
}

const outputPath = "/sessions/pensive-modest-edison/mnt/AdiPAIE_V02/SunuPaie_CODIR_Mai2026.pptx";
pres.writeFile({ fileName: outputPath })
  .then(() => {
    console.log("OK Presentation generee : " + outputPath);
    console.log("Total : " + TOTAL_SLIDES + " slides");
  })
  .catch(err => {
    console.error("ERREUR :", err);
    process.exit(1);
  });
