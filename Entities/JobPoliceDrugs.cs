using SQLite;

namespace JobPolice.Entities
{
    public class JobPoliceDrugs : ModKit.ORM.ModEntity<JobPoliceDrugs>
    {
        [AutoIncrement][PrimaryKey] public int Id { get; set; }
        public int CharacterId { get; set; }
        public long LastCannabis { get; set; }
        public long LastAlcohol { get; set; }
        public JobPoliceDrugs()
        {
        }
    }
}
