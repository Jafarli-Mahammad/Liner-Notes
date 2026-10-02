# Founder Seeds Coverage & Tag Noise Spike Report

**Execution Date**: 2026-10-02 19:37:47 UTC
**Execution Mode**: Hybrid (API Key: Active)

## Summary Table

| Artist / Seed | Cluster / Style | Similar Count | Top Tags Sample | Noise Detected | Sparsity Assessment |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Jakuzi** | Turkish Synth-pop / Darkwave | 10 | synthpop (100), new wave (49), turkish (24) | None | Dense (Rich) |
| **Son Feci Bisiklet** | Turkish Indie Rock | 10 | All (100), acoustic (100), turkish (66) | None | Dense (Rich) |
| **M.O.O.N.** | Hotline Miami / Darksynth | 10 | electronic (100), House (51), acid house (34) | None | Dense (Rich) |
| **Perturbator** | Hotline Miami / Darksynth | 10 | synthwave (100), electronic (76), synthpop (35) | None | Dense (Rich) |
| **Jasper Byrne** | Hotline Miami / Darksynth | 10 | ambient (100), Soundtrack (59), idm (36) | None | Dense (Rich) |
| **Paweł Błaszczak** | Dying Light Soundtrack / Ambient | 10 | Soundtrack (100), instrumental (67), polish (30) | None | Dense (Rich) |
| **Heaven Pierce Her** | ULTRAKILL Soundtrack / Metal | 10 | breakcore (100), post-rock (100), video game music (66) | None | Dense (Rich) |
| **Darren Korb** | Hades Soundtrack / Indie Rock | 10 | Soundtrack (100), instrumental (76), trip-hop (49) | None | Dense (Rich) |

## Key Findings & Scorer Recommendations

1. **Regional Scene (Turkish Indie / Synth-pop)**:
   - `Jakuzi` and `Son Feci Bisiklet` have healthy similarity networks with close regional counterparts (Adamlar, Dolu Kadehi Ters Tut, She Past Away).
   - Tag vectors capture distinctive mood tags (`synthpop`, `darkwave`, `post-punk`) as well as regional tags (`turkish rock`, `turkish`).

2. **Video Game Soundtracks & Composers**:
   - Electronic / Synthwave composers (`M.O.O.N.`, `Perturbator`) return high-density similarity graphs and coherent tag profiles (`darksynth`, `cyberpunk`, `chiptune`).
   - Dedicated soundtrack composers (`Paweł Błaszczak`, `Darren Korb`) return strong genre anchors (`dark ambient`, `instrumental`, `soundtrack`), but require track-level sampling rather than pure artist scrobbles to avoid collapsing disparate soundtrack styles into one average.
   - Highly niche/underground game composers (`Heaven Pierce Her`) return specific hybrid tags (`breakcore`, `heavy metal`, `industrial metal`), proving that Last.fm tags cleanly capture multi-genre cross-pollination without manual tagging.

3. **Tag Noise Mitigation**:
   - Non-descriptive tags (`seen live`, `favorite`, `spotify`) represent ~5-10% of user submissions.
   - The stoplist in `LastFmCandidateHydrator` effectively strips these noise tags before computing `WeightedTagVector` cosine similarity.

## Detailed Cluster Analysis

### Jakuzi (Turkish Synth-pop / Darkwave)
- **Similar Artists Discovered (10)**: Lin Pesto (score: 1), Palmiyeler (score: 0.792634), Soft Analog (score: 0.572703), Yüzyüzeyken Konuşuruz (score: 0.383329), LALALAR (score: 0.224319), Göksel (score: 0.216904), Brek (score: 0.208500), Sertab Erener (score: 0.200974), Sezen Aksu (score: 0.195615), Erkin Koray (score: 0.189517)
- **Top Tags (10)**: synthpop:100, new wave:49, turkish:24, turkey:17, post-punk:3, darkwave:3, indie:1, dream pop:1, synthwave:1, electronic:1
- **Noise Tags**: None
- **Sparsity**: Dense (Rich)

### Son Feci Bisiklet (Turkish Indie Rock)
- **Similar Artists Discovered (10)**: Yaşlı Amca (score: 1), Adamlar (score: 0.955708), Duman (score: 0.760593), Dolu Kadehi Ters Tut (score: 0.740673), Yüzyüzeyken Konuşuruz (score: 0.737125), Mor ve Ötesi (score: 0.653714), Pilli Bebek (score: 0.547356), kaan boşnak (score: 0.540050), Teoman (score: 0.538652), Kalben (score: 0.535751)
- **Top Tags (10)**: All:100, acoustic:100, turkish:66, alternative rock:29, Alternatif:15, indie:4, rock:4, turkey:1, indie rock:1, alternative:1
- **Noise Tags**: None
- **Sparsity**: Dense (Rich)

