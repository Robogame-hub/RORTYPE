# VFX import report — 2026-09-13

Imported into `Assets/Plugins/VFX` from existing local Unity projects. Original projects were not modified. Demo scenes and demo-only folders were excluded.

## Copy verification

- SHA-256 verified every copied source file except the two documented integration edits.
- No GUID collisions with the pre-existing project or between the selected packs were found.
- Added the CFX legacy circular mesh and the external Rings prefab to `Cartoon FX Remaster/Dependencies`.
- Removed four SlimeSlayer gameplay-only PushingImpulseView components from copied Rings.
- Guarded the copied CameraShake UnityEditor import with UNITY_EDITOR.
- Unity import, shader compilation and visual playback were not run. Some source projects use Unity 6 while RORTYPE uses Unity 2022.3/URP 14.

## Source defects retained for review

The following references were already unresolved in the source projects. They were not silently replaced with different artwork. Effects that use these resources may render incompletely. This inventory is a copied asset library, not a completed gameplay VFX integration.

### `155f67fd8750f8e4e83c8efb1731f3a0`

- `Assets/Plugins/VFX/Epic Toon FX/Materials/Basics/circle_AB.mat`
- `Assets/Plugins/VFX/Epic Toon FX/Materials/Basics/circle_ADD.mat`
- `Assets/Plugins/VFX/Epic Toon FX/Materials/Basics/circle_nosoft_AB.mat`

### `1fb08705db49c61459c2beb1c436c5a2`

- `Assets/Plugins/VFX/Cartoon FX Remaster/CFXR Assets/Graphics/cfxr ww smoke glow ab.mat`

### `2e0e72b39b9d9a9499fc9606faf9e117`

- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Interactive/Cards/CardglowType01.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Interactive/Cards/CardglowType02.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Interactive/Cards/CardglowType03.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Interactive/Cards/CardglowType04.prefab`

### `46a627430ba38144498c4588ddbd226a`

- `Assets/Plugins/VFX/Epic Toon FX/Materials/Misc/Powerbox/PreColored/power_heart_pc.mat`

### `4bf10d94145fd6c4187c18de0af92e43`

- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Explosions/NovaExplosion/ExplosionNovaBlue.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Explosions/NovaExplosion/ExplosionNovaFire.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Explosions/NovaExplosion/ExplosionNovaGreen.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Explosions/NovaExplosion/ExplosionNovaPink.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Explosions/NovaSmallExplosion/ExplosionNovaSmallBlue.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Explosions/NovaSmallExplosion/ExplosionNovaSmallFire.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Explosions/NovaSmallExplosion/ExplosionNovaSmallGreen.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Explosions/NovaSmallExplosion/ExplosionNovaSmallPink.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Explosions/SoulExplosion/SoulExplosionCrimson.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Explosions/SoulExplosion/SoulExplosionGreen.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Explosions/SoulExplosion/SoulExplosionOrange.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Explosions/SoulExplosion/SoulExplosionPurple.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/Bullet/BulletMeshSmallBlue.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/Bullet/BulletMeshSmallFire.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/Bullet/BulletMeshSmallGreen.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/Bullet/BulletMeshSmallPink.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/Bullet/BulletSmallBlue.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/Bullet/BulletSmallFire.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/Bullet/BulletSmallGreen.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/Bullet/BulletSmallPink.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/FatBullet/BulletFatBlue.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/FatBullet/BulletFatFire.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/FatBullet/BulletFatGreen.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/FatBullet/BulletFatPink.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/FatBullet/BulletMeshFatBlue.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/FatBullet/BulletMeshFatFire.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/FatBullet/BulletMeshFatGreen.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/FatBullet/BulletMeshFatPink.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/NovaMissile/NovaMissileBlue.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/NovaMissile/NovaMissileFire.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/NovaMissile/NovaMissileGreen.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/NovaMissile/NovaMissilePink.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/NovaSmall/NovaMissileSmallBlue.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/NovaSmall/NovaMissileSmallFire.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/NovaSmall/NovaMissileSmallGreen.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/NovaSmall/NovaMissileSmallPink.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/Soul/SoulMissileGreen.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/Soul/SoulMissileOrange.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Missiles/Soul/SoulMissilePurple.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Muzzleflash/EnergyNovaMuzzle/EnergyNovaMuzzleBlue.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Muzzleflash/EnergyNovaMuzzle/EnergyNovaMuzzleFire.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Muzzleflash/EnergyNovaMuzzle/EnergyNovaMuzzleGreen.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Muzzleflash/EnergyNovaMuzzle/EnergyNovaMuzzlePink.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Muzzleflash/EnergyNovaSmallMuzzle/EnergyNovaMuzzleSmallBlue.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Muzzleflash/EnergyNovaSmallMuzzle/EnergyNovaMuzzleSmallFire.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Muzzleflash/EnergyNovaSmallMuzzle/EnergyNovaMuzzleSmallGreen.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Muzzleflash/EnergyNovaSmallMuzzle/EnergyNovaMuzzleSmallPink.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Muzzleflash/SoulMuzzle/SoulMuzzleCrimson.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Muzzleflash/SoulMuzzle/SoulMuzzleGreen.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Muzzleflash/SoulMuzzle/SoulMuzzleOrange.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Combat/Muzzleflash/SoulMuzzle/SoulMuzzlePurple.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Environment/Firework/FireworkBlue.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Environment/Firework/FireworkBlueCluster.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Environment/Firework/FireworkGreen.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Environment/Firework/FireworkGreenCluster.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Environment/Firework/FireworkPurple.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Environment/Firework/FireworkPurpleCluster.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Environment/Firework/FireworkRed.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Environment/Firework/FireworkRedCluster.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Environment/Firework/FireworkYellow.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Environment/Firework/FireworkYellowCluster.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Environment/Sparks/SparkExplosionBlue.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Environment/Sparks/SparkExplosionYellow.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Environment/Sparks/SparkLoopYellow.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Environment/Sparks/SparkRadialExplosionBlue.prefab`
- `Assets/Plugins/VFX/Epic Toon FX/Prefabs/Environment/Sparks/SparkRadialExplosionYellow.prefab`

