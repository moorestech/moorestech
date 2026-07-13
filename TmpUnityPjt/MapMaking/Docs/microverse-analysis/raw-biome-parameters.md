# MicroVerse 全バイオーム パラメータダンプ

uloop execute-dynamic-code で2026-03-19に抽出した生データ。

## 砂漠エリア
Position: (1412.95, 146.29, 808.38)

### TextureStamps (2)
- [Ground] layer=SandFine weight=1, noise: None
- [Ground] layer=Mud weight=0.099, noise: WormFBM freq=2.96 amp=7.92

### DetailStamps (1)
- [Plants] proto=Malcomia_grass, noise: Simple freq=12.87 amp=16.7

### TreeStamps (1)
- [Trees] density=7.72 poisson=1.647 seed=16, occlude=True/True, protos=8
  - Olivetree1-5, Olivebush1-3
  - rand: h=(0.8,1.2) w=(0.8,1.2), noise: None f=3.79 a=20.73

### ObjectStamps (2)
- [High Desert Cliffs] density=0.48 poisson=2 seed=298, protos=3
  - DesertHighCliff_0,1,2
  - rand: scaleX=(1,2) scaleY=(1,2) rotY=(-180,180) slopeAlign=0.04 sink=(-10,-12)
  - noise: WormFBM f=10 a=0.21
- [Dense Rubble] density=2.63 poisson=2 seed=225, protos=6
  - RubbleDense_1-3, RubbleSparse_1-3
  - rand: scaleX=(0.8,1.2) scaleY=(0.8,1.2) slopeAlign=1 sink=(0,0.2)
  - noise: None

---

## メサエリア
Position: (1368.21, 61.64, 1345.02)

### TextureStamps (4)
- [Ground] layer=SandLarge weight=1, noise: None (base layer)
- [Ground] layer=SandFine weight=1, slope=(2.3°,19.5°) smooth=(6,6), noise: None
- [Ground] layer=SandFine_gravel weight=0.548, noise: WormFBM freq=14.29 amp=7.92
- [Ground] layer=SandFine_rubble weight=0.299, noise: WormFBM freq=2.97 amp=15.51

### DetailStamps (20)
- [Plants] x8: proto=DryGrass2/3/4 (repeating pattern), noise: WormFBM freq=12.87 amp=16.7
- [Plants] x4: proto=DryGrass2/3/4, noise: None freq=12.87 amp=16.7
- [Plants] x4: proto=WildflowersYellow1-4, noise: Simple freq=12.87 amp=16.7
- [Plants] x4: proto=WildflowersYellow1-4, noise: WormFBM freq=25.84 amp=16.7

### TreeStamps (5)
- [Trees] density=1.7 protos=13 (Senita1-8, Saguaro1-5), noise: Simple f=25.84 a=8
- [Trees] density=12.3 protos=4 (Cacactus1-4), noise: None
- [Trees] density=6.8 protos=8 (Senita1-8), noise: None
- [Trees] density=3 protos=8 (Senita1-8), noise: Simple f=25.84 a=8
- [Trees] density=8 protos=4 (Opuntia1-4), noise: Simple f=25.84 a=8
- All: poisson=1.647, h=(0.8,1.5) w=(0.8,1.5)

### ObjectStamps (8)
- [Strate] density=0.8 protos=6 (Strate_0-5), slopeAlign=0.605, noise: WormFBM
- [Dense Rubble] density=5.79 poisson=0.21 protos=3 (RubbleDense), slopeAlign=1
- [Strate Mesa Sharp 01] density=3.41 poisson=0.17 protos=5, slopeAlign=0.605
- [Thin Mesa] density=0.8 protos=6 (ThinMesa_0-5), slopeAlign=0.325, noise: WormFBM
- [Big Mesa] density=4.41 protos=6 (BigMesa_0-5), slopeAlign=0.325, noise: WormFBM a=3.34
- [Strate Mesa Sharp 01] density=0.93 protos=5, slopeAlign=0.605, noise: WormFBM
- [Dense Rubble] density=7.39 poisson=0.21 protos=3 (RubbleSparse), slopeAlign=1
- [Boulders] density=1.51 protos=5 (Boulders_1-5), scaleX=(0.5,0.8) slopeAlign=1

---

## サバンナエリア
Position: (862.39, 46.12, 1535.80)

### TextureStamps (7)
- [Grass(1)] layer=GrassGreen weight=1, noise: None
- [Grass(2)] layer=GrassDark weight=1, noise: WormFBM freq=10
- [Grass(1)] layer=GrassDark weight=1, noise: None
- [Grass(2)] layer=GrassGreen weight=1, noise: WormFBM freq=10
- [Grass(1)] layer=GrassGreen weight=1, noise: None
- [Grass(2)] layer=GrassDark weight=1, noise: WormFBM freq=10
- [Grass(3)] layer=GrassGreenDIrt weight=1, noise: WormFBM freq=10

