# AdiPAIE\_V02



WatermarkTransparency : règle la transparence (opacité) de l’image d’arrière-plan.



Valeur 0 → image 100% opaque (très visible).



Valeur 100 → image 100% transparente (invisible).



Bon compromis pour un papier à en-tête : 10–25 (le contenu reste parfaitement lisible).



WatermarkPageRange : indique sur quelles pages afficher le fond.



Exemples utiles :



"1" → seulement la première page.



"1-" → toutes les pages à partir de la 1ʳᵉ (donc 1, 2, 3, …).



"2-3" → pages 2 à 3 uniquement.



"1,3,5-" → page 1, page 3, puis 5 et suivantes.



Tu peux combiner virgules et intervalles.



📝 Notes



Ces réglages s’appliquent au Watermark du rapport (donc à la prévisualisation, impression et export PDF).



Si tu ne sais pas quoi mettre : commence avec WatermarkTransparency = 15 et WatermarkPageRange = "1" (fond uniquement sur la 1ʳᵉ page).

