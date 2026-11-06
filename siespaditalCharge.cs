using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using heihie.Common;
using heihie.Content.Items;

namespace heihie.Content.Projectiles
{
    // 右键蓄力时生成的"蓄力弹幕"。
    // 它本身不会打怪，只是挂在玩家身上当个计时器：时间一到就给玩家加一层蓄力等级，然后自己消失。
    public class siespaditalCharge : ModProjectile
    {
        // 直接借用剑的贴图来画，省得单独做一张蓄力特效图
        public override string Texture => "heihie/Content/Items/siespadital";

        // ===== 贴图锚点（贴图 100×100，剑柄在左下角，剑尖在右上角）=====
        private static readonly Vector2 HandlePixel = new Vector2(0f, 100f); // 剑柄在贴图上的像素坐标
        private static readonly Vector2 TipPixel    = new Vector2(100f, 0f); // 剑尖在贴图上的像素坐标
        private static Vector2 HandleToTip => TipPixel - HandlePixel;        // 剑柄指向剑尖的向量（用来算剑有多长、朝哪）
        private static float TipOffset => HandleToTip.ToRotation();          // 贴图本身剑尖方向相对"正右方"的偏角
        // 我们想要的是"剑尖朝某个角度"，但贴图本身剑尖就斜了 45°，
        // 所以画图时要先减掉这个偏角才是真正该传给 SpriteBatch 的旋转角
        private static float DrawRot(float visualAngle) => visualAngle - TipOffset;

        public override void SetDefaults()
        {
            Projectile.width = 100;
            Projectile.height = 100;
            Projectile.friendly = false;              // 打不到怪，纯粹是个计时器
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;                // 无限穿透（实际上它不碰怪）
            Projectile.timeLeft = 300;                // 活 300 帧 = 5 秒，刚好蓄满
            Projectile.tileCollide = false;           // 不撞方块
            Projectile.aiStyle = 0;                   // 用自定义 AI，不用原版那套
        }

        public override void AI()
        {
            // 找到弹幕的主人（玩家）
            Player player = Main.player[Projectile.owner];
            // 玩家没了就一起死
            if (!player.active || player.dead) { Projectile.Kill(); return; }

            // 看看玩家手里拿的是哪把 siespadital 系列武器，取出它能蓄到几级
            var held = player.HeldItem.ModItem as siespaditalBase;
            int maxLevel = held?.MaxChargeLevel ?? 0;

            // ========== 蓄满 5 秒：结算，给玩家加一层蓄力 ==========
            if (Projectile.timeLeft <= 1)
            {
                var modPlayer = player.GetModPlayer<siespaditalChargePlayer>();
                // 还没到上限就往上加一级
                if (modPlayer.ChargeLevel < maxLevel) modPlayer.ChargeLevel++;
                modPlayer.ChargeTimer = 300;   // 强化持续 5 秒（只在挥剑时才会倒扣，不挥剑不掉）

                // 只有本地玩家才去关掉"持续使用"状态，避免多人下抢控制权
                if (Main.myPlayer == Projectile.owner)
                {
                    player.channel = false;      // 松掉右键的持续状态
                    player.itemTime = 0;         // 清掉使用计时
                    player.itemAnimation = 0;    // 清掉挥动动画
                }

                // 蓄满时的两个音效，一高一低叠在一起
                SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.5f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item74, Projectile.Center);

                // 蓄满"炸一圈"的紫色火花，40 个
                for (int i = 0; i < 40; i++)
                {
                    Vector2 dir = Main.rand.NextVector2CircularEdge(1f, 1f); // 在圆边上随机取一个方向
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch, dir * 15f, 0, default, 2f);
                    d.noGravity = true; // 让它飘着别往下掉
                }
                Projectile.Kill(); // 蓄力弹幕自己消失
                return;
            }

            // ========== 玩家松开右键：取消蓄力 ==========
            if (!Main.mouseRight && Main.myPlayer == Projectile.owner)
            {
                Projectile.Kill();
                return;
            }

            // 每一帧都跟着玩家走，并且强制玩家保持"使用中"姿势（不然剑会垂下来）
            Projectile.Center = player.MountedCenter;
            player.itemTime = 2;
            player.itemAnimation = 2;

            // ========== 蓄力姿势：剑横着举 ==========
            // 鼠标在玩家右边 → 脸朝右，剑指向右；反之朝左
            if (Main.MouseWorld.X > player.Center.X)
            {
                player.direction = 1;
                Projectile.rotation = MathHelper.TwoPi;   // 2π = 0°，剑朝右
            }
            else
            {
                player.direction = -1;
                Projectile.rotation = MathHelper.Pi;      // 180°，剑朝左
            }

            // 蓄力过程中围着自己冒紫色小火星（只有本地玩家画，避免多人重复）
            if (Main.myPlayer == Projectile.owner)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector2 dir = Main.rand.NextVector2CircularEdge(1f, 1f); // 从玩家中心朝外随机方向
                    Dust d = Dust.NewDustPerfect(
                        player.Center + dir * 20f,             // 离玩家 20 像素远的位置
                        DustID.PurpleTorch,
                        dir * Main.rand.NextFloat(3f, 8f),     // 朝外飞，速度随机
                        0, default, 1.5f);
                    d.noGravity = true;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D swordTex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = HandlePixel; // 以剑柄为旋转中心

            // progress：蓄力进度 0→1（timeLeft 从 300 递减到 0）
            float progress = 1f - (float)Projectile.timeLeft / 300f;

            // 进度越高抖得越厉害，满蓄力时抖动幅度 8 像素
            float shake = progress * 8f;
            Vector2 shakeOffset = new Vector2(
                Main.rand.NextFloat(-shake, shake),
                Main.rand.NextFloat(-shake, shake));
            Vector2 drawPos = Projectile.Center - Main.screenPosition + shakeOffset;

            // 剑上叠一层紫色发光，蓄得越满越亮
            Color glowColor = new Color(200, 100, 255) * (0.3f + progress * 0.7f);
            float drawRot = DrawRot(Projectile.rotation);

            // 先画大一号的发光底，再画正常的剑，看起来就有辉光
            Main.EntitySpriteDraw(swordTex, drawPos, null, glowColor, drawRot, origin, 1.1f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(swordTex, drawPos, null, lightColor, drawRot, origin, 1f, SpriteEffects.None, 0);

            return false; // 我们自己画了，就别让原版再画一遍
        }
    }
}
