using JobPolice.Entities;
using Life;
using Life.Network;
using ModKit.Utils;
using System;
using mk = ModKit.Helper.TextFormattingHelper;

namespace JobPolice
{
    public class MyEvents : ModKit.Helper.Events
    {
        public MyEvents(IGameAPI api) : base(api)
        {
        }

        public async override void OnPlayerConsumeAlcohol(Player player, int itemId, float alcoholValue)
        {
            Console.WriteLine($"{player.GetFullName()} vient de boire {alcoholValue} degré d'alcool.");

            var query = await JobPoliceDrugs.Query(d => d.CharacterId == player.character.Id);
            if(query != null && query.Count > 0)
            {
                query[0].LastAlcohol = DateUtils.GetCurrentTime();
                await query[0].Save();
            } else
            {
                var newJobPoliceDrugs = new JobPoliceDrugs();
                newJobPoliceDrugs.CharacterId = player.character.Id;
                newJobPoliceDrugs.LastAlcohol = DateUtils.GetCurrentTime();
                await newJobPoliceDrugs.Save();
            }
        }

        public async override void OnPlayerConsumeDrug(Player player)
        {
            Console.WriteLine($"le joueur {player.GetFullName()} vient de consommer du cannabis");
            var query = await JobPoliceDrugs.Query(d => d.CharacterId == player.character.Id);
            if (query != null && query.Count > 0)
            {
                query[0].LastCannabis = DateUtils.GetCurrentTime();
                await query[0].Save();
            }
            else
            {
                var newJobPoliceDrugs = new JobPoliceDrugs();
                newJobPoliceDrugs.CharacterId = player.character.Id;
                newJobPoliceDrugs.LastCannabis = DateUtils.GetCurrentTime();
                await newJobPoliceDrugs.Save();
            }
        }

        public override void OnPlayerDamagePlayer(Player fromPlayer, Player toPlayer, int damage)
        {
            if (fromPlayer.setup.NetworkisRestrain)
            {
                fromPlayer.setup.TargetShowCenterText($"{mk.Color("AVERTISSEMENT", mk.Colors.Warning)}", "Vous ne pouvez pas frapper un joueur en étant menotté. [Powergaming]", 5);
                toPlayer.Health += damage;
            }
        }
    }
}
