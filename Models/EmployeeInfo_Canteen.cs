using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CanteenManagementSystem.Models
{
    [Table("vw_EmployeeInfo_Canteen")]
    public class EmployeeInfo_Canteen
    {
        [Key]
        [StringLength(15)]
        public string EmployeeID { get; set; }
        [StringLength(200)]
        public string EmployeeName { get; set; }
        public string MobileNo { get; set; }
        public string DesignationName { get; set; }
        public string EmployeeTypeName { get; set; }
        public string EmployeePhotoPath { get; set; }
        public string EmployeeGender { get; set; }
        [NotMapped]
        public string EmployeePhoto { get; set; }
        [NotMapped]
        public CanteenUserType UserType => CanteenUserType.Employee;
    }
}
