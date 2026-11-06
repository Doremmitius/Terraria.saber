using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace heihie.Content.Items
{
    // ── Tier 0：泥巴剑（35 伤 / 无弹幕 / 剑影 1 / 不蓄力）──
    public class siespaditalTier0 : siespaditalBase
    {
        public override int Tier => 0;
        public override int BaseDamage => 35;
        public override int VolleyCount => 0;        // 0 = 不发射飞剑，纯挥砍
        public override int SwingGhostCount => 1;    // 没有残影
        public override int MaxChargeLevel => 0;     // 不支持蓄力

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.DirtBlock, 5)   // 5 个土块
                .AddTile(TileID.WorkBenches)          // 工作台合成
                .Register();
        }
    }

    // ── Tier 1：狱石剑（40 伤 / 1 弹幕 / 剑影 3 / 不蓄力）──
    public class siespaditalTier1 : siespaditalBase
    {
        public override int Tier => 1;
        public override int BaseDamage => 40;
        public override int VolleyCount => 1;
        public override int SwingGhostCount => 3;
        public override int MaxChargeLevel => 0;

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<siespaditalTier0>(1)   // 上一档 + 熔岩大剑
                .AddIngredient(ItemID.FieryGreatsword, 1)
                .AddTile(TileID.Anvils)               // 铁砧合成
                .Register();
        }
    }

    // ── Tier 2：神圣剑（50 伤 / 2 弹幕 / 剑影 5 / 一阶蓄力）──
    public class siespaditalTier2 : siespaditalBase
    {
        public override int Tier => 2;
        public override int BaseDamage => 50;
        public override int VolleyCount => 2;
        public override int SwingGhostCount => 5;
        public override int MaxChargeLevel => 1;

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<siespaditalTier1>(1)   // 上一档 + 断钢剑 + 神圣锭
                .AddIngredient(ItemID.Excalibur, 1)
                .AddIngredient(ItemID.HallowedBar, 10)
                .AddTile(TileID.MythrilAnvil)         // 秘银砧合成
                .Register();
        }
    }

    // ── Tier 3：四柱剑（100 伤 / 3 弹幕 / 剑影 7 / 一阶蓄力）──
    public class siespaditalTier3 : siespaditalBase
    {
        public override int Tier => 3;
        public override int BaseDamage => 100;
        public override int VolleyCount => 3;
        public override int SwingGhostCount => 7;
        public override int MaxChargeLevel => 1;

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<siespaditalTier2>(1)             // 上一档 + 四种月柱碎片
                .AddIngredient(ItemID.FragmentSolar, 20)
                .AddIngredient(ItemID.FragmentVortex, 20)
                .AddIngredient(ItemID.FragmentNebula, 20)
                .AddIngredient(ItemID.FragmentStardust, 20)
                .AddTile(TileID.LunarCraftingStation)           // 远古操纵机合成
                .Register();
        }
    }

    // ── Tier 4：喵刀（140 伤 / 3 弹幕 / 剑影 9 / 二阶蓄力 / 蓄力后弹幕随机变多）──
    public class siespadital : siespaditalBase
    {
        public override int Tier => 4;
        public override int BaseDamage => 140;
        public override int VolleyCount => 3;
        public override int SwingGhostCount => 9;
        public override int MaxChargeLevel => 2;

        // 👇 一阶蓄力随机 3~5，二阶蓄力随机 5~7，普通固定 3
        public override (int min, int max) GetVolleyRange(int level)
        {
            if (level == 1) return (3, 5);
            if (level == 2) return (5, 7);
            return (VolleyCount, VolleyCount);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<siespaditalTier3>(1)   // 上一档 + 喵刀
                .AddIngredient(ItemID.Meowmere, 1)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }
}
