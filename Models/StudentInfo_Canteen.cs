using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CanteenManagementSystem.Models
{
    [Table("vw_StudentInfo_Canteen")]
    public class StudentInfo_Canteen
    {
        [Key]
        [StringLength(14)]
        public string StudentID { get; set; }
        [StringLength(14)]
        public string StudentIDC { get; set; }
        public int SourceID { get; set; }
        public int ProgramID { get; set; }
        public int SubjectID { get; set; }
        public int GroupID { get; set; }
        public int ClassID { get; set; }
        public int SectionID { get; set; }
        public int ShiftID { get; set; }
        public int SessionID { get; set; }
        public int VersionID { get; set; }
        public int StudentCatID { get; set; }
        public int HouseID { get; set; }
        public string StudentRoll { get; set; }
        public string StudentName { get; set; }
        public string ContactNo { get; set; }
        public string StudentSex { get; set; }
        public string PhotoPathS { get; set; }
        public string VersionName { get; set; }
        public string ProgramName { get; set; }
        public string SessionName { get; set; }
        public string SectionName { get; set; }

        [NotMapped]
        public string StudentPhoto { get; set; }

        [NotMapped]
        public string StudentGender => StudentSex.Equals("M", StringComparison.CurrentCultureIgnoreCase) ? "Male"
            : StudentSex.Equals("F", StringComparison.CurrentCultureIgnoreCase) ? "Female"
            : "";

        [NotMapped]
        public CanteenUserType UserType => CanteenUserType.Student;
    }
}
