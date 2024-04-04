using SQLite;

namespace JobPolice.Entities
{
    public class JobPoliceRecord : ModKit.ORM.ModEntity<JobPoliceRecord>
    {
        [AutoIncrement][PrimaryKey] public int Id { get; set; }
        public int CitizenId { get; set; }
        public int OffenseId { get; set; }
        public int CreatedAt { get; set; }
        public JobPoliceRecord()
        {
        }
    }
}
