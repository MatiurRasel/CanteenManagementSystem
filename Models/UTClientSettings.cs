using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CanteenManagementSystem.Models
{
    [Table("UTClientSettings")]
    public class UTClientSettings
    {
        [Key]
        public int SrlNo { get; set; }
        public string OptDesc { get; set; }
        public string UserVal { get; set; }
        public DateTime? CreateDate { get; set; }
        public int CreateBy { get; set; }
        public string CompNm { get; set; }
    }
}
