# Phase C1.1 — Inventario de familias de validación

Este inventario se genera de forma reproducible y de solo lectura. Los archivos con `[ADD …]` son salidas sintéticas y se excluyen de la validación humana. Los duplicados binarios se colapsan por SHA-256.

- Archivos `.osu`: 1212
- Archivos mania válidos: 1212
- Archivos inválidos/no mania: 0
- Salidas sintéticas excluidas: 1200
- Ubicaciones originales humanas: 12
- Ubicaciones duplicadas exactas: 1
- Charts humanos únicos: 11
- Familias: 11

Una familia se define por `Artist + Title + Creator` normalizados; la dificultad (`Version`) se conserva como chart separado. En este corpus cada familia tiene una sola dificultad única.

| Familia | Dificultad | K | Objetos | Taps | LN | Timing | BPM | Dup. paths | Candidates afectados | Copias | Ruta relativa |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| -45 \| MIDORIGO QUEEN BEE \| ITZBENJA616 | Sooth | 7 | 4897 | 4876 | 21 | 1 | 153–153 | 0.027397260274 | 2 | 1 | ManiaAddNotesLab-932f92eec34b42a0bbe1ef0b2ede4d5c/-45 - Midorigo Queen Bee (ItzBenja616) [Sooth].osu |
| A-ONE \| SIDE BY SIDE \| KURISU MAKISE | Lunatic | 10 | 4001 | 4001 | 0 | 1 | 165–165 | 0 | 0 | 1 | ManiaAddNotesLab-f5a5b697431649b9bf70ce8289507356/A-One - Side by Side (Kurisu Makise) [Lunatic].osu |
| AKATSUKI RECORDS \| MIZUIRO RAINDROP \| [GB]V1DO | Lunat1c (Cut ver.) | 4 | 2241 | 2241 | 0 | 1 | 176–176 | 0 | 0 | 1 | ManiaAddNotesLab-824fc52bd41e4a9186d385ad3f715838/Akatsuki Records - Mizuiro Raindrop ([GB]V1do) [Lunat1c (Cut ver.)].osu |
| BUTAOTOME \| HAKANAKI MONO NINGEN \| YUEAST 2018 | fake | 4 | 1933 | 363 | 1570 | 1 | 180–180 | 0.024561139155 | 1545 | 1 | ManiaAddNotesLab-08255ad9b6104a4bac32bcbc4a445ed5/BUTAOTOME - Hakanaki Mono Ningen (YuEast 2018) [fake].osu |
| KIKUO \| KARA KARA KARA NO KARA \| XNETT | Karanival! | 7 | 10142 | 6184 | 3958 | 18 | 133–170 | 0.020766496142 | 4040 | 1 | ManiaAddNotesLab-3dda05e77fb04c549bcf02b2d695642f/Kikuo - Kara Kara Kara no Kara (xNett) [Karanival!].osu |
| MORO \| HOLY BITCH \| ITZBENJA616 | Selfishness | 7 | 8284 | 8284 | 0 | 1 | 220–220 | 0 | 0 | 1 | ManiaAddNotesLab-c9792f6e6e054cca9f0a2e5a2d504b45/moro - Holy Bitch (ItzBenja616) [Selfishness].osu |
| NJK RECORD FEAT. 3L \| SPRING OF DREAMS \| ITZBENJA616 | KNH** - Lvl 81 (No SV) CUSTOM | 7 | 4608 | 1021 | 3587 | 1 | 153–153 | 0.015299389674 | 3363 | 1 | ManiaAddNotesLab-cb69a956fc164cf9a7c21fe1b49dbb13/NJK Record feat. 3L - Spring of Dreams (ItzBenja616) [KNH - Lvl 81 (No SV)] CUSTOM.osu |
| NMK&WATHUE \| CELESTIAL AXES \| WONKI | Insane | 7 | 3098 | 3029 | 69 | 1 | 218–218 | 0.065934065934 | 18 | 1 | ManiaAddNotesLab-e0a90265f66f4602a029ae018f756893/nmk&wathue - Celestial Axes (Wonki) [Insane].osu |
| NOWISEE \| KO INU \| RUKA | aphotic | 7 | 5400 | 2400 | 3000 | 1 | 176–176 | 0.018925833814 | 2987 | 1 | ManiaAddNotesLab-c81deb37e7374b3fbe1da9cbf9a63eec/nowisee - Ko Inu (ruka) [aphotic].osu |
| ORGT \| DESTINY \| BAIO | Insane | 7 | 1653 | 1357 | 296 | 1 | 182–182 | 0.035628310063 | 124 | 2 | ManiaAddNotesLab-64eca68cb6f6401a8fbd4034d5a921ea/orgT - DESTINY (Baio) [Insane].osu |
| SAMFREE FT. SF-A2 MIKI \| MIKIMIKI ROMANTIC NIGHT \| NANANANA | Miki Dance | 7 | 4579 | 4526 | 53 | 1 | 164–164 | 0.058620689655 | 34 | 1 | ManiaAddNotesLab-f5b43f7ecd4c4b3c85e2a5b98daa23ae/Samfree ft. SF-A2 Miki - MikiMiki Romantic Night (Nananana) [Miki Dance].osu |
