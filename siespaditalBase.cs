using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using heihie.Content.Projectiles;

namespace heihie.Content.Items
{
    // siespadital 系列所有档位武器的共同基类。
    // 具体数值（伤害、弹幕数量、残影数、蓄力上限）由子类 override 覆盖。
    public abstract class siespaditalBase : ModItem
    {
        public override string Texture => "heihie/Content/Items/siespadital";

        // ===== 子类必须/可以覆盖的"参数" =====
        public abstract int Tier { get; }                 // 档位（影响稀有度、售价）
        public abstract int BaseDamage { get; }           // 基础伤害
        public virtual int VolleyCount => 1;              // 每次挥砍发几把飞剑（0 = 不发）
        public virtual int SwingGhostCount => 1;          // 挥砍时画几个残影
        public virtual int MaxChargeLevel => 0;           // 最高能蓄到几级（0 = 不支持蓄力）
        public virtual float Level1DamageMult => 1.5f;    // 一阶蓄力伤害倍率
        public virtual float Level2DamageMult => 3f;      // 二阶蓄力伤害倍率

        // 👇 新增：某一等级下的弹幕数量范围（默认固定 = VolleyCount）
        // 返回 (最少, 最多)，实际发射时在这个闭区间里随机取
        public virtual (int min, int max) GetVolleyRange(int level)
        {
            return (VolleyCount, VolleyCount);
        }

        public override void SetDefaults()
        {
            Item.damage = BaseDamage;
            Item.DamageType = DamageClass.Melee;
            Item.width = 100;
            Item.height = 100;
            Item.useTime = 10;         // 使用间隔
            Item.useAnimation = 20;    // 挥动一次动画长度
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6f;
            Item.autoReuse = true;     // 按住自动连续挥
            Item.noUseGraphic = true;  // 不画原版手里那把剑（我们自己的弹幕来画）
            Item.channel = true;       // 允许"按住持续使用"（右键蓄力要用）
            Item.shoot = ModContent.ProjectileType<siespaditalSwingNew>(); // 默认挥砍弹幕
            Item.UseSound = SoundID.Item1 with { Volume = 0.6f, PitchVariance = 0.2f };

            // 稀有度按档位从白→绿→紫→青→红一路升高
            Item.rare = Tier switch
            {
                0 => ItemRarityID.White,
                1 => ItemRarityID.Green,
                2 => ItemRarityID.LightPurple,
                3 => ItemRarityID.Cyan,
                4 => ItemRarityID.Red,
                _ => ItemRarityID.Yellow
            };
            // 售价随档位上涨（1 金 + 档位×5 金）
            Item.value = Item.buyPrice(gold: 1 + Tier * 5);
        }

        // 只有当这档武器支持蓄力（MaxChargeLevel > 0）时，右键才有反应
        public override bool AltFunctionUse(Player player) => MaxChargeLevel > 0;

        // 每次"使用"前决定这次要用什么参数（区分左键挥砍 / 右键蓄力）
        public override bool CanUseItem(Player player)
        {
            if (player.altFunctionUse == 2 && MaxChargeLevel > 0)
            {
                // ===== 右键 = 蓄力 =====
                Item.useTime = 90;      // 蓄力弹幕本身不按帧用，这个 90 只是"别让原版太快重触发"
                Item.useAnimation = 90;
                Item.channel = true;
                Item.shoot = ModContent.ProjectileType<siespaditalCharge>();
                Item.UseSound = SoundID.Item4;
            }
            else
            {
                // ===== 左键 = 挥砍 =====
                Item.useTime = 10;
                Item.useAnimation = 20;
                Item.channel = true;
                Item.shoot = ModContent.ProjectileType<siespaditalSwingNew>();
                Item.UseSound = SoundID.Item1 with { Volume = 0.6f, PitchVariance = 0.2f };
            }
            return base.CanUseItem(player);
        }

        // 在真正生成弹幕之前做最后拦截：
        // 同一个弹幕不想同时存在多个（会互相打架、浪费性能），所以有就拒绝生成。
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            if (player.altFunctionUse == 2)
            {
                // 右键：不支持蓄力的武器直接忽略
                if (MaxChargeLevel == 0) return false;
                int chargeType = ModContent.ProjectileType<siespaditalCharge>();
                if (player.ownedProjectileCounts[chargeType] > 0) return false; // 已经在蓄力了，别叠
                return true;
            }

            // 左键：场上已经有挥砍弹幕就别再生成第二个
            int swingType = ModContent.ProjectileType<siespaditalSwingNew>();
            if (player.ownedProjectileCounts[swingType] > 0) return false;
            return true;
        }
    }
}
