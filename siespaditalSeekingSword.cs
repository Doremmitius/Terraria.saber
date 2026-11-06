using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace heihie.Content.Projectiles
{
    // 发射出去的追踪小飞剑。会自己找最近的敌人并拐过去，打 4 次就消失。
    public class siespaditalSeekingSword : ModProjectile
    {
        // ===== 贴图锚点（100×100，剑柄左下，剑尖右上）=====
        private static readonly Vector2 TipPixel    = new Vector2(100f, 0f);  // 剑尖在贴图上的位置
        private static readonly Vector2 HandlePixel = new Vector2(0f, 100f);  // 剑柄在贴图上的位置
        private static Vector2 HandleToTip => TipPixel - HandlePixel;         // 剑柄→剑尖的向量
        private static float TipOffset => HandleToTip.ToRotation();           // 贴图剑尖的偏角
        // 把"想朝向的角度"转成"贴图该旋转的角度"
        private static float DrawRot(float visualAngle) => visualAngle - TipOffset;

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;              // 能打怪
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 4;                // 最多打 4 个敌人
            Projectile.timeLeft = 180;               // 最多活 3 秒
            Projectile.light = 0.5f;                 // 自身会发光
            Projectile.ignoreWater = true;           // 不受水减速
            Projectile.tileCollide = false;          // 穿墙飞
            Projectile.aiStyle = 0;                  // 自定义 AI
            Projectile.usesLocalNPCImmunity = true;  // 每只怪单独算无敌帧
            Projectile.localNPCHitCooldown = 15;     // 同一只怪 15 帧内只吃一次伤害
        }

        public override void AI()
        {
            // 剑身永远朝速度方向（看起来像在"冲"）
            Projectile.rotation = Projectile.velocity.ToRotation();

            // ai[0] 里存的是"目标 NPC 的索引 + 1"（因为 0 要留给"没有目标"）
            int targetIdx = (int)Projectile.ai[0] - 1;
            bool needTarget = true; // 先假设没有可用目标，下面检查

            // 检查之前记下的目标还活着吗
            if (targetIdx >= 0 && targetIdx < Main.maxNPCs)
            {
                NPC target = Main.npc[targetIdx];
                if (target.active && target.life > 0)
                {
                    needTarget = false; // 目标还活着，不用重新找
                    // 朝目标方向慢慢拐过去
                    float targetRot = (target.Center - Projectile.Center).ToRotation(); // 目标方向
                    float currentRot = Projectile.velocity.ToRotation();                // 当前方向
                    float newRot = currentRot.AngleLerp(targetRot, 0.15f);              // 每帧拐一点点（0.15 是转向力度）
                    Projectile.velocity = newRot.ToRotationVector2() * 15f;             // 保持 15 的速度
                }
            }

            // 没目标（没锁定过 / 锁定目标死了）→ 重新找
            if (needTarget)
            {
                int newTarget = FindTarget();
                Projectile.ai[0] = newTarget + 1; // 存起来时 +1，0 表示"当前没有目标"
                if (newTarget == -1)
                {
                    // 全屏都没怪，慢慢减速，速度太低就自爆
                    Projectile.velocity *= 0.95f;
                    if (Projectile.velocity.Length() < 0.5f) Projectile.Kill();
                }
            }

            // 每帧在自身周围撒一点紫色火星当尾迹
            Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.PurpleTorch);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // 打中时来一声低沉的爆炸音
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.2f, Volume = 0.6f }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            // 飞剑绕"剑身中心"旋转（贴图中心 = 剑身对角线中点 = (50,50)）
            Vector2 origin = tex.Size() / 2f;
            float drawRot = DrawRot(Projectile.rotation);

            Main.EntitySpriteDraw(tex, drawPos, null, lightColor, drawRot,
                origin, Projectile.scale, SpriteEffects.None, 0);
            return false; // 自己画了，别让原版再画
        }

        // 在 800 像素范围内找最近的、能打的敌人；找不到返回 -1
        private int FindTarget()
        {
            int bestTarget = -1;                // 目前选中的目标，-1 表示还没有
            float minDistSq = 800f * 800f;      // 搜索半径 800，用平方比较省得开根号

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                // 跳过：不活跃 / 友军 / 已死 / 不能被追的（比如某些NPC阶段无敌）
                if (!npc.active || npc.friendly || npc.life <= 0 || !npc.CanBeChasedBy()) continue;

                float distSq = Vector2.DistanceSquared(Projectile.Center, npc.Center); // 跟这只怪的距离平方
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    bestTarget = i; // 找到更近的就换目标
                }
            }
            return bestTarget;
        }
    }
}
