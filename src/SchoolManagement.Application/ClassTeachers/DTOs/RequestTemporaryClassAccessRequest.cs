using System;
using System.Collections.Generic;
using System.Text;

namespace SchoolManagement.Application.ClassTeachers.DTOs
{
    public class RequestTemporaryClassAccessRequest
    {
        public int AcademicYearId { get; set; }

        public int SchoolClassId { get; set; }

        public string? Reason { get; set; }
    }
}