### `4c11e14e6e46e6c488fcd4b15c69e7db`

- `Assets/Plugins/VFX/Epic Toon FX/Materials/Misc/Powerbox/PreColored/power_spikybomb_pc.mat`

### `68f27fc14e168f94ba29e2a20a73fc96`

- `Assets/Plugins/VFX/Unity Particle Pack/WaterEffects/Materials/StormCloudParticle.mat`

### `88b9906b4442aa2448a9379fa0d1b987`

- `Assets/Plugins/VFX/Unity Particle Pack/FireExplosionEffects/Prefabs/BigExplosionEffect.prefab`

### `92b6ff7c5fd1d854fa9c6920e16c829e`

- `Assets/Plugins/VFX/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR _BOING_.prefab`
- `Assets/Plugins/VFX/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR _BOOM_.prefab`
- `Assets/Plugins/VFX/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR _POW_.prefab`
- `Assets/Plugins/VFX/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR _SLASH_.prefab`
- `Assets/Plugins/VFX/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR2 _CURSED_.prefab`
- `Assets/Plugins/VFX/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR2 _WHAM_ 3.prefab`
- `Assets/Plugins/VFX/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR3 _WOW_.prefab`
- `Assets/Plugins/VFX/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR4 _FROZEN_.prefab`
- `Assets/Plugins/VFX/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR4 _POISONED_.prefab`

### `c934e911e8386904398e71d6d79bcb84`

- `Assets/Plugins/VFX/Cartoon FX Remaster/CFXR Assets/Graphics/cfxr cloud blur add.mat`

### `d0324500825284cceafb111efc1e2ec5`

- `Assets/Plugins/VFX/Fire Particle Systems/Resources/Materials/ParticleSystem_Additive.mat`
- `Assets/Plugins/VFX/Fire Particle Systems/Resources/Materials/ParticleSystem_Additive_Emission.mat`
- `Assets/Plugins/VFX/Fire Particle Systems/Resources/Materials/ParticleSystem_Additive_Emission_Fire.mat`
- `Assets/Plugins/VFX/Fire Particle Systems/Resources/Materials/ParticleSystem_Color.mat`
- `Assets/Plugins/VFX/Fire Particle Systems/Resources/Materials/ParticleSystem_Multiply.mat`

### `d2391aab6d5845c4a96581b34c6395a5`

- `Assets/Plugins/VFX/Epic Toon FX/Materials/Misc/Powerbox/PreColored/power_exclamation_pc.mat`

### `d4d6919451fe3e24388816386a6d15a4`

- `Assets/Plugins/VFX/VFX Toon Fire Asset/Material/GridMat.mat`

### `dd497160bad50c04e8482e4afdc83942`

- `Assets/Plugins/VFX/Epic Toon FX/Materials/Misc/Powerbox/PreColored/power_boxingglove_pc.mat`

### `e6604b51154db9a45897df856817cba3`

- `Assets/Plugins/VFX/Epic Toon FX/Materials/Misc/Powerbox/PreColored/power_bomb_pc.mat`

### `f343d204ab60b284597aa1742824864c`

- `Assets/Plugins/VFX/Unity Particle Pack/FireExplosionEffects/Materials/SmokeDarkParticle.mat`
- `Assets/Plugins/VFX/Unity Particle Pack/WeaponEffects/Materials/RocketTrailParticle.mat`
