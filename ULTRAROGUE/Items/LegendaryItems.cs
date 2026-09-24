using HarmonyLib;
using Steamworks;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ULTRAKILL.Enemy;
using ULTRAKILL.Portal;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Ultrarogue.Items
{

    public class LuckyLeaf : BaseItem
    {
        public override string ItemName => "Lucky Leaf";
        public override string itemDescription => "Luck based items are more likely to trigger";
        public override List<ItemTag> itemTags => new List<ItemTag>() { ItemTag.Utility };
        public override Rarity Rarity => Rarity.Legendary;
        public override void OnGotten(int count, bool firstPickup)
        {
            Plugin.luck = count;
        }

        public override void OnRemoval()
        {
            Plugin.luck = 0;
        }
    }

    public class Dice : ActiveItem
    {
        public override string ItemName => "Dice";
        public override string itemDescription => "Reroll items";
        public override Rarity Rarity => Rarity.Legendary;
        public override int ChargeRequired => 5;
        public override void OnUse()
        {
            Room currentRoom = Room.getObjectInsideRoom(NewMovement.Instance.transform.position);
            ItemPickup[] pickups = currentRoom.GetComponentsInChildren<ItemPickup>();
            foreach (var item in pickups)
            {
                DroptableType drop = DroptableType.CommonOnly;

                switch (item.item.Rarity)
                {
                    case Rarity.Legendary:
                        drop = DroptableType.LegendaryOnly;
                        break;
                    case Rarity.Uncommon:
                        drop = DroptableType.UncommonOnly;
                        break;
                    case Rarity.Common:
                        drop = DroptableType.CommonOnly;
                        break;
                    case Rarity.Alchemy:
                        drop = DroptableType.Planetarium;
                        break;
                    case Rarity.NullItem:
                        drop = DroptableType.Planetarium;
                        break;
                }

                BaseItem randomItem = Plugin.GiveRandomItem(RogueDifficultyManager.ItemRNG, drop);
                item.SwitchItem(randomItem, RemoveCondition: false, delay: 1);
            }

        }
    }

    public class HolyLight : BaseItem
    {
        const float damage = 0.35f;
        public override string ItemName => "Holy Light";
        public override string itemDescription => $"Projectiles have a light that damages enemies within by {damage * 100}% (+{damage * 100}% per stack) per 0.25 seconds.";
        public override Rarity Rarity => Rarity.Legendary;
        public override bool CanSpawn()
        {
            return Plugin.weapons.Any((x) => x.weapon == Plugin.Weapon.Shotgun && !x.Alternate);
        }
        public override List<ItemTag> itemTags => new List<ItemTag>() { ItemTag.Damage };
        public override void OnStart()
        {
            new ProjectileStartEffect(ItemName, (obj, type) =>
            {
                switch (type)
                {
                    case ProjectileType.Projectile:
                        if (!obj.TryGetComponent<Projectile>(out var proj))
                            return;
                        if (!proj.playerBullet) return;
                        break;
                    case ProjectileType.Nail:
                        if (!obj.TryGetComponent<Nail>(out var nai))
                            return;
                        if (nai.enemy) return;
                        break;
                    case ProjectileType.Rocket:
                        if (!obj.TryGetComponent<Grenade>(out var greg))
                            return;
                        if (greg.enemy) return;
                        break;
                    case ProjectileType.Hitscan:
                        return;
                }

                GameObject lig = Object.Instantiate(GetLight());
                lig.transform.parent = obj.transform;
                lig.transform.localPosition = Vector3.zero;
                obj.AddComponent<TheLightWeLiveIn>().light = lig;
            });
        }

        GameObject _lightPrefab;

        public GameObject GetLight()
        {
            if (_lightPrefab == null)
            {
                _lightPrefab = Addressables.LoadAssetAsync<GameObject>($"{AssetsManager.RoguePath}LivingInTheLight.prefab").WaitForCompletion();

                if (_lightPrefab == null)
                    _lightPrefab = new GameObject("noprefab:(");
            }

            return _lightPrefab;
        }

        public class TheLightWeLiveIn : MonoBehaviour
        {
            float dmg = 0;
            float t = 0.25f;

            public GameObject light;

            Nail n;
            void Awake()
            {
                dmg = damage * Plugin.GetItemCount("Holy Light");
                n = GetComponent<Nail>();
            }

            void Update()
            {
                if(n != null)
                {
                    if (n.currentHitEnemy != null) return;
                    if (n.hit)
                    {
                        Destroy(light);
                        Destroy(this);
                    }
                }
                t += Time.deltaTime;

                if (t >= 0.075f)
                {
                    t = 0;
                    List<EnemyIdentifier> eids = EnemyTracker.Instance.GetCurrentEnemies();
                    if (eids.Count <= 0) return;
                    eids = eids.Where((x) => Vector3.Distance(x.transform.position, transform.position) <= 5).ToList();

                    foreach (var eid in eids)
                    {
                        eid.hitter = "light";
                        eid.DeliverDamage(eid.gameObject, Vector3.zero, eid.transform.position, dmg, false);
                    }
                }
            }
        }
    }

    [HarmonyPatch]
    public class ToolbarsFavorite : BaseItem
    {
        const float BounceMultiplier = 2f;
        const float BaseSpacing = 20f;
        const float SpacingDecayPerStack = 0.85f;
        const float MinSpacing = 2f;
        const int MaxZaps = 25;

        public override string ItemName => "Thunder Boomerang";
        public override string itemDescription => $"Double the hitscan bounce count. Every {BaseSpacing} (-{(1 - SpacingDecayPerStack) * 100}% per stack) units a hitscan travels, it zaps nearby enemies.";
        public override List<ItemTag> itemTags => new List<ItemTag>() { ItemTag.Damage };
        public override Rarity Rarity => Rarity.Legendary;

        internal static readonly HashSet<RevolverBeam> taggedBeams = new HashSet<RevolverBeam>();
        public override List<Plugin.Weapon> WeaponRequirements => new List<Plugin.Weapon>() { Plugin.Weapon.Revolver };
        public override float SpawnWeight => 0.8f; // Slightly lower spawn weight
        private static void SpawnExplosion(Vector3 point)
        {
            EnemyIdentifier.Zap(point, 0.5f);
            //GameObject go = Object.Instantiate(MonoSingleton<DefaultReferenceManager>.Instance.explosion, point, Quaternion.identity);
            //foreach (Explosion exp in go.GetComponentsInChildren<Explosion>())
            //    exp.canHit = AffectedSubjects.EnemiesOnly;
        }

        [HarmonyPatch(typeof(RevolverBeam), "Shoot")]
        class ShootPatch
        {
            static void Postfix(RevolverBeam __instance)
            {
                if (!Plugin.isInRogueScene())
                    return;

                if (Plugin.GetItemCount("Thunder Boomerang") == 0)
                    return;
                if (__instance.beamType == BeamType.Enemy) return;
                if (__instance.beamType == BeamType.MaliciousFace) return;
                LineRenderer lr = __instance.GetComponent<LineRenderer>();

                Vector3 start = lr.GetPosition(0);
                Vector3 end = lr.GetPosition(1);

                float distance = Vector3.Distance(start, end);
                Vector3 direction = (end - start).normalized;

                int stacks = Plugin.GetItemCount("Thunder Boomerang");

                // Clamp spacing to something sane — don't let it collapse toward zero
                float zapSpacing = BaseSpacing * Mathf.Pow(SpacingDecayPerStack, stacks - 1);
                if(__instance.hitterOverride == "Hitscan on hit")
                {
                    zapSpacing *= 2;
                }
                zapSpacing = Mathf.Max(zapSpacing, MinSpacing); // was 0.0001f

                // Hard cap on total zap points regardless of distance/stacks
                int zapCount = Mathf.Min(MaxZaps, Mathf.FloorToInt(distance / zapSpacing));

                for (int n = 1; n <= zapCount; n++)
                {
                    float i = n * zapSpacing;
                    if (i >= distance) break;
                    SpawnExplosion(start + direction * i);
                }
            }
        }
        [HarmonyPatch(typeof(RevolverBeam), nameof(RevolverBeam.Start))]
        [HarmonyPriority(Priority.Last)]
        public static void Prefix(RevolverBeam __instance)
        {
            if (!Plugin.isInRogueScene()) return;
            int count = Plugin.GetItemCount("Thunder Boomerang");
            if (count <= 0) return;
            if (__instance.hasBeenRicocheter) return;
            if (__instance.beamType == BeamType.Enemy) return;
            if (__instance.beamType == BeamType.MaliciousFace) return;
            __instance.ricochetAmount *= Mathf.RoundToInt(BounceMultiplier);
            if (__instance.hitAmount < 2) __instance.hitAmount = 2;
        }
    }

    public class BloodFlowingPlating : BaseItem
    {
        const float HealingPercentPerStack = 10f;

        public override string ItemName => "Blood Flowing Plating";
        public override string itemDescription => $"Have {HealingPercentPerStack}% of v1's healing (+{HealingPercentPerStack}% per stack)";
        public override List<ItemTag> itemTags => new List<ItemTag>() { ItemTag.Healing, ItemTag.Health };
        public override Rarity Rarity => Rarity.Legendary;
    }

    public class StyleBalls : BaseItem
    {
        const float StyleThreshold = 100f;
        const float DamagePerThresholdPerStack = 0.5f;
        const float CheckInterval = 5f;
        const int MinMultiplierToLaunch = 4;

        public override string ItemName => "Hell's Opinion";
        public override string itemDescription => $"every {StyleThreshold} style gotten, gain {DamagePerThresholdPerStack * 100}% (+{DamagePerThresholdPerStack * 100}% per stack) damage for the style orbs. After {CheckInterval} seconds, if gathered over {MinMultiplierToLaunch * DamagePerThresholdPerStack * 100}% damage, launch a style orb.";

        public override List<ItemTag> itemTags => new List<ItemTag>() { ItemTag.Damage };
        public override Rarity Rarity => Rarity.Legendary;

        int styleStart = 0;

        float timer = 0;

        public override void OnGotten(int count, bool firstPickup)
        {
            if (firstPickup)
                styleStart = StatsManager.Instance.stylePoints;
        }

        public override void OnUpdate(int count)
        {
            base.OnUpdate(count);
            if (!Room.isFighting) return;
            if (count == 0) return;

            timer += Time.deltaTime;

            if (timer >= CheckInterval)
            {
                int gainedStyle = StatsManager.Instance.stylePoints - styleStart;
                int mult = Mathf.CeilToInt(gainedStyle / StyleThreshold);
                if (mult >= MinMultiplierToLaunch)
                {
                    styleStart = StatsManager.Instance.stylePoints;
                    Plugin.Logger.LogInfo($"Launching orb with damage {mult * (DamagePerThresholdPerStack * count)}");
                    Launch(mult * (DamagePerThresholdPerStack * count));
                }
                timer = 0;
            }
        }
        bool attempted = false;
        GameObject missleModel = null;
        GameObject getMissleModel()
        {
            if (!attempted)
            {
                attempted = true;
                missleModel = Addressables.LoadAssetAsync<GameObject>("Assets/Modding/RogueMode/AuraProjectile.prefab").WaitForCompletion();
            }

            if (missleModel != null)
            {
                GameObject missle = GameObject.Instantiate(missleModel);
                return missle;
            }

            // fallback
            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fallback.GetComponent<Collider>().isTrigger = true;
            fallback.AddComponent<Rigidbody>().useGravity = false;
            return fallback;
        }
        public void Launch(float damage)
        {
            GameObject missle = getMissleModel();
            Missle proj = missle.AddComponent<Missle>();
            proj.speed *= 3.5f;
            proj.damage = damage;
            missle.transform.position = CameraController.Instance.GetDefaultPos() + Vector3.up * 3.5f;
        }
    }

    [HarmonyPatch]
    public class SplatterShot : BaseItem
    {
        public override string ItemName => "Splatter Shot";
        public override string itemDescription => "Replace your shotgun pellet with one large, high damage projectile that bursts into 15 (+15 per stack) bullets on hit.";
        public override Rarity Rarity => Rarity.Legendary;
        public override bool CanSpawn()
        {
            return Plugin.weapons.Any((x) => x.weapon == Plugin.Weapon.Shotgun && !x.Alternate);
        }

        List<GameObject> doneProjs = new List<GameObject>();

        public override void OnStart()
        {
            base.OnStart();
            new ProjectileCollideEffect(ItemName, (proj, type, other) =>
            {
                if (doneProjs.Contains(proj)) return;
                if (other == null) return;
                if (!LayerMaskDefaults.IsMatchingLayer(other.layer, LMD.EnemiesAndEnvironment)) return;
                if (proj.TryGetComponent<ShotOfSplatter>(out var splt))
                {
                    Vector3 awayDir = (proj.transform.position - other.transform.position).normalized;
                    if (awayDir == Vector3.zero)
                    {
                        awayDir = proj.transform.forward; // fallback if positions overlap
                    }

                    int c = Plugin.GetItemCount(this);

                    for (int i = 0; i < 15 * c; i++)
                    {
                        GameObject newProj = GameObject.Instantiate(splt.oldProj, proj.transform.position, Quaternion.identity);
                        Projectile projj = newProj.GetComponent<Projectile>();
                        projj.weaponType = "shotgun";
                        newProj.transform.position += -proj.transform.forward;

                        // Random direction, but flipped into the hemisphere facing away from 'other'
                        Vector3 randomDir = Random.insideUnitSphere;
                        if (Vector3.Dot(randomDir, awayDir) < 0f)
                        {
                            randomDir = -randomDir;
                        }

                        newProj.transform.forward = randomDir.normalized;
                    }
                    doneProjs.Add(proj);
                }
            });
        }

        public override void OnUpdate(int count)
        {
            base.OnUpdate(count);

        }


        public class ShotOfSplatter : MonoBehaviour
        {
            public GameObject oldProj;
        }

        [HarmonyPatch(typeof(Shotgun), nameof(Shotgun.Shoot))]
        [HarmonyPrefix]
        public static bool Prefix(Shotgun __instance)
        {
            int count = Plugin.GetItemCount(new SplatterShot().ItemName);
            if (count <= 0)
            {
                // No item -> let the original Shoot() run normally
                return true;
            }

            // --- Replicate the essential setup from the original Shoot() ---
            Rigidbody rb = MonoSingleton<NewMovement>.Instance.rb;
            Vector3 kickDir = Vector3.ProjectOnPlane(
                MonoSingleton<CameraController>.Instance.cam.transform.forward,
                MonoSingleton<NewMovement>.Instance.transform.up);
            rb.velocity += kickDir.normalized;

            Vector3 position = __instance.cam.transform.position;

            __instance.gunReady = false;

            PlayerAnimations pa = MonoSingleton<PlayerAnimations>.Instance;
            if (pa != null)
            {
                pa.Shoot(0.5f);
            }

            MonoSingleton<CameraController>.Instance.StopShake();

            Vector3 direction = __instance.cam.transform.forward;
            if (__instance.targeter.CurrentTarget && __instance.targeter.IsAutoAimed)
            {
                direction = __instance.targeter.GetAimDirectionFrom(
                    MonoSingleton<CameraController>.Instance.GetDefaultPos());
            }

            MonoSingleton<RumbleManager>.Instance.SetVibrationTracked(
                RumbleProperties.GunFireProjectiles, __instance.gameObject);
            position += direction;
            // --- Fire ONE big, high-damage projectile instead of the pellet spread ---
            GameObject bulletObj = Object.Instantiate(
                __instance.bullet, position, __instance.cam.transform.rotation);

            bulletObj.AddComponent<ShotOfSplatter>().oldProj = __instance.bullet;

            Projectile proj = bulletObj.GetComponent<Projectile>();
            proj.weaponType = "shotgun" + __instance.variation.ToString() + "_splatter";
            proj.sourceWeapon = __instance.gc.currentWeapon;
            proj.damage = 15;

            if(__instance.variation == 1)
                proj.damage += (2.5f * (__instance.primaryCharge + 1));
            if (__instance.targeter.CurrentTarget && __instance.targeter.IsAutoAimed)
            {
                bulletObj.transform.LookAt(__instance.targeter.CurrentTargetAimPosition);
            }
            else
            {
                bulletObj.transform.rotation = Quaternion.LookRotation(direction);
            }

            bulletObj.transform.localScale *= 6f;
            __instance.gunAud.SetPitch(Random.Range(0.75f, 0.85f));
            __instance.gunAud.clip = __instance.shootSound;
            __instance.gunAud.volume = 0.45f;
            __instance.gunAud.panStereo = 0f;
            __instance.gunAud.Play(true);

            __instance.cc.CameraShake(1.5f);

            if (__instance.variation == 1)
            {
                __instance.anim.SetTrigger("PumpFire");
            }
            else
            {
                __instance.anim.SetTrigger("Fire");
            }

            Transform[] shootPoints = __instance.shootPoints;
            for (int i = 0; i < shootPoints.Length; i++)
            {
                shootPoints[i].GetPositionAndRotation(out Vector3 sp, out Quaternion sr);
                Object.Instantiate(__instance.muzzleFlash, sp, sr);
            }

            __instance.releasingHeat = false;
            __instance.tempColor.a = 1f;
            __instance.heatSinkSMR.sharedMaterials[3].SetColor("_TintColor", __instance.tempColor);

            if (__instance.variation == 1)
            {
                __instance.primaryCharge = 0;
            }

            return false;
        }
    }

    public class PrimeHead : BaseItem
    {
        const float CooldownReductionPerStack = 0.60f;

        public override string ItemName => "Prime Head";
        public override string itemDescription => $"Cooldowns reduce by {CooldownReductionPerStack * 100}%";
        public override List<ItemTag> itemTags => new List<ItemTag>() { ItemTag.Utility };
        public override Rarity Rarity => Rarity.Legendary;
        Change change = new Change(percentage: 0);

        public override void OnStart()
        {
            new PlayerChange(cooldownReduction: change);
        }

        public override void OnUpdate(int count)
        {
            change.percentage = CooldownReductionPerStack * count;
        }

        public override void OnRemoval()
        {
            change.percentage = 0;
        }
    }

    public class VinnyPimpHat : BaseItem
    {
        const float FireInterval = 5f;
        const float DamagePerStack = 1.5f;

        public override string ItemName => "Vinny's Pimp Hat";
        public override string itemDescription => $"Every {FireInterval} seconds fire a purple saw that deals {DamagePerStack * 100}% (+{DamagePerStack * 100}% per stack) damage and stays until the room is cleared.";

        public override Rarity Rarity => Rarity.Legendary;
        public override List<Plugin.Weapon> WeaponProvisions => new List<Plugin.Weapon>() { Plugin.Weapon.Nailgun };
        float t = 0;
        bool wasPreviouslyFighting = false;

        GameObject sawPrefab = null;

        public override void OnUpdate(int count)
        {
            if (!Plugin.isInRogueScene()) return;
            if (count <= 0) return;

            if (wasPreviouslyFighting && !Room.isFighting)
            {
                Nail[] allNails = GameObject.FindObjectsOfType<Nail>();
                foreach (var nail in allNails)
                {
                    if (!nail.sawblade) continue;
                    if (nail.gameObject.name.Contains("SawVinny"))
                    {
                        Object.Destroy(nail.gameObject);
                    }
                }
            }

            if (Room.isFighting)
            {
                t += Time.deltaTime;

                if (t >= FireInterval)
                {
                    if (sawPrefab == null)
                        sawPrefab = Addressables.LoadAssetAsync<GameObject>("Assets/Modding/RogueMode/SawVinny.prefab").WaitForCompletion();

                    FireSaw(DamagePerStack * count);
                    t = 0;
                }
            }
            wasPreviouslyFighting = Room.isFighting;
        }

        public override void OnRemoval()
        {
            // Reset the timer so no saw fires immediately if the item is re-acquired
            t = 0;
            wasPreviouslyFighting = false;

            // Destroy any saws that are still alive in the world
            Nail[] allNails = GameObject.FindObjectsOfType<Nail>();
            foreach (var nail in allNails)
            {
                if (!nail.sawblade) continue;
                if (nail.gameObject.name.Contains("SawVinny"))
                {
                    Object.Destroy(nail.gameObject);
                }
            }
        }

        void FireSaw(float damage)
        {
            float currentSpread = 2f;
            GameObject gameObject2 = Object.Instantiate<GameObject>(sawPrefab, CameraController.Instance.GetDefaultPos(), CameraController.Instance.transform.rotation);

            gameObject2.transform.Rotate(Random.Range(-currentSpread / 3f, currentSpread / 3f), Random.Range(-currentSpread / 3f, currentSpread / 3f), Random.Range(-currentSpread / 3f, currentSpread / 3f));
            Rigidbody rigidbody;
            if (gameObject2.TryGetComponent<Rigidbody>(out rigidbody))
            {
                rigidbody.velocity = gameObject2.transform.forward * 200f;
            }
            Nail nail;
            if (gameObject2.TryGetComponent<Nail>(out nail))
            {
                nail.damage = damage;
                nail.hitAmount = float.MaxValue - 1;
            }

            KeepInBoundsRoom kibr = gameObject2.AddComponent<KeepInBoundsRoom>();

            kibr.RoomInside = Room.getObjectInsideRoom(NewMovement.Instance.transform.position);
            kibr.ResetVelocity = false;
        }
    }

    public class AgonizedMask : BaseItem
    {
        const float BaseChance = 10f;
        const float ChancePerStack = 5f;

        public override Rarity Rarity => Rarity.Legendary;
        public override string ItemName => "Agonized Mask";
        public override string itemDescription => $"Have a {BaseChance}% (+{ChancePerStack}% per stack) for an enemy to spawn as a puppet (does NOT include bosses)";
        public override List<ItemTag> itemTags => new List<ItemTag>() { ItemTag.Utility };
    }


    public class DualGun : BaseItem
    {
        const float BaseChance = 5f;
        const float ChancePerStack = 10f;

        public override Rarity Rarity => Rarity.Legendary;
        public override string ItemName => "Dual Gun";
        public override string itemDescription => $"Have a {BaseChance}% (+{ChancePerStack}% per stack) to get a dual wield";
        public override List<ItemTag> itemTags => new List<ItemTag>() { ItemTag.Utility };
        public override bool RequiresAtleastOneWeapon => true;
    }
    [HarmonyPatch]
    public class EyeOfGod : BaseItem
    {
        public override Rarity Rarity => Rarity.Legendary;
        public override string ItemName => "Eye of God";

        public override string itemDescription =>
            $"{BaseChance}% chance on hit to call down a virtue beam dealing {BaseDamage * 100}% base damage. " +
            $"Every 100% damage dealt increases activation chance by {ChancePerHundred}% (+{ChancePerHundred}% per stack) " +
            $"and beam damage by {DamagePerHundred * 100}% (+{DamagePerHundred * 100}% per stack).";

        public override float SpawnWeight => 0.75f;
        public override List<ItemTag> itemTags =>
            new List<ItemTag>() { ItemTag.Utility };

        private const float BaseChance = 3f;
        private const float ChancePerHundred = 3f;
        private const float MaxChance = 75f;

        private const float BaseDamage = 1.5f;
        private const float DamagePerHundred = 0.5f;
        private const float MaxDamageMultiplier = 7.5f;

        // GLOBAL accumulated damage
        private static float accumulatedDamage = 0f;

        const int MaximumBeamAmount = 3;

        public override void OnStart()
        {
            new HitEffect(ItemName, (eid, dmg) =>
            {
                int count = Plugin.GetItemCount(this);

                if (count <= 0) return;
                if (eid.hitter == "fire") return;
                if (eid.hitter == "godseye") return;

                int bC = GameObject.FindObjectsByType<GodBeam>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID).Length;
                if (bC >= MaximumBeamAmount) return;

                // Add damage dealt globally
                accumulatedDamage += dmg / Plugin.globalDamageMult.CalculateChanges(1f);

                // Every full 1.0 damage = one bonus step
                int thresholds = Mathf.FloorToInt(accumulatedDamage);

                float procChance =
                    BaseChance + (thresholds * ChancePerHundred * count);

                procChance = Mathf.Min(procChance, MaxChance);

                if (!Plugin.canExecute(procChance, eid.hitter))
                    return;

                float damageMultiplier =
                    BaseDamage + (thresholds * DamagePerHundred * count);

                damageMultiplier =
                    Mathf.Min(damageMultiplier, MaxDamageMultiplier);

                GameObject virtueBeam = Object.Instantiate(
                    AssetsManager.VirtueBeam,
                    eid.transform.position,
                    Quaternion.identity
                );
                if (virtueBeam.TryGetComponent<VirtueInsignia>(out var insig))
                {
                    insig.target = new EnemyTarget(eid);
                    insig.damage = Mathf.RoundToInt(damageMultiplier);
                    insig.windUpSpeedMultiplier = 2;
                }
                virtueBeam.name += "God";
                virtueBeam.AddComponent<GodBeam>();
                // RESET AFTER PROC
                accumulatedDamage = 0f;
            });
        }

        class GodBeam : MonoBehaviour
        {
            // nuthin
        }


        static Dictionary<VirtueInsignia, List<EnemyIdentifier>> alreadyHits = new Dictionary<VirtueInsignia, List<EnemyIdentifier>>();
        [HarmonyPatch(typeof(VirtueInsignia), nameof(VirtueInsignia.OnTriggerEnter))]
        public static bool Prefix(VirtueInsignia __instance, Collider other)
        {
            if (!Plugin.isInRogueScene()) return true;
            if (!__instance.gameObject.name.Contains("God")) return true;
            if (!alreadyHits.ContainsKey(__instance))
                alreadyHits.Add(__instance, new List<EnemyIdentifier>());
            if (__instance.target != null && (!__instance.target.isPlayer || other.gameObject.CompareTag("Player")))
            {

                EnemyIdentifier enemyIdentifier = other.GetComponent<EnemyIdentifier>();
                if (enemyIdentifier == null)
                {
                    EnemyIdentifierIdentifier component = other.GetComponent<EnemyIdentifierIdentifier>();
                    if (component != null)
                    {
                        enemyIdentifier = component.eid;
                    }
                }
                Rigidbody rigidbody;
                if (enemyIdentifier != null && other.TryGetComponent<Rigidbody>(out rigidbody) && !alreadyHits[__instance].Contains(enemyIdentifier))
                {
                    rigidbody.AddExplosionForce(1000f, __instance.transform.position, 10f);
                    enemyIdentifier.hitter = "godseye";
                    enemyIdentifier.SimpleDamage((float)__instance.damage);
                    alreadyHits[__instance].Add(enemyIdentifier);
                }

            }
            Flammable component2 = other.GetComponent<Flammable>();
            if (component2 && !component2.playerOnly)
            {
                component2.Burn(10f, false);
            }
            return false;
        }

    }

    public class JumperCable : BaseItem
    {
        const float GrowthRate = 0.05f;
        const float BaseChance = 0.10f;
        const float MaxChance = 0.20f;

        public override string ItemName => "Jumper Cable";
        public override string itemDescription => $"Enemies have a {BaseChance * 100}% chance to be shocked when a saw blade hits them. (+{GrowthRate * 100}% per stack)";
        public override List<ItemTag> itemTags => new List<ItemTag>() { ItemTag.Damage };

        public override List<Plugin.Weapon> WeaponRequirements => new List<Plugin.Weapon>() { Plugin.Weapon.Nailgun };

        public override Rarity Rarity => Rarity.Legendary;
        public override void OnStart()
        {
            new HitEffect(ItemName, (eid, dmg) =>
            {
                int c = Plugin.GetItemCount(this);
                if (c <= 0 || eid.hitter != "sawblade") return;

                float chance = Plugin.LogarithmicChance(c - 1, GrowthRate, BaseChance, MaxChance) * 100;
                if (Plugin.canExecute(chance, "", false))
                {
                    eid.hitter = "zapper";
                    eid.hitterAttributes.Add(HitterAttribute.Electricity);
                    eid.DeliverDamage(eid.gameObject, Vector3.up * 1000f, eid.transform.position, 10f, true, 0f, null, false, false);
                    foreach (EnemyIdentifierIdentifier enemyIdentifierIdentifier in eid.GetComponentsInChildren<EnemyIdentifierIdentifier>())
                    {
                        Object.Instantiate<GameObject>(AssetsManager.zapThingy, enemyIdentifierIdentifier.transform.position, Quaternion.identity).transform.localScale *= 0.5f;
                    }
                }
            });
        }
    }

    [HarmonyPatch]
    public class ResidualCannon : BaseItem
    {
        const float DurationPerStack = 0.5f;
        const float DamageMultiplier = 10f;

        public override string ItemName => "Residual Cannon";
        public override string itemDescription => $"On hitscan fire, create a continuous beam that stays for {DurationPerStack}s (+{DurationPerStack}s per stack) and deals 100% TOTAL damage";
        public override List<ItemTag> itemTags => new List<ItemTag>() { ItemTag.Damage };
        public override Rarity Rarity => Rarity.Legendary;
        public override List<Plugin.Weapon> WeaponRequirements => new List<Plugin.Weapon>() { Plugin.Weapon.Revolver };

        // No OnRemoval needed — the patch already gates on GetItemCount("Residual Cannon") > 0.

        [HarmonyPatch(typeof(RevolverBeam), nameof(RevolverBeam.Start))]
        public static void Postfix(RevolverBeam __instance)
        {
            if (!Plugin.isInRogueScene()) return;
            int count = Plugin.GetItemCount("Residual Cannon");
            if (count <= 0) return;
            if (__instance.beamType == BeamType.Enemy) return;
            if (__instance.beamType == BeamType.MaliciousFace) return;

            GameObject beam = Object.Instantiate(AssetsManager.mindflayerBeam, __instance.transform.position, __instance.transform.rotation);
            if (beam.TryGetComponent<ContinuousBeam>(out ContinuousBeam bem))
            {
                bem.damage = __instance.damage * DamageMultiplier;
                bem.canHitPlayer = false;
                bem.canHitEnemy = true;
            }

            if (beam.TryGetComponent<LineRenderer>(out LineRenderer lr))
            {
                lr.startColor = __instance.lr.startColor;
                lr.endColor = __instance.lr.endColor;
                lr.colorGradient = __instance.lr.colorGradient;
            }
            Object.Destroy(beam, DurationPerStack * count);
        }
    }


    public class BentSpoon : BaseItem
    {
        public override string ItemName => "Bent Spoon";
        public override string itemDescription => "All your projectile home.";
        public override bool CanOnlyHaveOne => true;

        public override Rarity Rarity => Rarity.Legendary;
        public override bool RequiresAtleastOneWeapon => true;
        public override void OnStart()
        {
            base.OnStart();
            new ProjectileStartEffect(ItemName, (proj, type) =>
            {
                switch (type)
                {
                    case ProjectileType.Projectile:
                        proj.AddComponent<ProjectileHoming>();
                        break;
                    case ProjectileType.Nail:
                        proj.AddComponent<NailHoming>();
                        break;
                    case ProjectileType.Rocket:
                        proj.AddComponent<RocketHoming>();
                        proj.AddComponent<GrenadeHoming>();
                        break;
                }

            });
        }

        static void TintPurple(GameObject go, bool cloneMaterial = false)
        {
            Renderer[] rends = go.GetComponentsInChildren<Renderer>();
            foreach (var rend in rends)
            {
                if (cloneMaterial)
                {
                    Material mat = new Material(rend.material);
                    if (mat.HasProperty("_Color"))
                        mat.color = mat.color * new Color(0.5f, 0, 0.5f);
                    rend.material = mat;
                }
                else
                {
                    foreach (var mat in rend.materials)
                    {
                        if (mat.HasProperty("_Color"))
                            mat.color = mat.color * new Color(0.5f, 0, 0.5f);
                    }
                }
            }
        }

        public static bool CanTarget(TargetDataRef data)
        {
            return data.target.EID.enemyType != EnemyType.Idol && data.target.EID.enemyType != EnemyType.Deathcatcher;
        }

        public class GrenadeHoming : MonoBehaviour
        {
            Grenade grenade;
            VisionQuery visionQuery;
            Vision vision;
            Rigidbody rb;
            System.Threading.CancellationTokenSource visionCts;

            void Awake()
            {
                grenade = GetComponent<Grenade>();
                rb = GetComponent<Rigidbody>();

                // Ensure it's a player grenade and NOT a rocket
                if (grenade == null || grenade.rocket || grenade.enemy)
                {
                    Destroy(this);
                    return;
                }

                this.visionQuery = new VisionQuery("GrenadeHomingSight", (TargetDataRef t) => t.target.isEnemy && CanTarget(t) && !t.IsObstructed(transform.position, LayerMaskDefaults.Get(LMD.Environment), false));
                this.vision = new Vision(base.transform.position, new VisionTypeFilter(new TargetType[]
                {
            TargetType.ENEMY
                }));
                visionCts = new System.Threading.CancellationTokenSource();
                MonoSingleton<PortalManagerV2>.Instance.TargetTracker.RegisterVision(this.vision, visionCts.Token);
            }

            void Start()
            {
                // Optional: Give standard grenades a purple tint to match Bent Spoon
                TintPurple(gameObject);
            }

            void FixedUpdate()
            {
                if (this.visionQuery == null || grenade == null) return;
                if (rb == null) rb = grenade.rb;
                if (rb == null || grenade.magnets.Count > 0) return;

                vision.UpdateSourcePos(transform.position);
                TargetDataRef src;

                // If vision spots an enemy, gently curve the grenade's velocity toward them
                if (this.vision.TrySee(this.visionQuery, out src) && src.target != null)
                {
                    Vector3 targetPos = src.target.Position;
                    Vector3 direction = (targetPos - transform.position).normalized;

                    float currentSpeed = rb.velocity.magnitude;
                    if (currentSpeed < 0.1f) currentSpeed = 20f;

                    // Smoothly steer velocity vector towards the target without completely breaking physics arc
                    rb.velocity = Vector3.RotateTowards(rb.velocity, direction * currentSpeed, 10f * Mathf.Deg2Rad * Time.fixedDeltaTime * 60f, 0f);

                    // Align rotation to face movement direction
                    if (rb.velocity != Vector3.zero)
                    {
                        transform.rotation = Quaternion.LookRotation(rb.velocity);
                    }
                }
            }

            void OnDestroy()
            {
                // Prevent the TargetTracker's vision list from growing forever.
                if (visionCts != null)
                {
                    visionCts.Cancel();
                    visionCts.Dispose();
                }
            }
        }

        public class RocketHoming : MonoBehaviour
        {
            Grenade grenade;
            VisionQuery visionQuery;
            Vision vision;
            System.Threading.CancellationTokenSource visionCts;

            void Awake()
            {
                grenade = GetComponent<Grenade>();
                if (grenade == null || !grenade.rocket || grenade.enemy)
                {
                    Destroy(this);
                    return;
                }

                this.visionQuery = new VisionQuery("RocketHomingSight", (TargetDataRef t) => t.target.isEnemy && CanTarget(t) && !t.IsObstructed(transform.position, LayerMaskDefaults.Get(LMD.Environment), false));
                this.vision = new Vision(base.transform.position, new VisionTypeFilter(new TargetType[]
                {
            TargetType.ENEMY
                }));
                visionCts = new System.Threading.CancellationTokenSource();
                MonoSingleton<PortalManagerV2>.Instance.TargetTracker.RegisterVision(this.vision, visionCts.Token);
            }

            void Start()
            {
                TintPurple(gameObject);
            }

            void FixedUpdate()
            {
                if (this.visionQuery == null || grenade == null || grenade.frozen || grenade.playerRiding) return;

                vision.UpdateSourcePos(transform.position);
                TargetDataRef src;

                if (this.vision.TrySee(this.visionQuery, out src) && src.target != null && grenade.magnets.Count == 0)
                {
                    Vector3 targetPos = src.target.Position;
                    Quaternion targetRot = Quaternion.LookRotation(targetPos - transform.position);

                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, Time.fixedDeltaTime * 180f);

                    if (grenade.rb != null && grenade.rocketSpeed > 0f)
                    {
                        grenade.rb.velocity = transform.forward * grenade.rocketSpeed;
                    }
                }
            }

            void OnDestroy()
            {
                if (visionCts != null)
                {
                    visionCts.Cancel();
                    visionCts.Dispose();
                }
            }
        }

        public class NailHoming : MonoBehaviour
        {
            Nail nail;
            VisionQuery visionQuery;
            Vision vision;
            Rigidbody rb;
            System.Threading.CancellationTokenSource visionCts;

            // --- Optimization: throttle + cache ---
            const float CheckInterval = 0.08f; // ~12 checks/sec instead of 50
            float nextCheckTime;
            Vector3 cachedDirection;
            bool hasCachedDirection;

            void Awake()
            {
                nail = GetComponent<Nail>();
                rb = GetComponent<Rigidbody>();

                if (nail == null || nail.enemy)
                {
                    Destroy(this);
                    return;
                }

                this.visionQuery = new VisionQuery("NailHomingSight", (TargetDataRef t) => t.target.isEnemy && CanTarget(t) && !t.IsObstructed(transform.position, LayerMaskDefaults.Get(LMD.Environment), false));
                this.vision = new Vision(base.transform.position, new VisionTypeFilter(new TargetType[]
                {
            TargetType.ENEMY
                }));
                visionCts = new System.Threading.CancellationTokenSource();
                MonoSingleton<PortalManagerV2>.Instance.TargetTracker.RegisterVision(this.vision, visionCts.Token);

                // Stagger which frame each nail does its expensive check on
                nextCheckTime = Time.time + UnityEngine.Random.Range(0f, CheckInterval);
            }

            void Start()
            {
                TintPurple(gameObject);
            }

            void FixedUpdate()
            {
                if (nail.currentHitEnemy != null) return;
                if (this.visionQuery == null || nail.hit) return;
                if (rb == null) rb = nail.rb;
                if (rb == null) return;

                // Only do the expensive vision/raycast query occasionally
                if (Time.time >= nextCheckTime)
                {
                    nextCheckTime = Time.time + CheckInterval;

                    vision.UpdateSourcePos(transform.position);
                    TargetDataRef src;

                    if (this.vision.TrySee(this.visionQuery, out src) && src.target != null)
                    {
                        cachedDirection = (src.target.Position - transform.position).normalized;
                        hasCachedDirection = true;
                    }
                    else
                    {
                        hasCachedDirection = false;
                    }
                }

                // Cheap steering runs every physics tick using the cached direction
                if (hasCachedDirection)
                {
                    float currentSpeed = rb.velocity.magnitude;
                    if (currentSpeed < 0.1f) currentSpeed = 100f;

                    rb.velocity = Vector3.RotateTowards(rb.velocity, cachedDirection * currentSpeed, 15f * Mathf.Deg2Rad * Time.fixedDeltaTime * 60f, 0f);
                }
            }

            void OnDestroy()
            {
                if (visionCts != null)
                {
                    visionCts.Cancel();
                    visionCts.Dispose();
                }
            }
        }

        public class ProjectileHoming : MonoBehaviour
        {
            Projectile proj;
            VisionQuery visionQuery;
            Vision vision;
            TargetHandle handle;
            Vector3 lastDimensionalTarget;
            TargetHandle lastTargetData;
            System.Threading.CancellationTokenSource visionCts;

            void Awake()
            {
                proj = GetComponent<Projectile>();
                if (!proj.playerBullet)
                {
                    Destroy(this);
                    return;
                }
                this.visionQuery = new VisionQuery("HomingSight", (TargetDataRef t) => t.target.isEnemy && CanTarget(t) && !t.IsObstructed(transform.position, LayerMaskDefaults.Get(LMD.Environment), false));
                this.vision = new Vision(base.transform.position, new VisionTypeFilter(new TargetType[]
                {
                TargetType.ENEMY
                }));
                visionCts = new System.Threading.CancellationTokenSource();
                MonoSingleton<PortalManagerV2>.Instance.TargetTracker.RegisterVision(this.vision, visionCts.Token);
            }
            void Start()
            {
                TintPurple(gameObject, cloneMaterial: true);
            }

            void Update()
            {
                if (this.visionQuery == null) return;
                vision.UpdateSourcePos(transform.position);
                TargetDataRef src;
                if (this.vision.TrySee(this.visionQuery, out src))
                {
                    this.handle = src.CreateHandle();
                    this.lastDimensionalTarget = Vector3.zero;
                    this.lastTargetData = src.ToData();
                    DoHome();
                }
            }

            void DoHome()
            {
                proj.homingType = HomingType.Instant;
                proj.targetHandle = handle;
            }

            void OnDestroy()
            {
                if (visionCts != null)
                {
                    visionCts.Cancel();
                    visionCts.Dispose();
                }
            }
        }

        [HarmonyPatch(typeof(RevolverBeam), "Start")]
        public class RevolverBeam_Start_BentSpoon_Bend
        {
            const float DetectionRadius = 25f;
            const float MaxBendAngle = 60f;

            static void Prefix(RevolverBeam __instance)
            {
                if (Plugin.GetItemCount("Bent Spoon") <= 0) return;
                if (__instance.beamType != BeamType.Revolver) return;
                if (__instance.fake) return;
                if (__instance.aimAssist) return;

                Transform t = __instance.transform;
                Vector3 origin = t.position;
                Vector3 forward = t.forward;

                Transform target = FindBendTarget(origin, forward, DetectionRadius, MaxBendAngle);
                if (target == null) return;

                Vector3 newDir = (target.position - origin).normalized;
                t.rotation = Quaternion.LookRotation(newDir, t.up);

                var marker = __instance.gameObject.AddComponent<BentSpoonBendMarker>();
                marker.originalForward = forward;
            }

            static Transform FindBendTarget(Vector3 origin, Vector3 forward, float radius, float maxAngle)
            {
                var hits = Physics.OverlapSphere(origin, 1000f, LayerMaskDefaults.Get(LMD.Enemies), QueryTriggerInteraction.Collide);
                Transform best = null;
                float bestLateral = float.MaxValue;

                foreach (var col in hits)
                {
                    var eii = col.GetComponentInParent<EnemyIdentifierIdentifier>();
                    if (eii == null || eii.eid == null || eii.eid.dead || eii.eid.enemyType == EnemyType.Idol || eii.eid.enemyType == EnemyType.Deathcatcher) continue;

                    Vector3 toTarget = col.transform.position - origin;
                    float alongForward = Vector3.Dot(toTarget, forward);
                    if (alongForward <= 0f) continue;

                    Vector3 closestOnRay = origin + forward * alongForward;
                    float lateralDist = Vector3.Distance(closestOnRay, col.transform.position);
                    if (lateralDist > radius) continue;

                    if (Vector3.Angle(forward, toTarget) > maxAngle) continue;
                    if (Physics.Linecast(origin, col.transform.position, LayerMaskDefaults.Get(LMD.Environment))) continue;

                    if (lateralDist < bestLateral)
                    {
                        bestLateral = lateralDist;
                        best = col.transform;
                    }
                }
                return best;
            }

            [HarmonyPatch(typeof(RevolverBeam), "Shoot")]
            public class RevolverBeam_Shoot_BentSpoon_Visual
            {
                const int CurveSegments = 16;

                static void Postfix(RevolverBeam __instance)
                {
                    var marker = __instance.GetComponent<BentSpoonBendMarker>();
                    if (marker == null) return;

                    var lr = __instance.GetComponent<LineRenderer>();
                    if (lr == null || lr.positionCount < 2) { Object.Destroy(marker); return; }
                    lr.startColor = new Color(0.5f, 0, 0.5f);
                    lr.endColor = new Color(0.5f, 0, 0.5f);
                    Vector3 start = lr.GetPosition(0);
                    Vector3 end = lr.GetPosition(lr.positionCount - 1);
                    float dist = Vector3.Distance(start, end);
                    Vector3 control = start + marker.originalForward.normalized * (dist * 0.35f);

                    Vector3[] curve = new Vector3[CurveSegments + 1];
                    for (int i = 0; i <= CurveSegments; i++)
                    {
                        float tt = i / (float)CurveSegments;
                        curve[i] = Bezier(start, control, end, tt);
                    }

                    lr.positionCount = curve.Length;
                    lr.SetPositions(curve);
                    Object.Destroy(marker);
                }

                static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
                {
                    Vector3 ab = Vector3.Lerp(a, b, t);
                    Vector3 bc = Vector3.Lerp(b, c, t);
                    return Vector3.Lerp(ab, bc, t);
                }
            }
        }

        public class BentSpoonBendMarker : MonoBehaviour
        {
            public Vector3 originalForward;
        }
    }

    public class Soulcatcher : BaseItem
    {
        const float DamagePerKill = 0.1f;

        public override string ItemName => "Soulcatcher";
        public override string itemDescription => $"Each kill increases damage by {DamagePerKill * 100}% for the room";
        public override Rarity Rarity => Rarity.Legendary;
        public override List<ItemTag> itemTags => new List<ItemTag>() { ItemTag.Damage };
        Change dmgChange;
        float killBonus = 0f;

        public override void OnStart()
        {
            dmgChange = new Change(percentage: 0);
            new PlayerChange(globalDamageMult: dmgChange);

            new DeathEffect(ItemName, (eid) =>
            {
                int count = Plugin.GetItemCount(this);
                if (count <= 0) return;
                killBonus += DamagePerKill;
            });
        }

        public override void OnUpdate(int count)
        {
            dmgChange.percentage = killBonus;
            if (!Room.isFighting)
                killBonus = 0;
        }

        public override void OnRemoval()
        {
            // Reset the accumulated kill bonus so it doesn't carry over if re-acquired
            killBonus = 0f;
            dmgChange.percentage = 0;
        }
    }

    [HarmonyPatch]
    public class CerberusHead : BaseItem
    {
        const float BaseSizeBonus = 0.5f;
        const float SizeBonusPerStack = 0.25f;
        const float BaseDamageMultiplier = 2f;
        const float DamageBonusPerStack = 0.5f;

        public override string ItemName => "Cerberus Head";
        public override string itemDescription => $"All Explosions caused by the player (rockets, projectile boosts, instakills) are {BaseSizeBonus * 100}% larger and do {(BaseDamageMultiplier - 1) * 100}% more damage ({SizeBonusPerStack * 100}% larger and {DamageBonusPerStack * 100}% damage per stack) (Your explosions no longer damage you)";
        public override Rarity Rarity => Rarity.Legendary;
        public override List<ItemTag> itemTags => new List<ItemTag>() { ItemTag.Damage };


        [HarmonyPatch(typeof(Explosion), nameof(Explosion.Start))]
        public static void Prefix(Explosion __instance)
        {
            int c = Plugin.GetItemCount("Cerberus Head");

            if (c <= 0) return;

            if (__instance.enemy) return;

            __instance.maxSize *= 1f + BaseSizeBonus + SizeBonusPerStack * (c - 1);
            __instance.damage = Mathf.RoundToInt(__instance.damage * (BaseDamageMultiplier + DamageBonusPerStack * (c - 1)));

            __instance.hasHitPlayer = true;
        }
    }

    public class WarMachine : BaseItem
    {
        const float AttackSpeedPerStack = 0.45f;
        const float MoveSpeedPerStack = 0.20f;

        public override string ItemName => "War Machine";
        public override string itemDescription => $"Attack speed +{AttackSpeedPerStack * 100}%, move speed +{MoveSpeedPerStack * 100}%";
        public override Rarity Rarity => Rarity.Legendary;
        public override List<ItemTag> itemTags => new List<ItemTag>() { ItemTag.Damage, ItemTag.Utility };
        Change atkChange;
        Change moveChange;

        public override void OnStart()
        {
            atkChange = new Change(percentage: 0);
            moveChange = new Change(percentage: 0);
            new PlayerChange(attackSpeed: atkChange, moveSpeed: moveChange);
        }

        public override void OnUpdate(int count)
        {
            atkChange.percentage = AttackSpeedPerStack * count;
            moveChange.percentage = MoveSpeedPerStack * count;
        }

        public override void OnRemoval()
        {
            atkChange.percentage = 0;
            moveChange.percentage = 0;
        }
    }

    public class HellsFire : BaseItem
    {
        const float DamagePerStack = 1f;

        public override string ItemName => "Hell's Fire";
        public override string itemDescription => $"Enemies on fire take +{DamagePerStack * 100}% more damage";
        public override Rarity Rarity => Rarity.Legendary;
        public override List<ItemTag> itemTags => new List<ItemTag>() { ItemTag.Damage };
        public override List<Plugin.Weapon> WeaponRequirements => new List<Plugin.Weapon>() { Plugin.Weapon.RocketLauncher };
        // No OnRemoval needed — HitEffect and DamageModifier both gate on GetItemCount > 0.

        public override void OnStart()
        {

            new DamageModifier(ItemName, (eid) =>
            {
                int count = Plugin.GetItemCount(this);
                if (count <= 0 || eid.dead || eid.hitter != "fire") return 1f;
                Flammable[] flams = eid.flammables.ToArray();
                foreach (var f in flams)
                {
                    if (f.burning)
                        return 1f + (DamagePerStack * count);
                }
                return 1f;
            });
        }
    }

    public class MachineVirus : BaseItem
    {
        const float DamagePerHitPerStack = 0.005f;

        public override string ItemName => "Machine Virus";
        public override string itemDescription => $"Increase damage by {DamagePerHitPerStack * 100}% for every time that enemy was hit.";

        Dictionary<EnemyIdentifier, int> hits = new Dictionary<EnemyIdentifier, int>();
        public override Rarity Rarity => Rarity.Legendary;
        public override List<ItemTag> itemTags => new List<ItemTag>() { ItemTag.Damage };

        public override void OnStart()
        {
            new DamageModifier(ItemName, (eid) =>
            {
                int c = Plugin.GetItemCount(this);
                if (c == 0) return 1f;

                int hit = 0;
                if (!hits.TryGetValue(eid, out hit))
                {
                    hits.Add(eid, hit = 1);
                    hit = 1;
                }
                hits[eid]++;
                return 1 + ((DamagePerHitPerStack * c) * hit);
            });
        }

        public override void OnRemoval()
        {
            // Clear tracked hit counts so stale data doesn't persist into future runs
            hits.Clear();
        }
    }
}