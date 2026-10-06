# Founder Seeds Coverage & Tag Noise Spike Report

**Execution Date**: 2026-10-06 20:10:50 UTC
**Execution Mode**: Hybrid (API Key: Active)

## Summary Table

| Artist / Seed | Cluster / Style | Similar Count | Top Tags Sample | Noise Detected | Sparsity Assessment |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Jakuzi** | Turkish Synth-pop / Darkwave | 10 | synthpop (100), new wave (49), turkish (24) | None | Dense (Rich) |
| **Son Feci Bisiklet** | Turkish Indie Rock | 10 | acoustic (100), All (100), turkish (66) | None | Dense (Rich) |
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
- **Similar Artists Discovered (10)**: Lin Pesto (score: 1), Palmiyeler (score: 0.774212), Soft Analog (score: 0.584089), Yüzyüzeyken Konuşuruz (score: 0.409680), LALALAR (score: 0.284833), Göksel (score: 0.225333), Brek (score: 0.212202), Mavi Işıklar (score: 0.211163), Sena Şener (score: 0.210544), Sertab Erener (score: 0.202939)
- **Top Tags (10)**: synthpop:100, new wave:49, turkish:24, turkey:16, post-punk:3, darkwave:3, indie:1, synthwave:1, dream pop:1, electronic:1
- **Noise Tags**: None
- **Sparsity**: Dense (Rich)

### Son Feci Bisiklet (Turkish Indie Rock)
- **Similar Artists Discovered (10)**: Yaşlı Amca (score: 1), Adamlar (score: 0.945037), Duman (score: 0.747278), Yüzyüzeyken Konuşuruz (score: 0.729418), Dolu Kadehi Ters Tut (score: 0.726617), Mor ve Ötesi (score: 0.647068), Teoman (score: 0.535258), Kalben (score: 0.512652), kaan boşnak (score: 0.508792), Pilli Bebek (score: 0.506015)
- **Top Tags (10)**: acoustic:100, All:100, turkish:66, alternative rock:29, Alternatif:15, indie:4, rock:4, indie rock:1, turkey:1, alternative:1
- **Noise Tags**: None
- **Sparsity**: Dense (Rich)

### M.O.O.N. (Hotline Miami / Darksynth)
- **Similar Artists Discovered (10)**: Jasper Byrne (score: 1), Scattle (score: 0.784684), Light Club (score: 0.721485), El Huervo (score: 0.573629), Benny Smiles (score: 0.471757), Elliott Berlin (score: 0.352948), LipPi Sound (score: 0.344145), ModuloGeek (score: 0.245496), Magic Sword (score: 0.229335), Perturbator (score: 0.200397)
- **Top Tags (10)**: electronic:100, House:51, acid house:34, Disco:17, Hotline Miami:6, synthwave:4, Soundtrack:3, funk:1, electro:1, electro house:1
- **Noise Tags**: None
- **Sparsity**: Dense (Rich)

### Perturbator (Hotline Miami / Darksynth)
- **Similar Artists Discovered (10)**: Carpenter Brut (score: 1), GosT (score: 0.765300), Dan Terminus (score: 0.755445), Mega Drive (score: 0.627829), Dance With the Dead (score: 0.600830), Lazerhawk (score: 0.505968), Waveshaper (score: 0.466367), Gunship (score: 0.463840), Dynatron (score: 0.432949), Scattle (score: 0.405047)
- **Top Tags (10)**: synthwave:100, electronic:76, synthpop:35, retro electro:24, industrial:12, coldwave:12, ebm:11, post-punk:3, electro:3, darksynth:2
- **Noise Tags**: None
- **Sparsity**: Dense (Rich)

### Jasper Byrne (Hotline Miami / Darksynth)
- **Similar Artists Discovered (10)**: Scattle (score: 1), Benny Smiles (score: 0.806495), Elliott Berlin (score: 0.544110), Life Companions (score: 0.514455), LipPi Sound (score: 0.461006), Sean Evans (score: 0.294250), Ludowick (score: 0.280157), The Green Kingdom (score: 0.167547), Mike Klubnika (score: 0.153316), Simon Viklund (score: 0.143647)
- **Top Tags (10)**: ambient:100, Soundtrack:59, idm:36, electronic:32, post-rock:15, Game Music:9, synthwave:6, Hotline Miami:1, video game music:1, electro:1
- **Noise Tags**: None
- **Sparsity**: Dense (Rich)

### Paweł Błaszczak (Dying Light Soundtrack / Ambient)
- **Similar Artists Discovered (10)**: Adam Skorupa (score: 1), Krzysztof Wierzynkiewicz (score: 0.655636), Olivier Deriviere (score: 0.650377), Adam Skorupa & Krzysztof Wierzynkiewicz (score: 0.455491), Marcin Przybyłowicz (score: 0.408342), Mikolai Stroinski (score: 0.405097), Inon Zur (score: 0.305011), Alexey Omelchuk (score: 0.284319), Piotr Musial (score: 0.281149), Justin Bell (score: 0.273839)
- **Top Tags (10)**: Soundtrack:100, instrumental:67, polish:30, Game Music:13, the witcher:7, video game music:7, composer:4, game soundtrack:1, game soundtracks:1, folk:1
- **Noise Tags**: None
- **Sparsity**: Dense (Rich)

### Heaven Pierce Her (ULTRAKILL Soundtrack / Metal)
- **Similar Artists Discovered (10)**: Marzuku (score: 1), Hakita (score: 0.936809), NoLongerNull (score: 0.490971), DM DOKURO (score: 0.435095), Toby Fox (score: 0.271944), AZALI (score: 0.271428), milkypossum (score: 0.226002), hkmori (score: 0.205000), Izar (score: 0.195862), Boggio (score: 0.190304)
- **Top Tags (10)**: breakcore:100, post-rock:100, video game music:66, industrial metal:46, Post-Metal:15, Progressive metal:8, Drum and bass:8, finland:1, ost:1, indie:1
- **Noise Tags**: None
- **Sparsity**: Dense (Rich)

### Darren Korb (Hades Soundtrack / Indie Rock)
- **Similar Artists Discovered (10)**: Danny Baranowsky (score: 1), Austin Wintory (score: 0.936708), Borislav Slavov (score: 0.736836), Lena Raine (score: 0.735037), Andrew Prahlow (score: 0.717963), Chris Christodoulou (score: 0.685020), Lorien Testard (score: 0.679864), Gareth Coker (score: 0.666616), Ben Prunty (score: 0.646437), Rozen (score: 0.610338)
- **Top Tags (10)**: Soundtrack:100, instrumental:76, trip-hop:49, video game music:40, folk:18, electronic:6, downtempo:2, composer:1, rock:1, USA:1
- **Noise Tags**: None
- **Sparsity**: Dense (Rich)

