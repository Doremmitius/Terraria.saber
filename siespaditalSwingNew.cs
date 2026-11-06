using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Graphics;
using heihie.Common;
using heihie.Content.Items;

namespace heihie.Content.Projectiles
{
    // 挥剑时挂在玩家身上的一把"虚拟剑"，负责表现挥砍动画、残影和拖尾，还负责发射追踪飞剑。
    public class siespaditalSwingNew : ModProjectile
    {
        public override string Texture => "heihie/Content/Items/siespadital";

        // ===== 贴图锚点（100×100，剑柄左下，剑尖右上）=====
        private static readonly Vector2 HandlePixel = new Vector2(0f, 100f); // 剑柄在贴图上的像素位置
        private static readonly Vector2 TipPixel    = new Vector2(100f, 0f); // 剑尖在贴图上的像素位置
        private static Vector2 HandleToTip => TipPixel - HandlePixel;        // 剑柄→剑尖的向量
        private static float TipOffset => HandleToTip.ToRotation();          // 贴图剑尖的偏角
        private static float DrawRot(float visualAngle) => visualAngle - TipOffset; // 视觉角度 → 贴图旋转角

        // ===== 拖尾相关 =====
        private readonly VertexStrip strip = new();  // 原版现成的拖尾工具，直接拿来用
        private readonly Vector2[] posArray = new Vector2[20]; // 最近 20 帧的"剑尖位置"记录（画残影和拖尾用）
        private readonly float[] rotArray = new float[20];     // 最近 20 帧的"剑的角度"记录

        public override void SetDefaults()
        {
            Projectile.width = 100;
            Projectile.height = 100;
            Projectile.friendly = false;             // 伤害靠发射的小飞剑，本体不直接打人
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 30;
            Projectile.tileCollide = false;
            Projectile.aiStyle = 0;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];

            // 玩家没了、或者挥剑动画结束了，就自爆
            if (!player.active || player.dead || player.itemAnimation <= 0)
            {
                Projectile.Kill();
                return;
            }

            // 每帧把寿命补回来，相当于"只要还在挥就一直活着"
            Projectile.timeLeft = 2;
            player.heldProj = Projectile.whoAmI; // 告诉玩家"手里的就是这把"，避免切换别的
            player.itemTime = 2;

            // progress：这次挥砍的进度 0 → 1（用于控制剑扫过的角度）
            float progress = 1f - (float)player.itemAnimation / player.itemAnimationMax;
            // aimAngle：从玩家到鼠标的世界角度，用来决定挥砍"朝哪边挥"
            float aimAngle = (Main.MouseWorld - player.MountedCenter).ToRotation();

            // 面向鼠标方向
            if (Main.MouseWorld.X > player.Center.X) player.direction = 1;
            else if (Main.MouseWorld.X < player.Center.X) player.direction = -1;

            // 拿到玩家身上存的蓄力状态，和手里这把武器的参数
            var modPlayer = player.GetModPlayer<siespaditalChargePlayer>();
            var held = player.HeldItem.ModItem as siespaditalBase;

            int volleyCount = held?.VolleyCount ?? 0;      // 每波发几把（0 表示这档武器不发飞剑）
            int maxLevel    = held?.MaxChargeLevel ?? 0;    // 这档武器最高能蓄到几级

            // 实际生效的蓄力等级 = min(玩家存的, 这档武器上限)，防止低档武器吃到高档 buff
            int level = System.Math.Min(modPlayer.ChargeLevel, maxLevel);

            // 只在挥剑期间倒扣蓄力时间，不挥剑就暂停（保证 buff 不会因为挂机溜走）
            if (Main.myPlayer == Projectile.owner && modPlayer.ChargeLevel > 0)
            {
                if (modPlayer.ChargeTimer > 0) modPlayer.ChargeTimer--;
                if (modPlayer.ChargeTimer <= 0)
                {
                    modPlayer.ChargeTimer = 0;
                    modPlayer.ChargeLevel = 0; // 时间用完，蓄力清空
                }
            }

            // localAI[0] 当作帧计数器用（每个弹幕自己一份）
            Projectile.localAI[0]++;

            // ==========================================
            // 发射飞剑：每 8 帧发一波（这就是"连发"的节奏）
            // ==========================================
            if (Projectile.localAI[0] % 8 == 0 && volleyCount > 0)
            {
                Projectile.ai[0] = level;   // 把当前等级同步给 PreDraw，让拖尾颜色跟着变
                Projectile.netUpdate = true; // 告诉联机另一边

                // 实际生成飞剑只在房主/本地玩家这边跑，避免联机双份
                if (Main.myPlayer == Projectile.owner)
                {
                    // 根据蓄力等级选伤害倍率
                    float damageMult = 1f;
                    if (level == 1) damageMult = held?.Level1DamageMult ?? 1.5f;
                    else if (level == 2) damageMult = held?.Level2DamageMult ?? 3f;

                    // 问武器"这一级该发几把"，返回 (最少, 最多)，随机在区间里取
                    var (minC, maxC) = held?.GetVolleyRange(level) ?? (volleyCount, volleyCount);
                    int count = minC == maxC ? minC : Main.rand.Next(minC, maxC + 1);

                    // 扇形的总张角：每多一把就宽 15°，只有一把时张角 0（直直往前射）
                    float spread = count <= 1 ? 0f : MathHelper.ToRadians(15f * count);

                    for (int i = 0; i < count; i++)
                    {
                        // t：这把飞剑在扇形里的位置，0=最左，1=最右，中间平均分布
                        float t = count == 1 ? 0.5f : i / (float)(count - 1);
                        // 实际发射角度 = 瞄准角 ± 扇形一半范围
                        float angle = aimAngle + MathHelper.Lerp(-spread * 0.5f, spread * 0.5f, t);
                        Vector2 vel = angle.ToRotationVector2() * 15f; // 速度 15

                        int p = Projectile.NewProjectile(
                            Projectile.GetSource_FromAI(),
                            player.MountedCenter,
                            vel,
                            ModContent.ProjectileType<siespaditalSeekingSword>(),
                            (int)(Projectile.damage * damageMult), // 伤害按等级放大
                            Projectile.knockBack,
                            Projectile.owner);

                        // 蓄力等级越高，飞剑越大，视觉上更"唬人"
                        if (level == 1) Main.projectile[p].scale = 1.5f;
                        if (level == 2) Main.projectile[p].scale = 2.0f;
                    }
                }
            }

