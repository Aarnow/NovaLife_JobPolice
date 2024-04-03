using Life.Network;
using Life.UI;
using Life.VehicleSystem;
using ModKit.Helper;
using ModKit.Utils;
using SQLite;

namespace JobPolice.Entities
{
    public class JobPoliceVehicle : ModKit.ORM.ModEntity<JobPoliceVehicle>
    {
        [AutoIncrement][PrimaryKey] public int Id { get; set; }
        public string Plate { get; set; }
        public int ModelId { get; set; }
        public string Reason { get; set; }
        public int CreatedAt { get; set; }
        public JobPoliceVehicle()
        {
        }
    }
}