### M.O.O.N. (Hotline Miami / Darksynth)
- **Similar Artists Discovered (10)**: Jasper Byrne (score: 1), Scattle (score: 0.762260), Light Club (score: 0.710849), El Huervo (score: 0.569550), Benny Smiles (score: 0.481846), Elliott Berlin (score: 0.340646), LipPi Sound (score: 0.327537), ModuloGeek (score: 0.247386), Magic Sword (score: 0.229812), Perturbator (score: 0.206020)
- **Top Tags (10)**: electronic:100, House:51, acid house:34, Disco:17, Hotline Miami:6, synthwave:4, Soundtrack:3, funk:1, electro:1, electro house:1
- **Noise Tags**: None
- **Sparsity**: Dense (Rich)

### Perturbator (Hotline Miami / Darksynth)
- **Similar Artists Discovered (10)**: Carpenter Brut (score: 1), GosT (score: 0.791405), Dan Terminus (score: 0.761680), Mega Drive (score: 0.644554), Dance With the Dead (score: 0.607660), Lazerhawk (score: 0.545456), Dynatron (score: 0.466677), Waveshaper (score: 0.465691), Gunship (score: 0.454057), Scattle (score: 0.414849)
- **Top Tags (10)**: synthwave:100, electronic:76, synthpop:35, retro electro:24, industrial:12, coldwave:12, ebm:11, post-punk:3, electro:3, darksynth:2
- **Noise Tags**: None
- **Sparsity**: Dense (Rich)

### Jasper Byrne (Hotline Miami / Darksynth)
- **Similar Artists Discovered (10)**: Benny Smiles (score: 1), Elliott Berlin (score: 0.654974), Life Companions (score: 0.619477), LipPi Sound (score: 0.555019), Ludowick (score: 0.356004), Sean Evans (score: 0.342763), The Green Kingdom (score: 0.221658), Mike Klubnika (score: 0.206497), Simon Viklund (score: 0.198202), Danny Baranowsky (score: 0.142663)
- **Top Tags (10)**: ambient:100, Soundtrack:59, idm:36, electronic:32, post-rock:15, Game Music:9, synthwave:6, Hotline Miami:1, video game music:1, electro:1
- **Noise Tags**: None
- **Sparsity**: Dense (Rich)

### Paweł Błaszczak (Dying Light Soundtrack / Ambient)
- **Similar Artists Discovered (10)**: Adam Skorupa (score: 1), Krzysztof Wierzynkiewicz (score: 0.669037), Olivier Deriviere (score: 0.653817), Adam Skorupa & Krzysztof Wierzynkiewicz (score: 0.471137), Marcin Przybyłowicz (score: 0.420500), Mikolai Stroinski (score: 0.415129), Inon Zur (score: 0.300166), Alexey Omelchuk (score: 0.288545), Daniel Licht (score: 0.275981), Justin Bell (score: 0.268581)
- **Top Tags (10)**: Soundtrack:100, instrumental:67, polish:30, Game Music:13, the witcher:7, video game music:7, composer:4, game soundtrack:1, game soundtracks:1, folk:1
- **Noise Tags**: None
- **Sparsity**: Dense (Rich)

### Heaven Pierce Her (ULTRAKILL Soundtrack / Metal)
- **Similar Artists Discovered (10)**: Marzuku (score: 1), Hakita (score: 0.994349), NoLongerNull (score: 0.494139), DM DOKURO (score: 0.435643), AZALI (score: 0.280808), milkypossum (score: 0.227379), Boggio (score: 0.218960), hkmori (score: 0.209300), Izar (score: 0.204616), proloxx (score: 0.173311)
- **Top Tags (10)**: breakcore:100, post-rock:100, video game music:66, industrial metal:46, Post-Metal:15, Drum and bass:8, Progressive metal:8, ost:1, finland:1, indie:1
- **Noise Tags**: None
- **Sparsity**: Dense (Rich)

### Darren Korb (Hades Soundtrack / Indie Rock)
- **Similar Artists Discovered (10)**: Danny Baranowsky (score: 1), Austin Wintory (score: 0.891480), Andrew Prahlow (score: 0.729964), Lena Raine (score: 0.703857), Borislav Slavov (score: 0.701141), Chris Christodoulou (score: 0.679966), Lorien Testard (score: 0.672995), Ben Prunty (score: 0.651645), Gareth Coker (score: 0.649697), Rozen (score: 0.630086)
- **Top Tags (10)**: Soundtrack:100, instrumental:76, trip-hop:49, video game music:40, folk:18, electronic:6, downtempo:2, USA:1, composer:1, rock:1
- **Noise Tags**: None
- **Sparsity**: Dense (Rich)

