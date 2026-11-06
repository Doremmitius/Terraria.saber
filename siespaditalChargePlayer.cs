using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Microsoft.Xna.Framework;
using heihie.Content.Items;

namespace heihie.Common
{
    // 挂在玩家身上的"蓄力状态"数据，记录当前蓄到几级、还剩多少时间。
    public class siespaditalChargePlayer : ModPlayer
    {
        public int ChargeLevel = 0; // 当前蓄力等级（0 = 没蓄力）
        public int ChargeTimer = 0; // 剩余强化时间（只有挥剑时才会扣）

        public override void PostUpdate()
        {
            // 没在挥剑 + 手里拿的不是 siespadital 系列武器 → 清空蓄力
            if (Player.itemAnimation <= 0 && !(Player.HeldItem.ModItem is siespaditalBase))
            {
                ChargeLevel = 0;
                ChargeTimer = 0;
                return;
            }

            // 有蓄力时，玩家身上定期冒火花提示
            if (ChargeLevel > 0)
            {
                // 用 GameUpdateCount 判断，避免 Timer 停在 300 时每帧都 %5==0
                // （如果用 ChargeTimer 来算，Timer 不动的时候会每帧都触发一次，特效会爆）
                if (Main.myPlayer == Player.whoAmI && Main.GameUpdateCount % 5 == 0)
                {
                    // 二阶红色、一阶金色
                    Color dustColor = ChargeLevel == 2 ? Color.Red : Color.Gold;
                    Dust d = Dust.NewDustPerfect(
                        Player.Center + Main.rand.NextVector2Circular(30f, 30f), // 在玩家周围 30 像素内随机位置
                        DustID.PurpleTorch, Vector2.Zero, 0, dustColor, 1.5f);
                    d.noGravity = true;
                }
            }
        }
    }
}
