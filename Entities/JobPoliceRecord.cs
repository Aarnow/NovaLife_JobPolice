using SQLite;
using System.Collections.Generic;

namespace JobPolice.Entities
{
    public class JobPoliceRecord : ModKit.ORM.ModEntity<JobPoliceRecord>
    {
        [AutoIncrement][PrimaryKey] public int Id { get; set; }
        public int CitizenId { get; set; }
        public string OffenseList { get; set; }
        [Ignore]
        public List<int> LOffenseList { get; set; } = new List<int>();
        public bool IsPaid { get; set; }
        public string CreatedBy { get; set; }
        public int CreatedAt { get; set; }
        public JobPoliceRecord()
        {
        }
    }
}
