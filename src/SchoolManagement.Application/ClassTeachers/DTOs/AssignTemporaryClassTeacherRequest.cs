using System;
using System.Collections.Generic;
using System.Text;

namespace SchoolManagement.Application.ClassTeachers.DTOs
{
    public class AssignTemporaryClassTeacherRequest
    {
        public int AcademicYearId { get; set; }

        public int SchoolClassId { get; set; }

        public int StaffId { get; set; }

        public string? Reason { get; set; }
    }
}