### DetailStamps (3)
- [Plants] x3: proto=SavannaGrass1/2/3_greener, noise: Simple freq=12.87 amp=5.95

### TreeStamps (2)
- [Tree Stamp] density=3.38 protos=8 (Acacia1-8), noise: FBM f=12.47 a=20.73
- [Trees] density=4.18 protos=3 (Bush1-3), h=(1,1) w=(1,1), noise: FBM f=26.13 a=20.73

### ObjectStamps (0)

---

## 岩石山エリア
Position: (904.00, 75.90, 743.00)

### TextureStamps (2)
- [Ground] layer=SandFine weight=1, noise: None
- [Ground] layer=Mud weight=0.099, noise: WormFBM freq=2.96 amp=7.92

### DetailStamps (1)
- [Plants] proto=Malcomia_grass, noise: None freq=12.87 amp=16.7

### TreeStamps (2)
- [Trees] density=3.74 protos=5 (Olivetree1-5), noise: FBM f=3.79 a=20.73
- [Trees] density=3.19 protos=3 (Olivebush1-3), h=(1,1), noise: None

### ObjectStamps (3)
- [Desert Cliffs] density=0.25 protos=5 (DesertCliff_0-4), scaleX=(4,6) slopeAlign=0.056 sink=(3,6)
- [Desert Cliffs] density=1.76 protos=5, scaleX=(1,2) slopeAlign=0, noise: WormFBM f=5.32 a=-5.54
- [Desert Rocks] density=6.68 protos=9 (DesertRock_1-9), scaleX=(0.8,1.2) slopeAlign=1

---

## 森林エリア
Position: (411.00, 2.29, 1432.00)

### TextureStamps (2)
- [Ground] layer=Grass weight=1, noise: None
- [Ground] layer=SoilPine weight=1, noise: WormFBM freq=10 amp=0.75

### DetailStamps (12)
- [Plant] x7: proto=ThinFern3/4/5 (dominant), noise: None/WormFBM freq=6.9 amp=4.83
- [Plant] x2: proto=Clovers1/2, noise: WormFBM freq=11.5 amp=43.07
- [Plant] x3: proto=ThinFern1/2/3 + RedFirBranches, noise: WormFBM freq=6.9 amp=4.83

### TreeStamps (2)
- [Trees] density=2.41 protos=5 (RedPine1-5), h[0]=(1.5,2) others=(0.8,1.2), noise: Worley f=4.72 a=2.11
- [Trees] density=2 protos=5 (RedPine1-5), h=(3,4) w=(3,4) (LARGE), noise: Worley f=4.72 a=2.11

### ObjectStamps (1)
- [Hollow Logs with Ferns] density=1.77 protos=6 (RedwoodHollowLog variants), slopeAlign=1

---

## 草原エリア
Position: (449.43, 78.00, 298.36)

### TextureStamps (1)
- [Ground] layer=Grass weight=1, noise: None

### DetailStamps (12)
- [Grass] x5: proto=Shurubu_1-5, noise: None freq=3.98 amp=2.62
- [Grass] x7: proto=Grass_0-4 + Sorrel, noise: None freq=10 amp=1

### TreeStamps (0)
(草原には木がない)

### ObjectStamps (1)
- [Rubble] density=3.02 protos=6 (RubbleDense/Sparse), slopeAlign=0, noise: WormFBM

---

## 林エリア
Position: (351.00, 71.22, 775.57)

### TextureStamps (3)
- [Grass(1)] layer=Grass01 weight=1, noise: None
- [Mud(1)] layer=Mud01 weight=1, slope=(0,12°) smooth=(10,10), noise: WormFBM freq=10 amp=1.51
- [Mud(2)] layer=Mud02 weight=1, slope=(0,17.4°) smooth=(0,0), noise: WormFBM freq=10 amp=1.1

### DetailStamps (6)
- [Branches(1)] proto=Branchs, noise: Worley freq=13.2 amp=-0.5
- [Grass(1)] proto=Grass4, noise: None freq=12.3 amp=24.31
- [Flower(1)] proto=Sorrel, noise: FBM freq=12.3 amp=24.31
- [Grass(1)] proto=Grass4, noise: WormFBM freq=12.3 amp=24.31
- [Grass(1)] proto=Grass1, noise: None freq=12.3 amp=24.31
- [Grass(2)] proto=Grass2, noise: None freq=12.3 amp=24.31

### TreeStamps (3)
- [Stones] density=1.56 poisson=1.918 protos=9 (Stone1-5 + b variants), noise: None
- [Tree Stamp] density=4.09 protos=6 (Spruce1-6), noise: FBM f=3.79 a=20.73
- [Tree Stamp] density=3.94 protos=6 (Fir1-6), noise: FBM f=3.79 a=20.73

### ObjectStamps (0)