            // ===== 挥砍角度：右 270°→360°，左 270°→180° =====
            float targetRotation;
            if (player.direction == 1)
                targetRotation = MathHelper.Lerp(MathHelper.ToRadians(270f), MathHelper.TwoPi, progress); // 朝右挥
            else
                targetRotation = MathHelper.Lerp(MathHelper.ToRadians(270f), MathHelper.Pi, progress);     // 朝左挥

            Projectile.rotation = targetRotation;
            Projectile.spriteDirection = 1;
            Projectile.Center = player.MountedCenter;

            // 算出"当前这一帧剑尖在世界上的位置"，存进历史记录供残影/拖尾用
            Vector2 currentTip = Projectile.Center + HandleToTip.RotatedBy(DrawRot(Projectile.rotation));

            // 把历史记录整体往后挪一格，再把最新的塞到 [0]
            for (int i = posArray.Length - 1; i > 0; i--)
            {
                posArray[i] = posArray[i - 1];
                rotArray[i] = rotArray[i - 1];
            }
            posArray[0] = currentTip;
            rotArray[0] = Projectile.rotation;

            // 第一帧：把所有历史记录都填成当前位置，避免拖尾从屏幕角落"拉"过来
            if (Projectile.localAI[0] == 1)
            {
                for (int i = 1; i < posArray.Length; i++)
                {
                    posArray[i] = currentTip;
                    rotArray[i] = Projectile.rotation;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D swordTex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = HandlePixel; // 以剑柄为旋转中心

            Player player = Main.player[Projectile.owner];
            var held = player.HeldItem.ModItem as siespaditalBase;
            int ghostCount = held?.SwingGhostCount ?? 1;   // 这一档武器要画几个残影
            if (ghostCount > posArray.Length) ghostCount = posArray.Length; // 别超过历史记录长度

            int level = (int)Projectile.ai[0]; // 当前蓄力等级（AI 里存进来的）

            // 拖尾的默认样式（未蓄力）：紫色，透明度 0.7，宽度 20
            Color baseColor = new Color(180, 100, 255);
            float alpha = 0.7f;
            float width = 20f;

            // 一阶蓄力：金色、更宽；二阶蓄力：红色、最宽
            if (level == 1) { baseColor = Color.Gold; alpha = 0.9f; width = 40f; }
            else if (level == 2) { baseColor = Color.Red; alpha = 1.0f; width = 70f; }

            if (ghostCount > 1)
            {
                // ===== 画残影：越老的残影越透明 =====
                for (int i = ghostCount - 1; i >= 1; i--)
                {
                    float fade = 1f - (float)i / ghostCount; // 越久越淡
                    Color ghostColor = baseColor * (fade * alpha);

                    float ghostRot = DrawRot(rotArray[i]);
                    // 历史记录里存的是"剑尖位置"，这里反推出"剑柄位置"再画
                    Vector2 historicalHandle = posArray[i] - HandleToTip.RotatedBy(ghostRot);
                    Vector2 ghostDrawPos = historicalHandle - Main.screenPosition;

                    Main.EntitySpriteDraw(swordTex, ghostDrawPos, null, ghostColor, ghostRot, origin, 1f, SpriteEffects.None, 0);
                }

                // ===== 画拖尾：把历史位置串成一条渐隐的带子 =====
                Vector2[] drawPosArray = new Vector2[ghostCount];
                float[] drawRotArray = new float[ghostCount];
                for (int i = 0; i < ghostCount; i++)
                {
                    drawPosArray[i] = posArray[i];

                    // 拖尾上每个点的"朝向"用切线算（向前一个点和后一个点的连线方向）
                    Vector2 tangent;
                    if (ghostCount < 2)
                        tangent = rotArray[i].ToRotationVector2(); // 只有一个点，直接用剑朝向
                    else if (i == 0)
                        tangent = posArray[0] - posArray[1];       // 最新点：跟下一个点的差
                    else if (i == ghostCount - 1)
                        tangent = posArray[ghostCount - 2] - posArray[ghostCount - 1]; // 最老点：跟上一个点的差
                    else
                        tangent = posArray[i - 1] - posArray[i + 1]; // 中间点：前后点的差（中心差分，更平滑）

                    drawRotArray[i] = tangent.ToRotation();
                }

                strip.PrepareStrip(
                    drawPosArray,
                    drawRotArray,
                    (p) => baseColor * (1f - p),   // p 从 0→1 表示从新到老，越老越透明
                    (p) => width * (1f - p),       // 越老越细
                    -Main.screenPosition
                );
                strip.DrawTrail();
            }

            // ===== 最后画当前这一帧的剑本体 =====
            Vector2 center = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(swordTex, center, null, lightColor, DrawRot(Projectile.rotation), origin, 1f, SpriteEffects.None, 0);

            return false; // 自己画完了，别让原版再画
        }
    }
}
