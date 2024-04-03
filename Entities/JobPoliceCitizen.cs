using SQLite;

namespace JobPolice.Entities
{
    public class JobPoliceCitizen : ModKit.ORM.ModEntity<JobPoliceCitizen>
    {
        [AutoIncrement][PrimaryKey] public int Id { get; set; }
        public int CharacterId { get; set; }
        public string Pseudonym { get; set; }
        public string Firstname { get; set; }
        public string Lastname { get; set; }
        public string PhoneNumber { get; set; }
        public string Sexe { get; set; }
        public string SkinColor { get; set; }
        public string EyesColor { get; set; }
        public bool Wanted { get; set; }
        public bool Dangerous { get; set; }
        public int CreatedAt { get; set; }
        public JobPoliceCitizen()
        {
        }
    }
}
