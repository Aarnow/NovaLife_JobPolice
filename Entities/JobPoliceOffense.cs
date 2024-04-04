using SQLite;
using System.Collections.Generic;
namespace JobPolice.Entities
{

    public class JobPoliceOffense : ModKit.ORM.ModEntity<JobPoliceOffense>
    {
        [AutoIncrement][PrimaryKey] public int Id { get; set; }
        public string Title { get; set; }
        public int PrisonTime { get; set; }
        public double Money { get; set; }
        public int Points { get; set; }
        public string OffenseType { get; set; }

        [Ignore]
        public Dictionary<string, int> OffenseTag { get; set; } = new Dictionary<string, int>()
        {
            { "Crime", 1626 },
            { "Infraction routière", 37 },
            { "Délit", 1463 },
            { "Autres", 87 },
            { "Stupéfiants", 28 }
        };

        public JobPoliceOffense()
        {
        }
    }
}
